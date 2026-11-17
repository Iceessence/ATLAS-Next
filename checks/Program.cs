using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using AtlasNext;

internal static class Checks
{
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

    [STAThread] static int Main()
    {
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
        Console.WriteLine($"All {count} checks passed. Temporary profile: {root}");
        return 0;
    }
}
