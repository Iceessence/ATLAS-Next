using System.Diagnostics;
using System.IO;
using System.Text.Json;
using AtlasNext;

internal static class GuideChecks
{
    internal static int Probe(string scratch,string file,string symbol)
    {
        if(!Directory.Exists(scratch) || Directory.EnumerateFileSystemEntries(scratch).Any())
            throw new ArgumentException("Expected empty scratch directory.");
        using var engine=new Engine(scratch);
        var imported=engine.Import(file);
        var selected=engine.Finish(engine.Submit(Guid.NewGuid(),$"Where is {symbol} defined?").Id);
        var clock=Stopwatch.StartNew();
        var guide=engine.Guide(selected.Id);
        Console.WriteLine(JsonSerializer.Serialize(new {selected=selected.State,imported,guide,ms=clock.Elapsed.TotalMilliseconds,
            workingSetBytes=Environment.WorkingSet},Assets.Json));
        return guide==null ? 1 : 0;
    }

    internal static void Run(string root,Action<bool,string> check)
    {
        Directory.CreateDirectory(root);
        string path=Path.Combine(root,"Routes.cs");
        string text="public class Routes {\n    /// <summary>Bounded response.</summary>\n    public int Depth(\n        int n) => n;\n}";
        File.WriteAllText(path,text);
        using var engine=new Engine(Path.Combine(root,"profile"));
        var src=engine.Import(path);
        var selection=engine.Finish(engine.Submit(Guid.NewGuid(),"Where is Routes.Depth defined?").Id);
        var guide=engine.Guide(selection.Id);
        check(guide is {Definition.Text:"Depth",Signature.Text:"public int Depth(\n        int n)",Return.Text:"int"} &&
              guide.Documentation?.Text=="/// <summary>Bounded response.</summary>" &&
              guide.Signature.Citation.Id==src.Id && guide.Render().Contains(guide.Signature.Citation.Digest),
            "coding evidence preserves exact multiline signature, return, documentation and identity");
        var lone=engine.Submit(Guid.NewGuid(),"What is the archive retention policy?");
        check(engine.Guide(lone.Id)==null,"coding excerpt requires a verified declaration");
        engine.SelectProject("different");
        check(engine.Guide(selection.Id)==null,"coding evidence cannot reuse a foreign project selection");
        engine.SelectProject("default");
        File.AppendAllText(path,"\n");
        engine.Import(path);
        check(engine.Guide(selection.Id)==null,"stale definitions cannot supply drafting context");
    }
}
