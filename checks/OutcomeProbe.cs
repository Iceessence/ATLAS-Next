using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using AtlasNext;

internal static class OutcomeProbe
{
    internal record Input(string Question,string[] Documents);

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
        var answer=engine.Finish(engine.Submit(Guid.NewGuid(),input.Question).Id);
        timer.Stop();
        Console.WriteLine(JsonSerializer.Serialize(new { answer,ids,elapsedUs=timer.Elapsed.TotalMicroseconds,
            workingSetBytes=Environment.WorkingSet },Assets.Json));
        return 0;
    }
}
