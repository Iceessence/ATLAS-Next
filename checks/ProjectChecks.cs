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
        const string repoNoUsings="""
            class DataStore {
                public IReadOnlyList<string> Load(string? filter) => Array.Empty<string>();
                public IReadOnlyList<string> Load(IReadOnlyList<int> ids) => Array.Empty<string>();
                public int Save(byte[] payload) => payload.Length;
                public int Matrix(int[,] grid) => grid.Length;
                public int RefArg(ref int count) => count;
            }
            """;
        const string workerNoUsings="""
            class Worker {
                public int Run(DataStore s, IReadOnlyList<int> ids) => s.Load(ids).Count;
                public int Other(DataStore s) => s.Load("a").Count;
                public int Shadow(IReadOnlyList<int> ids) { var s = new FakeStore(); return s.Load(ids).Count; }
            }
            class FakeStore { public IReadOnlyList<string> Load(IReadOnlyList<int> ids) => Array.Empty<string>(); }
            """;
        put("Repo.cs","\uFEFF"+repoNoUsings);
        put("Worker.cs",workerNoUsings);
        var noUsingDef=Ask("Where is DataStore.Load(IReadOnlyList<int>) defined?");
        check(noUsingDef is {State:"completed",Result:"Load",Evidence.Span.Line:3},
            "generic overload declaration resolves syntactically without implicit usings");
        check(Ask("Where is it called?",noUsingDef.Id).ResearchReview?.Verdict=="unsupported" &&
            Ask("Where is DataStore.Load(IReadOnlyList<int>) called?").ResearchReview?.Verdict=="unsupported",
            "omitted using directive safely withholds cross-file generic call lookup instead of injecting implicit usings");

        const string repoSrc="using System;\nusing System.Collections.Generic;\n"+repoNoUsings;
        const string workerSrc="using System;\nusing System.Collections.Generic;\n"+workerNoUsings;
        var repoDoc=put("Repo.cs","\uFEFF"+repoSrc);
        var workerDoc=put("Worker.cs",workerSrc);

        var genDef=Ask("Where is DataStore.Load( IReadOnlyList < int > ) defined?");
        check(genDef is {State:"completed",Result:"Load",Evidence.Span.Line:5} && genDef.Evidence.Id==repoDoc.Id,
            "generic overload selector normalizes spaces with BOM offset");
        var nullDef=Ask("Where is DataStore.Load(string?) defined?");
        check(nullDef is {State:"completed",Result:"Load",Evidence.Span.Line:4},"nullable overload selector disambiguates sibling");
        var arrDef=Ask("Where is DataStore.Save(byte[]) defined?");
        check(arrDef is {State:"completed",Result:"Save",Evidence.Span.Line:6},"array parameter selector resolves unique method");
        check(Ask("Where is DataStore.Matrix(int[,]) defined?").State=="needs clarification","multi-dimensional array selector withholds");
        check(Ask("Where is DataStore.RefArg(ref int) defined?").State=="needs clarification","ref modifier selector continues to withhold");

        var pSpan=Ask("What are its parameters?",genDef.Id);
        check(pSpan is {State:"completed",Specialist:"code.parameters",Result:"(IReadOnlyList<int> ids)",Evidence.Span.Line:5},
            "selected C# definition delivers exact parameter list span");
        var rSpan=Ask("What is its return type?",genDef.Id);
        check(rSpan is {State:"completed",Specialist:"code.return",Result:"IReadOnlyList<string>",Evidence.Span.Line:5},
            "selected C# definition delivers exact generic return type span");
        var callFollow=Ask("Where is it called?",genDef.Id);
        check(callFollow is {State:"completed",Kind:"code.calls",Specialist:"code.calls",ParentTaskId:{} pId} && pId==genDef.Id &&
            callFollow.Dependencies.SequenceEqual([genDef.Id.ToString("N")]) &&
            callFollow.Result=="Worker.cs, line 4: s.Load(ids)" && callFollow.Evidence!.Id==repoDoc.Id &&
            callFollow.ResearchReview!.Sources[1].Id==workerDoc.Id,
            "contextual caller follow-up uses explicit source usings and excludes shadowed/other overloads");
        check(Ask("Where is DataStore.Load(IReadOnlyList<int>) called?") is {State:"completed",Result:"Worker.cs, line 4: s.Load(ids)"},
            "direct generic typed call lookup uses explicit source usings");
        check(Ask("Where is DataStore.Load(string?) called?") is {State:"completed",Result:"Worker.cs, line 5: s.Load(\"a\")"},
            "nullable call lookup binds matching argument overload");
        check(Ask("Where is it called?").ResearchReview?.Verdict=="unsupported","parentless caller follow-up withholds");
        check(Ask("Show me its callers.",rSpan.Id).ResearchReview?.Verdict=="unsupported","caller follow-up rejects non-definition parent");

        engine.SelectProject("conv-ext");
        const string extTarget="""
            using System.Collections.Generic;
            namespace App {
                public static class Target {
                    public static void Process(int x) {}
                    public static void Process(string s) {}
                }
                public static class CustomSeq {
                    public static string First(this IEnumerable<object> xs) => "ok";
                }
            }
            """;
        const string extCallerDisabled="""
            using App;
            class ExtRunner {
                public void Run() {
                    var v = new[] { 1 }.First();
                    Target.Process(v);
                }
            }
            """;
        put("ExtTarget.cs",extTarget);
        put("ExtCaller.cs",extCallerDisabled);
        var procStrDef=Ask("Where is Target.Process(string) defined?");
        var procIntDef=Ask("Where is Target.Process(int) defined?");
        check(Ask("Where is Target.Process(string) called?") is {State:"completed",Result:"ExtCaller.cs, line 5: Target.Process(v)"} &&
            Ask("Where is it called?",procStrDef.Id) is {State:"completed",Result:"ExtCaller.cs, line 5: Target.Process(v)"} &&
            Ask("Where is Target.Process(int) called?").ResearchReview?.Verdict=="unsupported" &&
            Ask("Where is it called?",procIntDef.Id).ResearchReview?.Verdict=="unsupported",
            "competing extension method without System.Linq binds string overload for direct and selected callers");
        put("ExtCaller.cs","using System.Linq;\n"+extCallerDisabled);
        check(Ask("Where is Target.Process(int) called?") is {State:"completed",Result:"ExtCaller.cs, line 6: Target.Process(v)"} &&
            Ask("Where is it called?",procIntDef.Id) is {State:"completed",Result:"ExtCaller.cs, line 6: Target.Process(v)"} &&
            Ask("Where is Target.Process(string) called?").ResearchReview?.Verdict=="unsupported" &&
            Ask("Where is it called?",procStrDef.Id).ResearchReview?.Verdict=="unsupported",
            "explicit System.Linq import flips competing extension method to int overload for direct and selected callers");

        engine.SelectProject("conv");

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
                def lam(self, x: int) -> lambda a: a + 1:
                    return lambda a: a + 1
                def cont(self, x: int) -> \
                    dict[str, int] \
                    :
                    return {}
                def bad_order(self, a: int = 1, b: int):
                    pass
                def bad_kw(self, class: int = 1):
                    pass
                def bad_kw2(self, for):
                    pass
                def return(self):
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
        var pyLam=Ask("Where is Pipeline.lam(2 parameters) defined?");
        check(pyLam is {State:"completed",Result:"lam",Evidence.Span.Line:9} &&
            Ask("What are its parameters?",pyLam.Id).Result=="(self, x: int)" &&
            Ask("What is its return type?",pyLam.Id).Result=="lambda a: a + 1",
            "Python return annotation containing top-level lambda colon keeps exact span");
        var pyCont=Ask("Where is Pipeline.cont(2 parameters) defined?");
        check(pyCont is {State:"completed",Result:"cont",Evidence.Span.Line:11} &&
            Ask("What is its return type?",pyCont.Id).Result=="dict[str, int]",
            "Python return annotation excludes leading and trailing line continuations");
        foreach(var badReq in new[]{"Where is Pipeline.bad_order(3 parameters) defined?","Where is Pipeline.bad_order defined?",
            "Where is Pipeline.bad_kw(2 parameters) defined?","Where is Pipeline.bad_kw defined?",
            "Where is Pipeline.bad_kw2(2 parameters) defined?","Where is Pipeline.bad_kw2 defined?",
            "Where is Pipeline.return defined?"})
            check(Ask(badReq).State=="needs clarification",$"malformed Python header withheld for {badReq}");
        var rejectedDecl=Ask("Where is Pipeline.bad_order defined?");
        check(rejectedDecl.State=="needs clarification" &&
            Ask("What are its parameters?",rejectedDecl.Id).State=="needs clarification" &&
            Ask("What is its return type?",rejectedDecl.Id).State=="needs clarification",
            "malformed Python declaration cannot authorize a selected follow-up");
        check(Ask("Where is Pipeline.kwonly(3 parameters) defined?").State=="needs clarification","Python keyword-only separator still withholds arity lookup");
        check(Ask("Where is Pipeline.call_default(2 parameters) defined?").State=="needs clarification","Python call expression default still withholds arity lookup");

        put("py_valid_grammar.py","""
            class Derived(Base, metaclass=Meta):
                def nums(self, a=0, b=00, c=0_0, d=0x1f_aB, e=0x_1, f=0b10_10, g=0o7_7, h=1.5, i=.5, j=1., k=01.5, l=1e-3, m=1E+2, n=1j, o=-1_000, p=+0.5):
                    pass
                def ops(self, x: int | str | None = +-~1, y: (int if True else str) = (2 ** -3), z: bool = 0 < 1 <= 2) -> dict[str, tuple[int, ...]]:
                    return {}
                def walrus_comp(self, x: (T := int) = (v := 1), ys: list[int] = [i for i in range(3) if i], s: "A" "B" = "x" "y") -> Callable[[int, str], bool]:
                    return lambda a, b: True
                def lam_ann(self, fn: lambda a=1: a = 2) -> lambda a, b=1: a + b:
                    return lambda a, b=1: a + b
                def cont_inside(self, x: int) -> int | \
                    str:
                    return x
                def cont_around(self, x: int) -> \
                    tuple[int, str] \
                    :
                    return (x, "")
            """);
        check(Ask("Where is Derived defined?") is {State:"completed",Result:"Derived",Evidence.Span.Line:1},
            "valid Python class header with keyword metaclass resolves");
        var pyNums=Ask("Where is Derived.nums(17 parameters) defined?");
        check(pyNums is {State:"completed",Result:"nums",Evidence.Span.Line:2},
            "valid Python numeric literal defaults resolve with exact arity");
        var pyOps=Ask("Where is Derived.ops defined?");
        check(pyOps is {State:"completed",Result:"ops"} &&
            Ask("What are its parameters?",pyOps.Id).Result=="(self, x: int | str | None = +-~1, y: (int if True else str) = (2 ** -3), z: bool = 0 < 1 <= 2)" &&
            Ask("What is its return type?",pyOps.Id).Result=="dict[str, tuple[int, ...]]" &&
            Ask("Where is Derived.ops(4 parameters) defined?").State=="needs clarification",
            "valid Python operators, unary/power, comparisons, and ternary expressions resolve while non-literal defaults withhold arity");
        var pyWalrus=Ask("Where is Derived.walrus_comp defined?");
        check(pyWalrus is {State:"completed",Result:"walrus_comp"} &&
            Ask("What is its return type?",pyWalrus.Id).Result=="Callable[[int, str], bool]",
            "valid Python parenthesized walrus, comprehension, and string concat resolve");
        var pyLamAnn=Ask("Where is Derived.lam_ann(2 parameters) defined?");
        check(pyLamAnn is {State:"completed",Result:"lam_ann"} &&
            Ask("What are its parameters?",pyLamAnn.Id).Result=="(self, fn: lambda a=1: a = 2)" &&
            Ask("What is its return type?",pyLamAnn.Id).Result=="lambda a, b=1: a + b",
            "valid Python lambda in parameter annotation with default and return annotation resolves");
        var pyContIn=Ask("Where is Derived.cont_inside defined?");
        check(pyContIn is {State:"completed",Result:"cont_inside"} &&
            Ask("What is its return type?",pyContIn.Id).Result?.Replace("\r\n","\n")=="int | \\\n        str",
            "Python explicit continuation inside return annotation preserves exact span");
        var pyContAround=Ask("Where is Derived.cont_around defined?");
        check(pyContAround is {State:"completed",Result:"cont_around"} &&
            Ask("What is its return type?",pyContAround.Id).Result=="tuple[int, str]",
            "Python explicit continuations before and after return annotation exclude wrapper trivia");

        bool WithholdsAll(string symbol,int? arity=null) {
            foreach(var q in arity is {} n
                ? new[]{$"Where is {symbol}({n} parameters) defined?",$"Where is {symbol} defined?"}
                : new[]{$"Where is {symbol} defined?"}) {
                var def=Ask(q);
                if(def.State!="needs clarification") return false;
                if(Ask("What are its parameters?",def.Id).State!="needs clarification") return false;
                if(Ask("What is its return type?",def.Id).State!="needs clarification") return false;
            }
            return true;
        }

        put("py_bad_num.py","""
            class BadNum:
                def ok(self, x: int = 1) -> int:
                    return x
                def num_lead_def(self, x=01): pass
                def num_lead_pann(self, x: 0_1): pass
                def num_lead_ret(self, x) -> 01: pass
                def num_alpha_def(self, x=123abc): pass
                def num_alpha_pann(self, x: 123abc): pass
                def num_alpha_ret(self, x) -> 123abc: pass
                def num_dots_def(self, x=1.2.3): pass
                def num_dots_pann(self, x: 1.2.3): pass
                def num_dots_ret(self, x) -> 1..2: pass
                def num_us_dbl(self, x=1__2): pass
                def num_us_trail(self, x=1_): pass
                def num_hex_bad(self, x: 0xGG = 1): pass
                def num_bin_bad(self, x=0b2): pass
                def num_oct_bad(self, x) -> 0o8: pass
                def num_exp_bad(self, x=1e++2): pass
                def num_real_attr(self, x=1.real): pass
                def num_imag_tail(self, x=1j2): pass
            class BadNumClass(123abc):
                pass
            """);
        check(Ask("Where is BadNum.ok(2 parameters) defined?") is {State:"completed",Result:"ok"},
            "valid sibling method remains queryable alongside malformed numeric siblings");
        check(new[]{
            ("BadNum.num_lead_def",2),("BadNum.num_lead_pann",2),("BadNum.num_lead_ret",2),
            ("BadNum.num_alpha_def",2),("BadNum.num_alpha_pann",2),("BadNum.num_alpha_ret",2),
            ("BadNum.num_dots_def",2),("BadNum.num_dots_pann",2),("BadNum.num_dots_ret",2),
            ("BadNum.num_us_dbl",2),("BadNum.num_us_trail",2),("BadNum.num_hex_bad",2),
            ("BadNum.num_bin_bad",2),("BadNum.num_oct_bad",2),("BadNum.num_exp_bad",2),
            ("BadNum.num_real_attr",2),("BadNum.num_imag_tail",2)
        }.All(t=>WithholdsAll(t.Item1,t.Item2)) && WithholdsAll("BadNumClass"),
            "malformed numeric literals in defaults, parameter/return annotations, and class headers withhold definitions and follow-ups");

        put("py_bad_op.py","""
            class BadOp:
                def ok(self, x: int | str) -> int:
                    return 0
                def op_aug_def(self, x=a += 1): pass
                def op_aug_pann(self, x: int |= str): pass
                def op_aug_ret(self, x) -> int &= str: pass
                def op_star2eq_def(self, x=a **= 2): pass
                def op_tilde_bin_def(self, x=1 ~ 2): pass
                def op_tilde_bin_ret(self, x) -> int ~ str: pass
                def op_tilde_eq(self, x=1 ~= 2): pass
                def op_amp_amp_def(self, x=a && b): pass
                def op_pipe_pipe_pann(self, x: int || str): pass
                def op_pipe_gap_ret(self, x) -> int | | str: pass
                def op_pipe_trail_ret(self, x) -> int |: pass
                def op_bang_def(self, x=!a): pass
                def op_eq3_def(self, x=a === b): pass
                def op_diamond(self, x=a <> b): pass
                def op_star_sp(self, x=a * * b): pass
                def op_star2_star(self, x=a ** * b): pass
                def op_arrow_pann(self, x: int -> str): pass
                def op_not_unary_rhs(self, x=-not a): pass
                def op_not_bin(self, x=a not b): pass
                def op_is_not_in(self, x=a is not in b): pass
            class BadOpClass(Base += Extra):
                pass
            """);
        check(Ask("Where is BadOp.ok(2 parameters) defined?") is {State:"completed",Result:"ok"},
            "valid sibling method remains queryable alongside illegal operator siblings");
        check(new[]{
            ("BadOp.op_aug_def",2),("BadOp.op_aug_pann",2),("BadOp.op_aug_ret",2),
            ("BadOp.op_star2eq_def",2),("BadOp.op_tilde_bin_def",2),("BadOp.op_tilde_bin_ret",2),
            ("BadOp.op_tilde_eq",2),("BadOp.op_amp_amp_def",2),("BadOp.op_pipe_pipe_pann",2),
            ("BadOp.op_pipe_gap_ret",2),("BadOp.op_pipe_trail_ret",2),("BadOp.op_bang_def",2),
            ("BadOp.op_eq3_def",2),("BadOp.op_diamond",2),("BadOp.op_star_sp",2),
            ("BadOp.op_star2_star",2),("BadOp.op_arrow_pann",2),("BadOp.op_not_unary_rhs",2),
            ("BadOp.op_not_bin",2),("BadOp.op_is_not_in",2)
        }.All(t=>WithholdsAll(t.Item1,t.Item2)) && WithholdsAll("BadOpClass"),
            "illegal operators and operator sequences in defaults, parameter/return annotations, and class headers withhold definitions and follow-ups");

        put("py_bad_struct.py","""
            class BadStruct:
                def ok(self, x: dict[str, int]) -> list[str]:
                    return []
                def bad_tern_def(self, x=a if b): pass
                def bad_tern_ret(self, x) -> a if b: pass
                def bad_else_only(self, x=a else b): pass
                def bad_walrus_def(self, x=a := 1): pass
                def bad_walrus_pann(self, x: a := 1): pass
                def bad_walrus_ret(self, x) -> a := 1: pass
                def bad_lam_order(self, x=lambda a=1, b: a): pass
                def bad_lam_kw(self, x=lambda class: 1): pass
                def bad_lam_dup(self, x=lambda a, a: a): pass
                def bad_call_pos_kw(self, x=g(a=1, 2)): pass
                def bad_call_dup_kw(self, x=g(a=1, a=2)): pass
                def bad_dict_mix(self, x={1: 2, 3}): pass
                def bad_sub_empty(self, x) -> dict[]: pass
                def bad_sub_slice4(self, x=a[1:2:3:4]): pass
                def bad_star_paren(self, x) -> (*Ts): pass
                def bad_comp_lit(self, x=[i for 1 in xs]): pass
                def bad_walrus_comp(self, x=[(i := 1) for i in xs]): pass
                def bad_bytes_utf(self, x=b"é"): pass
                def bad_mix_bytes(self, x="a" b"b"): pass
                def bad_ur(self, x=ur"a"): pass
                def bad_fstr_expr(self, x=f"{1+}"): pass
                def bad_attr_kw(self, x=a.None): pass
                def bad_param_star_ann(self, x: *int): pass
                def bad_param_star_def(self, x, *a=()): pass
                def bad_param_slash_star(self, x, *a, /): pass
            class BadClsOrder(a=1, Base):
                pass
            class BadClsDup(a=1, a=2):
                pass
            """);
        check(Ask("Where is BadStruct.ok(2 parameters) defined?") is {State:"completed",Result:"ok"},
            "valid sibling method remains queryable alongside malformed structural siblings");
        check(new[]{
            ("BadStruct.bad_tern_def",2),("BadStruct.bad_tern_ret",2),("BadStruct.bad_else_only",2),
            ("BadStruct.bad_walrus_def",2),("BadStruct.bad_walrus_pann",2),("BadStruct.bad_walrus_ret",2),
            ("BadStruct.bad_lam_order",2),("BadStruct.bad_lam_kw",2),("BadStruct.bad_lam_dup",2),
            ("BadStruct.bad_call_pos_kw",2),("BadStruct.bad_call_dup_kw",2),("BadStruct.bad_dict_mix",2),
            ("BadStruct.bad_sub_empty",2),("BadStruct.bad_sub_slice4",2),("BadStruct.bad_star_paren",2),
            ("BadStruct.bad_comp_lit",2),("BadStruct.bad_walrus_comp",2),("BadStruct.bad_bytes_utf",2),("BadStruct.bad_mix_bytes",2),
            ("BadStruct.bad_ur",2),("BadStruct.bad_fstr_expr",2),("BadStruct.bad_attr_kw",2),
            ("BadStruct.bad_param_star_ann",2),("BadStruct.bad_param_star_def",3),("BadStruct.bad_param_slash_star",3)
        }.All(t=>WithholdsAll(t.Item1,t.Item2)) && WithholdsAll("BadClsOrder") && WithholdsAll("BadClsDup"),
            "invalid Python expression, parameter, lambda, container, string, and class structures withhold definitions and follow-ups");

        put("Repo.cs",repoSrc.Replace("Array.Empty<string>()","Array.Empty<string>().ToArray()"));
        check(Ask("Where is it called?",genDef.Id).ResearchReview?.Verdict=="unsupported","revised source invalidates earlier selected parent for caller follow-up");
        engine.SelectProject("other-conv");
        check(Ask("What are its parameters?",genDef.Id).State=="needs clarification" && Ask("Where is it called?",genDef.Id).State=="needs clarification",
            "project switch isolates parameter and caller follow-ups");
    }
}
