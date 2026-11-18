using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using AtlasNext;

internal static class OutcomeProbe
{
    internal record Input(string Question,string[] Documents,int? ProposedDocument=null);

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
        for(int i=0;i<input.Documents.Length;i++) {
            string path=Path.Combine(root,$"source-{i}.md");
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
