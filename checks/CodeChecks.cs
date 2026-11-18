using System.IO;
using System.Text;
using AtlasNext;

internal static class CodeChecks
{
    public static void Run(string root,Action<bool,string> check)
    {
        Directory.CreateDirectory(root);
        string profile=Path.Combine(root,"profile"),py=Path.Combine(root,"Sample.py"),cs=Path.Combine(root,"Meter.cs");
        const string source="# 🧪\r\nsample = '''\r\ndef Ghost():\r\n    pass\r\n'''\r\n"+
            "class Lens:\r\n    def focus(\r\n        self, aperture: str = ':)'\r\n    ):\r\n        return aperture\r\n";
        File.WriteAllText(py,source,new UTF8Encoding(true));
        const string csharp="// 😀\r\nclass Meter {\r\n /* public void Ghost() {} */\r\n [System.Obsolete]\r\n"+
            " public static int Read(\r\n     int channel) => channel;\r\n}";
        File.WriteAllText(cs,csharp);
        Guid parent;
        using(var engine=new Engine(profile)) {
            engine.SelectProject("lab");
            var doc=engine.Import(py);
            var meter=engine.Import(cs);
            Work Ask(string prompt,Guid? previous=null,string? restrict=null)=>
                engine.Finish(engine.Submit(Guid.NewGuid(),prompt,restrict,previous).Id);
            var first=Ask("Where is focus defined?");
            parent=first.Id;
            check(first is {State:"completed",Result:"focus",Evidence.Span.Line:7} && first.Evidence.Id==doc.Id &&
                ("\uFEFF"+source).Substring(first.Evidence.Span.Start,first.Evidence.Span.Length)=="focus","Python name span includes BOM, CRLF, astral prefix");
            var second=Ask("What is its signature?",parent);
            check(second is {State:"completed",Specialist:"code.signature",Evidence.Span.Line:7} && second.ParentTaskId==parent &&
                second.Result=="def focus(\r\n        self, aperture: str = ':)'\r\n    ):" &&
                ("\uFEFF"+source).Substring(second.Evidence.Span.Start,second.Evidence.Span.Length)==second.Result,
                "selected Python method supplies exact multiline signature");
            var method=Ask("Locate Read.");
            check(method is {State:"completed",Result:"Read",Evidence.Span.Line:5} && method.Evidence.Id==meter.Id,
                "C# syntax tree locates method exactly");
            var signature=Ask("Show me its signature.",method.Id);
            check(signature is {State:"completed",Evidence.Span.Line:5} && signature.Result=="public static int Read(\r\n     int channel)" &&
                csharp.Substring(signature.Evidence.Span.Start,signature.Evidence.Span.Length)==signature.Result,"C# header excludes attributes, body and comments");
            foreach(var query in new[]{"Where is Ghost defined?","Locate read","Where is Read defined in production?",
                "Where is the class Read defined?","What is its signature?"})
                check(Ask(query) is {State:"needs clarification",Evidence:null},$"withhold unsupported code request {query}");
            check(Ask("Locate Lens") is {State:"completed",Evidence.Span.Line:6},"Python class declaration lookup");
            check(Ask("Locate Meter") is {State:"completed",Evidence.Span.Line:2},"C# class declaration lookup");
            check(Ask("What is its signature?",signature.Id).State=="needs clarification","only completed definitions anchor follow-ups");
            check(Ask("Locate Read",restrict:doc.Id) is {State:"needs clarification",ResearchReview.Verdict:"supported"},
                "Research cannot bypass restricted code retrieval");
            var text=Path.Combine(root,"claims.md");
            File.WriteAllText(text,"Sensor package mode: beta\nSensor mode: alpha\nLocal switch: red\nLocal switch set to: blue");
            engine.Import(text);
            check(Ask("What is the sensor mode set to, please?").Result=="alpha","fuller exact subject survives search-term collision");
            check(Ask("What is the local switch set to, please?") is {State:"needs clarification",ResearchReview.Verdict:"ambiguous"},
                "courtesy suffix retains literal ambiguity");
            check(Ask("What is the focus?") is {State:"needs clarification",Evidence:null},"source declarations never become text claims");
            var foreign=engine.Submit(Guid.NewGuid(),"What is its signature?",null,method.Id);
            string journal=Path.Combine(profile,"tasks",foreign.Id.ToString("N")+".json");
            File.WriteAllText(journal,System.Text.Json.JsonSerializer.Serialize(foreign with {ParentTaskId=first.Id},Assets.Json));
            check(engine.Finish(foreign.Id).State=="needs clarification","authentic signature withheld after selected parent substitution");
            var pending=engine.Submit(Guid.NewGuid(),"What is its signature?",null,parent);
            string dup=Path.Combine(root,"Other.py");
            File.WriteAllText(dup,"def focus(other): pass\n");
            engine.Import(dup);
            check(engine.Finish(pending.Id) is {State:"needs clarification",ResearchReview.Verdict:"ambiguous"},
                "delivery rechecks definitions added after submission");
            check(Ask("Locate focus",restrict:doc.Id) is {State:"needs clarification",ResearchReview.Verdict:"ambiguous"},
                "narrowed Primary cannot hide duplicate definitions");
            File.WriteAllText(dup,"# harmless\n");
            engine.Import(dup);
            var before=engine.Submit(Guid.NewGuid(),"What is its signature?",null,parent);
            File.WriteAllText(py,"def focus(power): pass\n");
            check(engine.Import(py).Version==2,"new immutable source revision");
            check(engine.Finish(before.Id).State=="failed" && Ask("What is its signature?",parent).State=="needs clarification",
                "pending and new follow-ups both refuse superseded parent");
            var fresh=Ask("Locate focus");
            parent=fresh.Id;
            check(Ask("What is its signature?",parent).Result=="def focus(power):","fresh lookup uses changed revision");
            engine.SelectProject("other");
            check(Ask("What is its signature?",parent).State=="needs clarification" && Ask("Locate focus").State=="needs clarification",
                "code context isolated across projects");
            engine.SelectProject("lab");
            File.WriteAllText(py,"def bad():\n    text = '''unfinished");
            try {engine.Import(py); throw new Exception("Accepted malformed lexical source");}
            catch(InvalidDataException) {check(Ask("Locate focus").Evidence?.Version==2,"invalid import leaves the healthy revision active");}
            File.WriteAllText(cs,"class First { void Scan() {} }\nclass Second { int Scan(int n)=>n; }");
            engine.Import(cs);
            check(Ask("Locate Scan") is {State:"needs clarification",ResearchReview.Verdict:"ambiguous",ResearchReview.Sources.Length:2},
                "same method name in two types requires clarification");
        }
        using var reopened=new Engine(profile);
        reopened.SelectProject("lab");
        check(reopened.Finish(reopened.Submit(Guid.NewGuid(),"What is its signature?",null,parent).Id).Result=="def focus(power):",
            "explicitly selected completed definition survives restart");
    }
}
