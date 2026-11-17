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
    public string SourceIntegrity { get; init; }="not checked";
    public string AnswerRelevance { get; init; }="not checked";
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
    public string RecoveryNotice { get; }
    public Journal(string profile)
    {
        directory=Path.Combine(profile,"tasks");
        Directory.CreateDirectory(directory);
        lease=new FileStream(Path.Combine(profile,"session.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        try
        {
            RecoveryNotice=Recover(profile);
        }
        catch { lease.Dispose(); throw; }
    }
    private string Recover(string profile)
    {
        var interrupted=new List<Work>();
        var rejected=new List<(string Path,string Reason)>();
        // Finish classification before moving anything or replacing a committed record.
        // I/O errors propagate here, so an unreadable healthy record is never discarded.
        foreach(var path in Directory.GetFiles(directory).Order(StringComparer.OrdinalIgnoreCase))
        {
            if(path.EndsWith(".json.pending",StringComparison.OrdinalIgnoreCase))
                rejected.Add((path,"uncommitted"));
            else if(path.EndsWith(".json",StringComparison.OrdinalIgnoreCase))
            {
                if(!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path),"N",out var id))
                {
                    rejected.Add((path,"filename"));
                    continue;
                }
                try
                {
                    var task=Load(path,id);
                    if(task.State is "queued" or "review") interrupted.Add(task);
                }
                catch(Exception ex) when(ex is JsonException or InvalidDataException)
                {
                    rejected.Add((path,ex is JsonException ? "json" : "invalid-record"));
                }
            }
        }
        string quarantine=Path.Combine(profile,"quarantine");
        var batch=Path.Combine(quarantine,Guid.NewGuid().ToString("N"));
        foreach(var file in rejected)
        {
            string dest=Path.Combine(batch,file.Reason);
            Directory.CreateDirectory(dest);
            File.Move(file.Path,Path.Combine(dest,Path.GetFileName(file.Path)));
        }
        foreach(var task in interrupted)
            Save((task with {Result="Application closed before completion.", Verification="incomplete"}).At("interrupted"));
        int total=Directory.Exists(quarantine) ? Directory.GetFiles(quarantine,"*",SearchOption.AllDirectories).Length : 0;
        return total==0 && interrupted.Count==0 ? "" :
            $"Recovered {interrupted.Count} interrupted tasks. Quarantined {rejected.Count} files this startup. {total} preserved in {quarantine}.";
    }

    private static Work Load(string path,Guid id)
    {
        using var stream=File.OpenRead(path);
        if(stream.Length>1024*1024) throw new InvalidDataException("Oversized task record");
        using var document=JsonDocument.Parse(stream);
        var root=document.RootElement;
        if(root.ValueKind!=JsonValueKind.Object || !root.TryGetProperty("schema",out var schema) ||
            schema.ValueKind!=JsonValueKind.Number || !schema.TryGetInt32(out int version) || version!=1 ||
            !root.TryGetProperty("id",out var identity) || identity.ValueKind!=JsonValueKind.String ||
            !identity.TryGetGuid(out var storedId) || storedId!=id)
            throw new InvalidDataException("Unexpected task identity or schema");
        var task=root.Deserialize<Work>(Assets.Json)!;
        if(task.Id!=id || task.Schema!=1 || string.IsNullOrWhiteSpace(task.Request) || task.Request.Length>2048 ||
            task.Events is not {Length:>0} || task.Events.Any(e=>e==null || e.At==default) ||
            !ValidTimeline(task.Events) ||
            task.Proposal is {} p && (p.Sha256 is not {Length:64} || !p.Sha256.All(Uri.IsHexDigit)) ||
            task.Evidence is {} citation && string.IsNullOrWhiteSpace(citation.Id) ||
            task.State is "review" or "completed" && task.Evidence==null ||
            task.State=="completed" && string.IsNullOrEmpty(task.Result) ||
            root.TryGetProperty("state",out var state) &&
                (state.ValueKind!=JsonValueKind.String || state.GetString()!=task.State))
            throw new InvalidDataException("Malformed task record");
        return task;
    }

    private static bool ValidTimeline(TaskEvent[] events) => events.Select(e=>e.State).ToArray() switch {
        ["queued"] or ["queued","interrupted" or "failed" or "needs clarification" or "review"] => true,
        ["queued","review","completed" or "cancelled" or "interrupted" or "needs clarification" or "failed"] => true,
        _ => false
    };

    private string PathFor(Guid id)=>Path.Combine(directory,id.ToString("N")+".json");
    public Work? Read(Guid id)
    {
        try { return Load(PathFor(id),id); }
        catch(FileNotFoundException) { return null; }
    }
    public IEnumerable<Work> History()=>Directory.GetFiles(directory,"*.json")
        .Select(path=>Load(path,Guid.ParseExact(Path.GetFileNameWithoutExtension(path),"N")));
    public void Save(Work task)
    {
        string path=PathFor(task.Id);
        using(var f=new FileStream(path+".pending",FileMode.CreateNew,FileAccess.Write,FileShare.None))
        {
            JsonSerializer.Serialize(f,task,Assets.Json);
            f.Flush(true);
        }
        File.Move(path+".pending",path,true);
    }
    public void Dispose()=>lease.Dispose();
}
