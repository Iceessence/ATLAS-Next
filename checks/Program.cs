using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using AtlasNext;

internal static class Checks
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
    static extern bool CreateHardLink(string link,string existing,IntPtr reserved);
    static int count;
    static void Check(bool ok,string name)
    {
        if(!ok) throw new InvalidOperationException(name);
        Console.WriteLine($"PASS {++count}: {name}");
    }
    static void Click(WorkWindow window,string name) =>
        ((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static void CheckRecovery(string profile)
    {
        string directory=Path.Combine(profile,"tasks");
        string PathFor(Guid id)=>Path.Combine(directory,id.ToString("N")+".json");
        Guid completed=Guid.NewGuid(), pending=Guid.NewGuid();
        using(var engine=new Engine(profile)) {
            engine.Finish(engine.Submit(completed,"fact.format").Id);
            engine.Submit(pending,"procedure.restore");
        }

        // A schema-1 history from NEXT-001 has no per-check verdicts.
        var legacy=JsonNode.Parse(File.ReadAllText(PathFor(completed)))!.AsObject();
        legacy.Remove("sourceIntegrity");
        legacy.Remove("answerRelevance");
        File.WriteAllText(PathFor(completed),legacy.ToJsonString());
        var healthy=File.ReadAllBytes(PathFor(completed));
        var waiting=File.ReadAllBytes(PathFor(pending));
        var fixtures=new Dictionary<string,byte[]>();
        void Add(string name,string content) {
            var bytes=System.Text.Encoding.UTF8.GetBytes(content);
            fixtures.Add(name,bytes);
            File.WriteAllBytes(Path.Combine(directory,name),bytes);
        }
        string Name()=>Guid.NewGuid().ToString("N")+".json";
        void Mutate(Action<JsonObject> edit) {
            var id=Guid.NewGuid();
            var node=JsonNode.Parse(waiting)!.AsObject();
            node["id"]=id;
            edit(node);
            Add(id.ToString("N")+".json",node.ToJsonString());
        }
        foreach(string broken in new[]{"{","","null","{}","[]"}) Add(Name(),broken);
        Add("broken.json","{}");
        Add(Guid.NewGuid().ToString("D")+".json",legacy.ToJsonString());
        Add(Name()+".pending","unfinished orphan");
        Add(Path.GetFileName(PathFor(pending))+".pending","unfinished replacement");
        Mutate(n=>n.Remove("schema"));
        Mutate(n=>n["schema"]=2);
        Mutate(n=>n.Remove("id"));
        Mutate(n=>n["id"]=Guid.NewGuid());
        Mutate(n=>n["request"]=null);
        Mutate(n=>n["events"]=new JsonArray());
        Mutate(n=>n["events"]=null);
        Mutate(n=>n["events"]=new JsonArray((JsonNode?)null));
        Mutate(n=>n["proposal"]!["sha256"]="abc");
        Mutate(n=>n["evidence"]=null);

        string locked=Path.Combine(directory,"ffffffffffffffffffffffffffffffff.json");
        using(var guard=new FileStream(locked,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
            try { using var first=new Engine(profile); throw new Exception("Expected unreadable fixture"); }
            catch(IOException) {
                Check(waiting.SequenceEqual(File.ReadAllBytes(PathFor(pending))) &&
                    fixtures.Keys.All(name=>File.Exists(Path.Combine(directory,name))),
                    "preflight I/O failure leaves every fixture and pending review untouched");
            }
        }
        fixtures.Add(Path.GetFileName(locked),[]);
        using(var engine=new Engine(profile)) {
            var tasks=engine.History().ToList();
            Check(tasks.Count==2 && tasks.Single(w=>w.Id==pending).State=="interrupted","mixed corrupt profile recovers complete healthy history");
            Check(healthy.SequenceEqual(File.ReadAllBytes(PathFor(completed))) &&
                tasks.Single(w=>w.Id==completed).AnswerRelevance=="not checked","legacy completion preserved byte-for-byte with relevance unassessed");
            var files=Directory.GetFiles(Path.Combine(profile,"quarantine"),"*",SearchOption.AllDirectories);
            Check(files.Length==fixtures.Count && files.All(p=>File.ReadAllBytes(p).SequenceEqual(fixtures[Path.GetFileName(p)])),
                $"all {fixtures.Count} corrupt and pending originals preserved byte-for-byte");
            var window=new WorkWindow(engine,profile);
            window.Show();
            Check(((TextBlock)window.FindName("RecoveryInfo")).Text.Contains($"Recovered 1 interrupted tasks. Quarantined {fixtures.Count} files"),
                "mixed-profile recovery counts visible in real window");
            window.Close();
        }
        using var reopened=new Engine(profile);
        Check(reopened.History().Single(w=>w.Id==pending).Events.Length==3 &&
            reopened.RecoveryNotice.Contains("Recovered 0 interrupted tasks. Quarantined 0 files"),
            "second restart preserves quarantine and adds no recovery events");
    }

    static void CheckDocuments(string root)
    {
        Directory.CreateDirectory(root);
        string path=Path.Combine(root,"policy.md");
        string profile=Path.Combine(root,"profile");
        Work Ask(Engine e,string q)=>e.Finish(e.Submit(Guid.NewGuid(),q).Id);
        string question="Which value is recorded for the Orion colour?";
        const string text="# Notes 🚀\r\nOrion colour: blå\r\nValid through: 2099-01-01\r\n";
        File.WriteAllText(path,text,new System.Text.UTF8Encoding(true));
        string stable;
        using(var e=new Engine(profile)) {
            var doc=e.Import(path);
            stable=doc.Id;
            Check(doc.Version==1 && e.Import(path)==doc,"idempotent owner import and stable identity");
            var work=Ask(e,question);
            var span=work.Evidence?.Span;
            Check(work is {State:"completed",Result:"blå",Scope:"owner-local"} && span is {Line:2} &&
                ("\uFEFF"+text).Substring(span.Start,span.Length)=="blå" &&
                File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(Path.Combine(profile,span.Path))),
                "UTF-8 BOM, surrogate pair and CRLF preserved with exact UTF-16 value span");
            Check(work.ResearchReview is {Verdict:"supported",Trust:"owner-selected text"} &&
                work.ResearchReview.Freshness.Contains("2099-01-01"),"research verdict retains trust and declared validity");
            var narrowed=e.Submit(Guid.NewGuid(),question,"absent-from-project");
            Check(narrowed is {State:"needs clarification",Evidence:null,ResearchReview.Verdict:"supported"},
                "Research support cannot override a Primary source restriction");
            var revoked=e.Submit(Guid.NewGuid(),question);
            File.WriteAllText(Path.Combine(profile,"tasks",revoked.Id.ToString("N")+".json"),
                JsonSerializer.Serialize(revoked with {Authorization="pending"},Assets.Json));
            Check(e.Finish(revoked.Id).State=="failed","research evidence cannot replace host authorization");
            foreach(var field in new[]{"Scope","Specialist"}) {
                var issued=e.Submit(Guid.NewGuid(),"What is the Orion colour set to, please?");
                Check(issued.State=="review",$"paraphrase issued before changing {field}");
                var altered=field=="Scope" ? issued with {Scope="public-demo"} : issued with {Specialist="procedure"};
                File.WriteAllText(Path.Combine(profile,"tasks",issued.Id.ToString("N")+".json"),JsonSerializer.Serialize(altered,Assets.Json));
                Check(e.Finish(issued.Id).State=="failed",$"delivery rejects a changed {field}");
            }
            var waiting=e.Submit(Guid.NewGuid(),question);
            var second=Path.Combine(root,"second.txt");
            File.WriteAllText(second,"Orion colour is green.");
            e.Import(second);
            var conflict=e.Finish(waiting.Id);
            Check(conflict is {State:"needs clarification",SourceIntegrity:"passed",AnswerRelevance:"subject and intent matched",ResearchReview.Verdict:"contradiction"} &&
                conflict.ResearchReview.Sources.Length==2,"delivery rechecks new contradicting source and withholds answer");
            File.WriteAllText(second,"Orion colour: blå");
            e.Import(second);
            Check(Ask(e,question).ResearchReview is {Verdict:"supported",Sources.Length:2},"independent documents agreeing on one value");
            File.WriteAllText(second,"Orion colour: blå\nValid through: 2000-01-01");
            e.Import(second);
            Check(Ask(e,question).ResearchReview?.Verdict=="stale","expired matching source withholds delivery");
            File.WriteAllText(second,"Archive retention: 90 days");
            e.Import(second);
            Check(Ask(e,"What is the archive retention policy?").ResearchReview is {Verdict:"contradiction",Sources.Length:2},
                "pinned and imported sources compared on the same claim");
            Check(Ask(e,"How are archives restored?").Evidence?.Id=="procedure.restore","restored paraphrase retrieves procedure");
            File.WriteAllText(path,"Orion colour: violet");
            var revision=e.Import(path);
            Check(revision.Id==stable && revision.Version==2 && File.Exists(Path.Combine(profile,span!.Path)),
                "changed selection creates immutable revision under same document ID");
            var current=Ask(e,question);
            Check(current.Result=="violet" && current.Evidence?.Version==2,"active revision supersedes old claim");
            File.WriteAllText(path,"Original file was later edited");
            Check(Ask(e,question).Result=="violet","owned copy independent of subsequent edits to selected path");
            var pending=e.Submit(Guid.NewGuid(),question);
            string stored=Path.Combine(profile,current.Evidence!.Span!.Path);
            var original=File.ReadAllBytes(stored);
            try { File.AppendAllText(stored,"!"); Check(e.Finish(pending.Id).State=="failed","altered owned source blocked at finish"); }
            finally { File.WriteAllBytes(stored,original); }
            void Reject(string name,byte[] bytes) {
                string bad=Path.Combine(root,name);
                File.WriteAllBytes(bad,bytes);
                try { e.Import(bad); throw new Exception("Unsafe import accepted"); }
                catch(InvalidDataException) { Check(true,$"reject {name}"); }
            }
            Reject("encoding.txt",[0xff,0xfe]);
            Reject("binary.md",[0]);
            Reject("oversize.txt",new byte[256*1024+1]);
            Reject("script.ps1",[42]);
            var terms=Path.Combine(root,"terms.txt");
            File.WriteAllText(terms,"Restore manifest entries: 17\nConfigure guide title: Amber\nRecovery steps count: 6\nArchive: file");
            e.Import(terms);
            foreach(var (prompt,value) in new[]{("What is the restore manifest entries?","17"),
                ("Name the configure guide title.","Amber"),("Which value is recorded for the recovery steps count?","6")})
                Check(Ask(e,prompt) is {State:"completed",Specialist:"fact"} found && found.Result==value,
                    $"affirmative imported lookup: {prompt}");
            var relations=Path.Combine(root,"relations.md");
            const string relationText="# Repair notes 🔧\r\nPrimary decoder parameters: α, β\r\n"+
                "Secondary decoder declaration line: 71\r\nRuby requirement: >=3.2\r\n"+
                "Wheel generator: forge 2\r\nWheel: legacy\r\n# Installed payload audit\r\nChecked payload files: 17";
            File.WriteAllText(relations,relationText);
            e.Import(relations);
            foreach(var (prompt,value) in new[]{
                ("Which parameters does the primary decoder take, please?","α, β"),
                ("Where is the secondary decoder declared?","71"),
                ("What Ruby version does the package require?",">=3.2"),
                ("Which tool generated the wheel?","forge 2"),
                ("How many installed files did the audit check?","17"),
                ("What is the configure guide title recorded as?","Amber")}) {
                var answer=Ask(e,prompt);
                Check(answer is {State:"completed",ResearchReview.Verdict:"supported",Evidence.Span:{} anchor} &&
                    answer.Result==value && File.ReadAllText(Path.Combine(profile,anchor.Path)).Substring(anchor.Start,anchor.Length)==value,
                    $"whole request and exact UTF-16 evidence for {prompt}");
            }
            foreach(var prompt in new[]{"Which parameters does the secondary decoder take?",
                "Which parameters does the primary decoder take in recovery mode?",
                "Where is the secondary decoder declared and who owns it?",
                "How many modified installed files did the audit check?",
                "What Ruby version does the previous installation require?",
                "What is the configure guide title recorded as in production?"})
                Check(Ask(e,prompt) is {State:"needs clarification",Evidence:null},$"unsupported constraint withheld for {prompt}");
            File.WriteAllText(relations,"# Download audit\nChecked payload files: 99\nRuby requirement: enabled\nSecondary decoder declaration line: unknown");
            e.Import(relations);
            foreach(var prompt in new[]{"How many installed files did the audit check?","What Ruby version does the package require?",
                "Where is the secondary decoder declared?"})
                Check(Ask(e,prompt) is {State:"needs clarification",ResearchReview.Verdict:"insufficient"},$"missing relation evidence withheld for {prompt}");
            File.WriteAllText(relations,"Local switch: amber\nLocal switch set to: violet");
            e.Import(relations);
            Check(Ask(e,"What is the local switch set to?") is {State:"needs clarification",ResearchReview.Verdict:"ambiguous"},
                "helper and literal project name collision requires clarification");
            Check(Ask(e,"How do I restore an archive?").Evidence?.Id=="procedure.restore",
                "generic imported subject cannot displace a handbook procedure");
            Check(Ask(e,"What is the archive restoration?").State=="needs clarification","unanswered definition withheld");
            File.WriteAllText(second,"Orion colour: undetermined");
            e.Import(second);
            Check(Ask(e,question) is {State:"needs clarification",ResearchReview.Verdict:"insufficient"},
                "definite value plus unconfirmed value is insufficient");
            File.WriteAllText(second,"Separate claim: other");
            e.Import(second);
            var privateTask=e.Submit(Guid.NewGuid(),question);
            e.SelectProject("lunar");
            Check(!e.History().Any() && Ask(e,question).State=="needs clarification","project isolation for history and retrieval");
            try { e.Finish(privateTask.Id); throw new Exception("Cross-project finish"); }
            catch(InvalidOperationException){Check(true,"task delivery confined to its issuing project");}
            var lunar=Path.Combine(root,"lunar.md");
            File.WriteAllText(lunar,"Orion colour: red");
            e.Import(lunar);
            var red=Ask(e,question);
            Check(red is {Result:"red",Project:"lunar"} && File.Exists(Path.Combine(profile,red.Evidence!.Span!.Path)),
                "named project produces resolvable project-local citations");
            e.SelectProject("default");
            Check(e.Finish(privateTask.Id).Result=="violet","switching back restores original project scope");
            File.WriteAllText(second,"Telemetry interval: TBD");
            e.Import(second);
            Check(Ask(e,"What is the telemetry interval?").ResearchReview?.Verdict=="insufficient",
                "unknown value is insufficient factual support");
            var hard=Path.Combine(root,"alias.txt");
            if(!CreateHardLink(hard,path,IntPtr.Zero)) throw new IOException("Cannot create hard-link fixture");
            try { e.Import(hard); throw new Exception("Accepted hard link"); }
            catch(InvalidDataException) { Check(true,"reject hard-linked file without elevation"); }
        }
        using var reopened=new Engine(profile);
        Check(Ask(reopened,question) is {Result:"violet",Evidence.Version:2},"index rebuilt from owned revisions after restart");
    }

    [STAThread] static int Main(string[] args)
    {
        if(args is ["--code-probe",string scratch]) return OutcomeProbe.Code(scratch);
        if(args is ["--outcome"]) return OutcomeProbe.Run();
        var root=Path.Combine(Path.GetTempPath(),"atlas-next-check-"+Guid.NewGuid().ToString("N"));
        var router=new Routing();
        using var golden=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"golden.json")));
        Check(golden.RootElement.EnumerateArray().All(row=>{
            var result=router.Infer(row.GetProperty("prompt").GetString()!);
            return result.Route==row.GetProperty("route").GetString() &&
                   Math.Abs(result.Probability-row.GetProperty("probability").GetDouble())<1e-10;
        }),"all public golden logits-derived predictions match Python to 1e-10");
        var ckpt=Path.Combine(Assets.Root,"native.json");
        var original=File.ReadAllBytes(ckpt);
        try {
            File.AppendAllText(ckpt," ");
            try { _=new Routing(); throw new Exception("Expected checkpoint rejection"); }
            catch(InvalidDataException){ Check(true,"altered checkpoint refused"); }
        } finally {File.WriteAllBytes(ckpt,original);}
        Guid id=Guid.NewGuid(), pending=Guid.NewGuid();
        using(var engine=new Engine(root))
        {
            var work=engine.Submit(id,"What is the archive retention policy?");
            Check(work.State=="review" && work.Evidence?.Id=="fact.retention","persisted scoped fact proposal");
            Check(work.Proposal is {Origin:"original",Qualification:"shadow",Sha256.Length:64},"local original CPU inference");
            Check(engine.Submit(id,work.Request).Events.Length==2,"idempotent task submission");
            try {engine.Submit(id,"different request");throw new Exception("Expected identity conflict");}
            catch(InvalidOperationException){Check(true,"conflicting retry refused");}
            Check(engine.Finish(id).Result!.Contains("30 days"),"verified actual record body");
            Check(engine.Finish(id,true).State=="completed","late cancellation preserves completed evidence");
            foreach(var (prompt, expected) in new (string,string?)[] {
                ("How long is the archive retention policy?", "fact.retention"),
                ("How long will the archive retention policy keep exports?", "fact.retention"),
                ("How many days is archive retention?", "fact.retention"),
                ("How is an export receipt formatted?", "fact.format"),
                ("Name the archive format.", "fact.format"),
                ("What is the archive restoration?", null),
                ("What is the retention policy for uranium?", null),
                ("How long will atlasprint keep its archive?", null),
                ("How many exports are in the archive?", null),
                ("What is the archive retention policy and receipt format?", null),
                ("How do I change the archive retention policy?", null),
                ("What is the archive retention policy for 水?", null)
            }) {
                var answer=engine.Finish(engine.Submit(Guid.NewGuid(),prompt).Id);
                Check(answer.Evidence?.Id==expected && answer.State==(expected==null?"needs clarification":"completed"),
                    $"scope and intent for '{prompt}'");
            }
            // Substitute an intact, correctly scoped same-type citation after retrieval.
            var swapped=engine.Submit(Guid.NewGuid(),"How long is the archive retention policy?");
            var format=new Library().Exact("fact.format")!;
            var edited=swapped with {Evidence=format.Citation};
            File.WriteAllText(Path.Combine(root,"tasks",swapped.Id.ToString("N")+".json"),JsonSerializer.Serialize(edited,Assets.Json));
            var withheld=engine.Finish(edited.Id);
            Check(withheld is {State:"needs clarification",SourceIntegrity:"passed",AnswerRelevance:"unsupported"} &&
                withheld.Result!=format.Record.Body,"delivery rejects an authentic same-type citation for the wrong subject");
            var step=engine.Submit(Guid.NewGuid(),"How do I restore an archive?");
            Check(step.Evidence?.Id=="procedure.restore","procedure retrieval");
            Check(engine.Finish(step.Id,true).Result==null && engine.Finish(step.Id).State=="cancelled","cancel prevents delivery");
            Check(engine.Submit(Guid.NewGuid(),"unknown topic").State=="needs clarification","no evidence handling");
            Check(engine.Submit(Guid.NewGuid(),"what is uranium?").Evidence==null,"out of corpus abstention");
            Check(engine.Submit(Guid.NewGuid(),"fact.format").Evidence?.Id=="fact.format","exact ID retrieval");
            engine.Submit(pending,"procedure.restore");
            try { using var second=new Engine(root);throw new Exception("Expected lease denial"); }
            catch(IOException){Check(true,"second writer denied");}
            try { engine.Submit(Guid.NewGuid(),new string('a',2049));throw new Exception("Budget ignored"); }
            catch(ArgumentException){Check(true,"request budget enforced");}
        }
        using(var engine=new Engine(root))
        {
            Check(engine.History().Single(x=>x.Id==pending).State=="interrupted","pending review safely recovered");
            Check(engine.History().Single(x=>x.Id==id).State=="completed","completed result survives reopen");
            var window=new WorkWindow(engine,root);
            window.Show();
            Click(window,"Search"); Click(window,"Verify"); Click(window,"Export");
            var receipt=JsonSerializer.Deserialize<Work>(File.ReadAllText(Directory.GetFiles(Path.Combine(root,"exports")).Single()),Assets.Json)!;
            Check(receipt.State=="completed" && receipt.Evidence?.Id=="fact.retention" && receipt.SourceIntegrity=="passed" && receipt.AnswerRelevance=="supported","real WPF find, verify and export handlers");
            Click(window,"Search"); Click(window,"Stop");
            Check(((TextBlock)window.FindName("Status")).Text=="cancelled","real WPF stop control");
            Click(window,"Search"); Click(window,"Search"); Click(window,"EmergencyStop");
            Check(!engine.History().Any(task=>task.State=="review"),"global stop cancels all pending tasks");
            window.Close();
            var path=Path.Combine(Assets.Root,"library","retention.json");
            var bytes=File.ReadAllBytes(path);
            var task=engine.Submit(Guid.NewGuid(),"fact.retention");
            try {File.AppendAllText(path," "); Check(engine.Finish(task.Id).State=="failed","modified evidence blocks delivery");}
            finally {File.WriteAllBytes(path,bytes);}
        }
        CheckRecovery(Path.Combine(root,"mixed"));
        CheckDocuments(Path.Combine(root,"import"));
        CodeChecks.Run(Path.Combine(root,"code"),Check);
        Console.WriteLine($"All {count} checks passed. Temporary profile: {root}");
        return 0;
    }
}
