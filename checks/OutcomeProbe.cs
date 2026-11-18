using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using AtlasNext;

internal static class OutcomeProbe
{
    internal record Input(string Question,string[] Documents,int? ProposedDocument=null,string[]? Extensions=null);

    internal record CodeInput(string Name,string Source,string Symbol,string Follow="What is its signature?",int Repeats=1);
    private sealed record Pair(Work[] Turns,double[] TurnMs);
    public static int Code(string root)
    {
        Console.InputEncoding=new UTF8Encoding(false);
        Console.OutputEncoding=new UTF8Encoding(false);
        var input=JsonSerializer.Deserialize<CodeInput>(Console.In.ReadToEnd(),Assets.Json)!;
        if(!Directory.Exists(root) || Directory.EnumerateFileSystemEntries(root).Any() ||
            Path.GetFileName(input.Name)!=input.Name || Path.GetExtension(input.Name) is not (".cs" or ".py"))
            throw new ArgumentException("Expected an empty test directory and a source basename.");
        string path=Path.Combine(root,input.Name);
        File.WriteAllText(path,input.Source,new UTF8Encoding(false));
        using var engine=new Engine(Path.Combine(root,"profile"));
        engine.Import(path);
        if(input.Repeats is <1 or >4) throw new ArgumentException("Expected one to four probe runs.");
        var runs=new List<Pair>();
        for(int i=0;i<input.Repeats;i++) {
            var timer=Stopwatch.StartNew();
            var first=engine.Finish(engine.Submit(Guid.NewGuid(),"Where is "+input.Symbol+" defined?").Id);
            double definitionMs=timer.Elapsed.TotalMilliseconds;
            timer.Restart();
            var second=engine.Finish(engine.Submit(Guid.NewGuid(),input.Follow,null,first.Id).Id);
            runs.Add(new([first,second],[definitionMs,timer.Elapsed.TotalMilliseconds]));
        }
        Console.WriteLine(JsonSerializer.Serialize(new {runs[0].Turns,runs[0].TurnMs,runs,
            workingSetBytes=Environment.WorkingSet},Assets.Json));
        return 0;
    }

    public static int Run()
    {
        Console.InputEncoding=new UTF8Encoding(false);
        Console.OutputEncoding=new UTF8Encoding(false);
        var input=JsonSerializer.Deserialize<Input>(Console.In.ReadToEnd(),Assets.Json)!;
        // lab/outcomes.py supplies a new private temporary directory for every case.
        string root=Environment.GetEnvironmentVariable("ATLAS_OUTCOME_WORK") ?? throw new ArgumentException("Missing outcome workspace");
        if(!Directory.Exists(root) || Directory.EnumerateFileSystemEntries(root).Any()) throw new ArgumentException("Expected empty workspace");
        using var engine=new Engine(Path.Combine(root,"profile"));
        var ids=new List<string>();
        if(input.Extensions!=null && (input.Extensions.Length!=input.Documents.Length ||
            input.Extensions.Any(e=>e is not (".md" or ".txt" or ".py" or ".cs")))) throw new ArgumentException("Invalid source extensions.");
        for(int i=0;i<input.Documents.Length;i++) {
            string path=Path.Combine(root,$"source-{i}{input.Extensions?[i] ?? ".md"}");
            File.WriteAllText(path,input.Documents[i],new UTF8Encoding(false));
            ids.Add(engine.Import(path).Id);
        }
        var timer=Stopwatch.StartNew();
        string? restriction=input.ProposedDocument switch {
            null=>null, -1=>"", int n when n>=0 && n<ids.Count=>ids[n], _=>throw new InvalidDataException("Invalid proposal")
        };
        var answer=engine.Finish(engine.Submit(Guid.NewGuid(),input.Question,restriction).Id);
        timer.Stop();
        Console.WriteLine(JsonSerializer.Serialize(new { answer,ids,elapsedUs=timer.Elapsed.TotalMicroseconds,
            workingSetBytes=Environment.WorkingSet },Assets.Json));
        return 0;
    }
}
