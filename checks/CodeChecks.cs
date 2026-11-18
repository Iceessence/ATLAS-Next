using System.IO;
using System.Text;
using AtlasNext;

internal static class CodeChecks
{
    public static void Depth(string root,Action<bool,string> check)
    {
        Directory.CreateDirectory(root);
        string profile=Path.Combine(root,"profile");
        using var engine=new Engine(profile);
        engine.SelectProject("depth");
        void Import(string name,string source) {
            string path=Path.Combine(root,name);
            File.WriteAllText(path,source,new UTF8Encoding(true));
            engine.Import(path);
        }
        Work Ask(string request,Guid? parent=null,string? restrict=null)=>
            engine.Finish(engine.Submit(Guid.NewGuid(),request,restrict,parent).Id);
        void Exact(Work work,string result,int line,string name) {
            var cite=work.Evidence;
            string bytes=cite?.Span is {} s ? Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(profile,s.Path))) : "";
            check(work is {State:"completed",AnswerRelevance:"supported",SourceIntegrity:"passed"} &&
                cite?.Span is {} span && span.Line==line && work.Result==result && bytes.Substring(span.Start,span.Length)==result,name);
        }
        Import("meters.cs","// 🔧\r\nclass First {\r\n    /// <summary>Alpha.</summary>\r\n    /// <remarks>More.</remarks>\r\n"+
            "    [System.Obsolete]\r\n    public void Scan() { void Local() {} }\r\n}\r\n"+
            "struct Second {\r\n    public void Scan() {}\r\n}\r\nclass Multiple { void Scan() {} void Scan(int n) {} }\r\n"+
            "class Detached {\r\n    /// <summary>Detached.</summary>\r\n\r\n    void Probe() {}\r\n}\r\n");
        var ambiguous=Ask("Locate Scan");
        check(ambiguous is {State:"needs clarification",ResearchReview.Verdict:"ambiguous",ResearchReview.Sources.Length:4} &&
              ambiguous.Result!.Contains("First.Scan") && ambiguous.Result.Contains("Second.Scan"),"bare repeats report qualified source locations");
        var first=Ask("Where is First.Scan defined?");
        Exact(first,"Scan",6,"unique qualified Roslyn method and UTF-16 identifier");
        Exact(Ask("Show its documentation.",first.Id),"/// <summary>Alpha.</summary>\r\n    /// <remarks>More.</remarks>",3,"XML documentation preserves source markup and CRLF");
        Exact(Ask("What is its summary?",first.Id),"<summary>Alpha.</summary>",3,"XML summary excludes remarks");
        Exact(Ask("What is its enclosing type?",first.Id),"First",2,"C# enclosing type has independent source span");
        var second=Ask("Locate Second.Scan");
        Exact(Ask("What is its enclosing type?",second.Id),"Second",8,"struct type context");
        check(Ask("What is its enclosing class?",second.Id).State=="needs clarification","struct cannot verify a class request");
        check(Ask("Show its docstring.",first.Id).State=="needs clarification","language-specific docstring requires Python");
        check(Ask("Locate Multiple.Scan") is {State:"needs clarification",ResearchReview.Verdict:"ambiguous"},"qualified overloads still ambiguous");
        check(Ask("Locate First.Local").State=="needs clarification","C# local function is not a member of its containing type");
        var detached=Ask("Locate Detached.Probe");
        check(Ask("Show its documentation.",detached.Id).State=="needs clarification","blank line detaches C# documentation");
        var denied=Ask("Locate First.Scan",restrict:"not-an-eligible-source");
        check(denied is {State:"needs clarification",Evidence:null,ResearchReview.Verdict:"supported"},"qualification cannot widen Primary authority");
        Import("reader.py","# 📚\r\nclass Reader:\r\n    def read(\r\n        self):\r\n        # attachment\r\n        r\"\"\"Read\r\n        safely.\"\"\"\r\n"+
            "        def helper(): return 1\r\n        return helper()\r\nclass Writer:\r\n\tdef read(self): 'Other.'; return 2\r\n"+
            "\tdef late(self):\r\n\t\tx = 1\r\n\t\t'Later literal'\r\ndef free(): pass\r\n");
        var reader=Ask("Locate Reader.read");
        Exact(reader,"read",3,"qualified Python multiline method");
        Exact(Ask("Show its docstring.",reader.Id),"r\"\"\"Read\r\n        safely.\"\"\"",6,"raw Python docstring keeps delimiters and source offsets");
        Exact(Ask("What is its enclosing class?",reader.Id),"Reader",2,"Python enclosing class");
        var writer=Ask("Locate Writer.read");
        Exact(Ask("Show its documentation.",writer.Id),"'Other.'",11,"inline Python docstring in tab-indented class");
        check(Ask("Locate Reader.helper").State=="needs clarification" && Ask("Locate Writer.free").State=="needs clarification","Python nested function and dedent preserve ownership");
        check(Ask("Show its documentation.",Ask("Locate Writer.late").Id).State=="needs clarification","later Python string expression cannot serve as docstring");
        check(Ask("Show its XML documentation.",reader.Id).State=="needs clarification","language-specific XML requires C#");
        check(Ask("What is its enclosing type?",Ask("Locate free").Id).State=="needs clarification","module function has no enclosing class");
        foreach(var request in new[]{"Locate reader.read","Locate Reader.read outside this project","Locate Outer.Reader.read"})
            check(Ask(request).State=="needs clarification","unsupported qualified request: "+request);
        var pending=engine.Submit(Guid.NewGuid(),"Show its documentation.",null,reader.Id);
        var wrong=Ask("Show its documentation.",writer.Id);
        string taskPath=Path.Combine(profile,"tasks",pending.Id.ToString("N")+".json");
        File.WriteAllText(taskPath,System.Text.Json.JsonSerializer.Serialize(pending with {Evidence=wrong.Evidence},Assets.Json));
        check(engine.Finish(pending.Id).State=="needs clarification","authentic same-file documentation substitution withheld");
        var sameContainer=engine.Submit(Guid.NewGuid(),"What is its enclosing type?",null,writer.Id);
        var other=Ask("Locate Writer.late");
        File.WriteAllText(Path.Combine(profile,"tasks",sameContainer.Id.ToString("N")+".json"),
            System.Text.Json.JsonSerializer.Serialize(sameContainer with {ParentTaskId=other.Id},Assets.Json));
        check(engine.Finish(sameContainer.Id).State=="needs clarification","changed parent refused even with identical container span");
        var delayed=engine.Submit(Guid.NewGuid(),"Show its documentation.",null,reader.Id);
        Import("reader.py","class Reader:\n    def read(self): 'Revised.'\n");
        check(engine.Finish(delayed.Id).State=="failed" && Ask("Show its documentation.",reader.Id).State=="needs clarification","new revision invalidates pending and selected documentation");
        Exact(Ask("Show its documentation.",Ask("Locate Reader.read").Id),"'Revised.'",2,"fresh parent obtains new documentation");
        Import("also.cs","namespace Other { class First { public void Scan() {} } }");
        check(Ask("Locate First.Scan") is {State:"needs clarification",ResearchReview.Verdict:"ambiguous",ResearchReview.Sources.Length:2},
            "qualified duplicate owner and member in different files stays ambiguous");
        check(Ask("Show its documentation.",first.Id) is {State:"needs clarification",ResearchReview.Verdict:"ambiguous"},
            "duplicate definition with no documentation still invalidates unique documentation selection");
        Import("many.cs",string.Join("\n",Enumerable.Range(0,12).Select(n=>$"class Channel{n} {{ void Poll() {{}} }}")));
        var capped=Ask("Locate Poll");
        check(capped is {State:"needs clarification",Evidence:null,ResearchReview.Sources.Length:8} &&
            capped.Result!.Contains("Additional matches omitted"),"ambiguity lists at most eight cited locations");
        engine.SelectProject("elsewhere");
        check(Ask("Show its documentation.",first.Id).State=="needs clarification","foreign project parent cannot authorize documentation");
    }

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
