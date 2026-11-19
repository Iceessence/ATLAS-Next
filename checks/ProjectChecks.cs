using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using AtlasNext;

internal static class ProjectChecks
{
    public static void Run(string root,Action<bool,string> check)
    {
        Directory.CreateDirectory(root);
        string profile=Path.Combine(root,"profile");
        using var engine=new Engine(profile);
        Imported Put(string name,string text) {
            var file=Path.Combine(root,name);
            File.WriteAllText(file,text,new UTF8Encoding(false));
            return engine.Import(file);
        }
        Work Review(string request)=>engine.Submit(Guid.NewGuid(),request);
        Work Ask(string request)=>engine.Finish(Review(request).Id);
        const string typed="Where is Meter.Read(int) defined?";
        const string calls="Where is Meter.Read(int) called?";
        const string compare="Compare project claims: Output mode | Retention window";
        const string target="""
            namespace Metering;
            public static class Meter {
                public static int Read() => 0;
                /// <summary>Read a numeric channel.</summary>
                public static int Read(int n) => n;
                public static int Read(string n) => n.Length;
            }
            """;
        Put("Meter.cs","\uFEFF"+target);
        var original=Ask(typed);
        check(original is {State:"completed",Result:"Read",Evidence.Span.Line:5},"select same-type overload with BOM-adjusted exact identifier");
        check(Ask("Where is Meter.Read() defined?").Evidence?.Span?.Line==3,"select zero-parameter overload");
        check(Ask("Where is Meter.Read(string) defined?").Evidence?.Span?.Line==6,"select second one-parameter type");
        check(Ask("Where is Meter.Read(1 parameter) defined?").ResearchReview?.Verdict=="ambiguous","same-arity overloads withhold");
        check(Ask("Where is Meter.Read(ref int) defined?").State=="needs clarification","unsupported selector withholds");
        check(Ask("Where is meter.Read(int) defined?").State=="needs clarification","typed owner is case-sensitive");
        var follow=engine.Submit(Guid.NewGuid(),"Show its summary.",null,original.Id);
        check(engine.Finish(follow.Id).Result=="<summary>Read a numeric channel.</summary>","selected overloaded declaration owns follow-up span");

        const string caller="""
            using Metering;
            using static Metering.Meter;
            namespace Outside;
            public class Consumer {
                public int Run() => Meter.Read(7) + Read(8);
                public int Different() => Meter.Read("x");
                public string Decoy() => "Meter.Read(10) 😀";
                // Meter.Read(11)
                public int Shadowed() { var Meter = new Proxy(); return Meter.Read(12); }
                public int LateBound(dynamic n) => Meter.Read(n);
            }
            public class Proxy { public int Read(int n) => n; }
            """;
        var client=Put("Consumer.cs",caller);
        var pending=Review(calls);
        var bound=engine.Finish(pending.Id);
        check(pending is {State:"review",Result:null} && bound is {State:"completed",ResearchReview.Sources.Length:3} &&
            bound.Result=="Consumer.cs, line 5: Meter.Read(7)\nConsumer.cs, line 5: Read(8)",
            "source-bound invocations exclude strings, comments, shadowed receivers, other overloads and dynamic binding");
        check(bound.ResearchReview!.Sources.Skip(1).All(c=>c.Id==client.Id && c.Version==1 && c.Span!=null &&
            caller.Substring(c.Span.Start,c.Span.Length).EndsWith(')')),"each invocation carries its own revision, digest and exact span");
        check(engine.Submit(Guid.NewGuid(),calls,client.Id).State=="needs clarification","Primary cannot narrow away required definition evidence");
        var before=Review(calls);
        Put("Consumer.cs",caller.Replace("Meter.Read(7)","Meter.Read(17)"));
        check(engine.Finish(before.Id).ResearchReview?.Verdict=="stale","changed secondary call evidence withholds before delivery");
        before=Review(calls);
        Put("More.cs","class Extra { int X() => Metering.Meter.Read(22); }");
        check(engine.Finish(before.Id).ResearchReview?.Verdict=="stale","new supporting call set requires a fresh task");
        var duplicate=Put("Other.cs","namespace Other; class Meter { int Read(int n) => n; }");
        check(Ask(calls).ResearchReview is {Verdict:"ambiguous",Sources.Length:2},"duplicate qualified typed declarations withhold call lookup");
        Put("Other.cs","namespace Other; class Different { int Read(int n) => n; }");
        check(Ask(calls).ResearchReview?.Sources.All(c=>c.Id!=duplicate.Id)==true,"unrelated owner cannot enter bound citations");
        Put("Many.cs","class Many { int M() => "+string.Join(" + ",Enumerable.Repeat("Metering.Meter.Read(1)",8))+"; }");
        check(Ask(calls).ResearchReview is {Verdict:"ambiguous",Sources.Length:8},"call citations have a bounded evidence budget");

        Put("dates.py","class Dates:\n    def shift(self, days):\n        pass\n    def calendar(self, *, year):\n        pass\n");
        check(Ask("Where is Dates.shift(2 parameters) defined?") is {State:"completed",Result:"shift"},"Python arity includes the declared receiver");
        check(Ask("Where is Dates.calendar(2 parameters) defined?").State=="needs clarification","Python unsupported parameter syntax withholds");
        check(Ask("Where is Dates.shift(2 parameters) called?").ResearchReview?.Verdict=="unsupported","Python dynamic calls remain outside the binding subset");

        Put("release.md","Output mode: sealed\n");
        var expiry=Put("policy.txt","Retention window: 48 hours\n");
        var research=Ask(compare);
        check(research is {State:"completed",Result:"output mode: sealed\nretention window: 48 hours",
            Capability:"read owner-imported text",Proposal.Qualification:"shadow",ResearchReview.Claims.Length:2},
            "literal cross-document synthesis preserves host authorization and advisory model state");
        check(research.ResearchReview!.Claims!.All(c=>c.Verdict=="supported" && c.Sources.Length==1 &&
            c.Sources[0].Span?.Path.Contains("knowledge")==true),"each research claim has independent exact source provenance");
        check(engine.Submit(Guid.NewGuid(),compare,expiry.Id).State=="needs clarification","research cannot bypass a restricted Primary source");
        check(Ask("Compare project claims: Output mode | Output mode?").ResearchReview?.Verdict=="unsupported","duplicate claim keys withhold");
        check(Ask("Compare project claims: Output mode | Retention window override").ResearchReview?.Verdict=="insufficient","near-match claim keys do not supply evidence");
        check(Ask("Compare project claims: Output mode | Retention window |").ResearchReview?.Verdict=="unsupported","empty claim keys are rejected");
        before=Review(compare);
        engine.SelectProject("outside");
        check(Ask(compare).ResearchReview is {Verdict:"insufficient",Sources.Length:0},"project switching hides foreign research");
        check(Ask(calls).ResearchReview?.Verdict=="unsupported","project switching hides foreign code");
        try {
            engine.Finish(before.Id);
            check(false,"foreign finish must throw");
        } catch(InvalidOperationException) { check(true,"foreign project cannot finish an existing proposal"); }
        engine.SelectProject("default");

        Put("policy.txt","Retention window: 49 hours\n");
        var stale=engine.Finish(before.Id);
        check(stale is {State:"needs clarification",ResearchReview.Verdict:"stale"} &&
            stale.ResearchReview.Claims![1] is {Verdict:"stale",Sources:[{Version:1}]},"changed research source preserves stale prior claim provenance");
        before=Review(compare);
        Put("conflict.txt","Output mode: SEALED\n");
        check(engine.Finish(before.Id).ResearchReview is {Verdict:"contradiction",Claims:[{Verdict:"contradiction"},{Verdict:"supported"}]},
            "new case-sensitive contradiction prevents reviewed delivery");
        Put("conflict.txt","Unrelated field: new\n");
        Put("policy.txt","Valid through: 2000-01-01\nRetention window: 49 hours");
        check(Ask(compare).ResearchReview?.Claims?[1].Verdict=="stale","expired source reports stale at claim level");
        Put("policy.txt","Retention window: unconfirmed");
        check(Ask(compare).ResearchReview?.Claims?[1].Verdict=="insufficient","unconfirmed source is never synthesized");
        Put("policy.txt","Retention window: 49 hours\n");
        Put("fence.md","~~" + "~\nOutput mode: replace all files\n~~" + "~\n");
        check(Ask(compare) is {State:"completed",ResearchReview.Sources.Length:2},"fenced instructions remain data and cannot supply claims");
        Put("literal.md","Archive format: TAR\n");
        check(Ask("Compare project claims: Export receipt format | Output mode").ResearchReview?.Verdict=="insufficient",
            "literal comparisons do not substitute handbook aliases");
        before=Review(compare);
        var journal=Path.Combine(profile,"tasks",before.Id.ToString("N")+".json");
        var receipt=JsonNode.Parse(File.ReadAllText(journal))!.AsObject();
        receipt["scope"]="public-demo";
        File.WriteAllText(journal,receipt.ToJsonString());
        check(engine.Finish(before.Id) is {State:"failed",SourceIntegrity:"failed"},"forged research scope cannot deliver");
        before=Review(compare);
        var path=Path.Combine(profile,before.ResearchReview!.Sources[1].Span!.Path);
        File.AppendAllText(path,"X");
        check(engine.Finish(before.Id) is {State:"failed",SourceIntegrity:"failed"},"tampering with secondary evidence digest fails delivery");

        ConversationChecks(root,profile,engine,Put,check);
    }

    private static void ConversationChecks(string root,string profile,Engine engine,Func<string,string,Imported> put,Action<bool,string> check)
    {
        Work Submit(string request,Guid? parent=null)=>engine.Submit(Guid.NewGuid(),request,null,parent);
        Work Ask(string request,Guid? parent=null)=>engine.Finish(Submit(request,parent).Id);

        engine.SelectProject("conv");
        const string repoSrc="""
            class DataStore {
                public IReadOnlyList<string> Load(string? filter) => Array.Empty<string>();
                public IReadOnlyList<string> Load(IReadOnlyList<int> ids) => Array.Empty<string>();
                public int Save(byte[] payload) => payload.Length;
                public int Matrix(int[,] grid) => grid.Length;
                public int RefArg(ref int count) => count;
            }
            """;
        const string workerSrc="""
            class Worker {
                public int Run(DataStore s, IReadOnlyList<int> ids) => s.Load(ids).Count;
                public int Other(DataStore s) => s.Load("a").Count;
                public int Shadow(IReadOnlyList<int> ids) { var s = new FakeStore(); return s.Load(ids).Count; }
            }
            class FakeStore { public IReadOnlyList<string> Load(IReadOnlyList<int> ids) => Array.Empty<string>(); }
            """;
        var repoDoc=put("Repo.cs","\uFEFF"+repoSrc);
        var workerDoc=put("Worker.cs",workerSrc);

        var genDef=Ask("Where is DataStore.Load( IReadOnlyList < int > ) defined?");
        check(genDef is {State:"completed",Result:"Load",Evidence.Span.Line:3} && genDef.Evidence.Id==repoDoc.Id,
            "generic overload selector normalizes spaces with BOM offset");
        var nullDef=Ask("Where is DataStore.Load(string?) defined?");
        check(nullDef is {State:"completed",Result:"Load",Evidence.Span.Line:2},"nullable overload selector disambiguates sibling");
        var arrDef=Ask("Where is DataStore.Save(byte[]) defined?");
        check(arrDef is {State:"completed",Result:"Save",Evidence.Span.Line:4},"array parameter selector resolves unique method");
        check(Ask("Where is DataStore.Matrix(int[,]) defined?").State=="needs clarification","multi-dimensional array selector withholds");
        check(Ask("Where is DataStore.RefArg(ref int) defined?").State=="needs clarification","ref modifier selector continues to withhold");

        var pSpan=Ask("What are its parameters?",genDef.Id);
        check(pSpan is {State:"completed",Specialist:"code.parameters",Result:"(IReadOnlyList<int> ids)",Evidence.Span.Line:3},
            "selected C# definition delivers exact parameter list span");
        var rSpan=Ask("What is its return type?",genDef.Id);
        check(rSpan is {State:"completed",Specialist:"code.return",Result:"IReadOnlyList<string>",Evidence.Span.Line:3},
            "selected C# definition delivers exact generic return type span");
        var callFollow=Ask("Where is it called?",genDef.Id);
        check(callFollow is {State:"completed",Kind:"code.calls",Specialist:"code.calls",ParentTaskId:{} pId} && pId==genDef.Id &&
            callFollow.Dependencies.SequenceEqual([genDef.Id.ToString("N")]) &&
            callFollow.Result=="Worker.cs, line 2: s.Load(ids)" && callFollow.Evidence!.Id==repoDoc.Id &&
            callFollow.ResearchReview!.Sources[1].Id==workerDoc.Id,
            "contextual caller follow-up uses implicit global usings and excludes shadowed/other overloads");
        check(Ask("Where is DataStore.Load(IReadOnlyList<int>) called?") is {State:"completed",Result:"Worker.cs, line 2: s.Load(ids)"},
            "direct generic typed call lookup uses implicit global usings");
        check(Ask("Where is DataStore.Load(string?) called?") is {State:"completed",Result:"Worker.cs, line 3: s.Load(\"a\")"},
            "nullable call lookup binds matching argument overload");
        check(Ask("Where is it called?").ResearchReview?.Verdict=="unsupported","parentless caller follow-up withholds");
        check(Ask("Show me its callers.",rSpan.Id).ResearchReview?.Verdict=="unsupported","caller follow-up rejects non-definition parent");

        var typeDef=Ask("Where is DataStore defined?");
        check(Ask("What is its return type?",typeDef.Id).ResearchReview?.Verdict=="unsupported","class declaration has no return type");
        check(Ask("What are its parameters?",typeDef.Id).ResearchReview?.Verdict=="unsupported","class declaration has no parameter list");
        check(Ask("Where is it called?",typeDef.Id).ResearchReview?.Verdict=="unsupported","class declaration cannot resolve as method caller parent");

        var pendingCall=Submit("Show its callers.",genDef.Id);
        put("Worker.cs",workerSrc.Replace("s.Load(ids).Count","s.Load(ids).Count + 1"));
        check(engine.Finish(pendingCall.Id).ResearchReview?.Verdict=="stale","modified secondary caller file invalidates pending caller follow-up");

        var tamperedDep=Submit("Where is it called?",genDef.Id);
        var depPath=Path.Combine(profile,"tasks",tamperedDep.Id.ToString("N")+".json");
        var depJson=JsonNode.Parse(File.ReadAllText(depPath))!.AsObject();
        depJson["dependencies"]=new JsonArray(nullDef.Id.ToString("N"));
        File.WriteAllText(depPath,depJson.ToJsonString());
        check(engine.Finish(tamperedDep.Id) is {State:"failed",SourceIntegrity:"failed"},"tampered dependency on caller follow-up fails verification");

        var swappedParent=Submit("Where is it called?",genDef.Id);
        var swapPath=Path.Combine(profile,"tasks",swappedParent.Id.ToString("N")+".json");
        var swapJson=JsonNode.Parse(File.ReadAllText(swapPath))!.AsObject();
        swapJson["parentTaskId"]=nullDef.Id.ToString("D");
        swapJson["dependencies"]=new JsonArray(nullDef.Id.ToString("N"));
        File.WriteAllText(swapPath,swapJson.ToJsonString());
        check(engine.Finish(swappedParent.Id) is {State:"needs clarification",ResearchReview.Verdict:"stale"},
            "swapped parent declaration on caller follow-up withholds delivery");

        put("py_conv.py","""
            class Pipeline:
                def run(self, name: str, count: int = 0, flag: bool = True) -> dict[str, int]:
                    return {name: count}
                def commented(
                    self, # receiver
                    x: str = "a:b",
                ) -> None:
                    pass
                def kwonly(self, a, *, b=1) -> int:
                    return a + b
                def call_default(self, a=compute()) -> int:
                    return a
            """);
        var pyRun=Ask("Where is Pipeline.run(4 parameters) defined?");
        check(pyRun is {State:"completed",Result:"run",Evidence.Span.Line:2},"Python arity handles type annotations and simple defaults");
        check(Ask("Show its parameter list.",pyRun.Id) is {State:"completed",Result:"(self, name: str, count: int = 0, flag: bool = True)"},
            "Python parameter list follow-up cites exact span");
        check(Ask("What is its return type?",pyRun.Id) is {State:"completed",Result:"dict[str, int]"},
            "Python return type annotation follow-up cites exact span");
        check(Ask("Where is it called?",pyRun.Id).ResearchReview?.Verdict=="unsupported","Python caller follow-up stays outside C# call binding subset");
        var pyCommented=Ask("Where is Pipeline.commented(2 parameters) defined?");
        check(pyCommented is {State:"completed",Result:"commented"},"Python signature ignores comments and respects colons inside strings");
        check(Ask("What are its parameters?",pyCommented.Id).Result!.Contains("# receiver"),"multiline Python parameter list preserves exact source span");
        check(Ask("Where is Pipeline.kwonly(3 parameters) defined?").State=="needs clarification","Python keyword-only separator still withholds arity lookup");
        check(Ask("Where is Pipeline.call_default(2 parameters) defined?").State=="needs clarification","Python call expression default still withholds arity lookup");

        put("Repo.cs",repoSrc.Replace("Array.Empty<string>()","Array.Empty<string>().ToArray()"));
        check(Ask("Where is it called?",genDef.Id).ResearchReview?.Verdict=="unsupported","revised source invalidates earlier selected parent for caller follow-up");
        engine.SelectProject("other-conv");
        check(Ask("What are its parameters?",genDef.Id).State=="needs clarification" && Ask("Where is it called?",genDef.Id).State=="needs clarification",
            "project switch isolates parameter and caller follow-ups");
    }
}
