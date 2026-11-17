using System.IO;
using System.Text.Json;

namespace AtlasNext;

internal record TaskEvent(string State,DateTimeOffset At);
internal sealed record Work(Guid Id,string Request)
{
    public int Schema { get; init; }=1;
    public string Kind { get; init; }="knowledge.lookup";
    public string Authority { get; init; }="local desktop owner";
    public string Scope { get; init; }="public-demo";
    public string[] Dependencies { get; init; }=[];
    public string Capability { get; init; }="read pinned public catalog";
    public string Budget { get; init; }="2048 chars / 1 record / 1 MiB per asset / CPU";
    public long? InferenceMicroseconds { get; init; }
    public long? ProcessWorkingSetBytes { get; init; }
    public string Router { get; init; }="deterministic-route-v1";
    public string? Specialist { get; init; }
    public Recommendation? Proposal { get; init; }
    public string Authorization { get; init; }="pending";
    public string Verification { get; init; }="pending";
    public Citation? Evidence { get; init; }
    public string? Result { get; init; }
    public TaskEvent[] Events { get; init; }=[];
    public string State=>Events.LastOrDefault()?.State ?? "new";
    public Work At(string state)=>this with {Events=[..Events,new TaskEvent(state,DateTimeOffset.UtcNow)]};
}

internal sealed class Journal : IDisposable
{
    private readonly string directory;
    private readonly FileStream lease;
    public Journal(string profile)
    {
        directory=Path.Combine(profile,"tasks");
        Directory.CreateDirectory(directory);
        lease=new FileStream(Path.Combine(profile,"session.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        try
        {
            foreach(var task in History().Where(t=>t.State is "queued" or "review"))
                Save((task with {Result="Application closed before completion.", Verification="incomplete"}).At("interrupted"));
        }
        catch { lease.Dispose(); throw; }
    }
    private string PathFor(Guid id)=>Path.Combine(directory,id.ToString("N")+".json");
    public Work? Read(Guid id)
    {
        string path=PathFor(id);
        if(!File.Exists(path)) return null;
        var task=JsonSerializer.Deserialize<Work>(File.ReadAllBytes(path),Assets.Json);
        if(task?.Id!=id || task.Schema!=1) throw new InvalidDataException("Unexpected task identity or schema");
        return task;
    }
    public IEnumerable<Work> History()=>Directory.EnumerateFiles(directory,"*.json")
        .Select(p=>Read(Guid.ParseExact(Path.GetFileNameWithoutExtension(p),"N"))!);
    public void Save(Work task)
    {
        string path=PathFor(task.Id);
        using(var f=new FileStream(path+".pending",FileMode.Create,FileAccess.Write,FileShare.None))
        {
            JsonSerializer.Serialize(f,task,Assets.Json);
            f.Flush(true);
        }
        File.Move(path+".pending",path,true);
    }
    public void Dispose()=>lease.Dispose();
}
