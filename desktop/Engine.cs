using System.IO;

namespace AtlasNext;

// Single dispatcher owns task commands. The exclusive journal lease also fences other processes.
internal sealed class Engine(string profile) : IDisposable
{
    private readonly Routing routing=new();
    private readonly Library library=new();
    private readonly Journal journal=new(profile);
    public IEnumerable<Work> History()=>journal.History().OrderByDescending(w=>w.Events.Last().At);
    public Work Submit(Guid id,string request)
    {
        request=request.Trim();
        if(request.Length is 0 or >2048) throw new ArgumentException("Enter a request of 1–2048 characters.");
        var old=journal.Read(id);
        if(old!=null) return old.Request==request ? old : throw new InvalidOperationException("Conflicting task identity");
        var task=new Work(id,request).At("queued");
        journal.Save(task);
        try
        {
            var clock=System.Diagnostics.Stopwatch.StartNew();
            var proposed=routing.Infer(request);
            clock.Stop();
            task=task with {InferenceMicroseconds=(long)clock.Elapsed.TotalMicroseconds, ProcessWorkingSetBytes=Environment.WorkingSet};
            var hit=library.Exact(request);
            var route=hit?.Record.Type ?? Routing.Rule(request);
            task=task with {Proposal=proposed,Specialist=route};
            if(route is "fact" or "procedure") hit??=library.Find(request,route);
            if(hit==null)
                task=(task with {Authorization="no matching scoped record",Verification="no evidence",
                    Result="Specify a supported archive or export question, or enter a catalog ID."}).At("needs clarification");
            else
                task=(task with {Authorization="allowed public-demo record read",Evidence=hit.Citation}).At("review");
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException)
        {
            task=(task with {Result=ex.Message,Verification="failed"}).At("failed");
        }
        journal.Save(task);
        return task;
    }
    public Work Finish(Guid id,bool cancel=false)
    {
        var task=journal.Read(id) ?? throw new ArgumentException("Task missing");
        if(task.State!="review") return task;
        if(cancel) task=(task with {Verification="cancelled before delivery",Result=null}).At("cancelled");
        else
        {
            try
            {
                var source=library.Exact(task.Evidence!.Id);
                if(source==null || source.Citation!=task.Evidence || task.Scope!=source.Record.Scope ||
                    task.Specialist!=source.Record.Type) throw new InvalidDataException("Source failed verification");
                task=(task with {Result=source.Record.Body,Verification="release hash, scope and version checked"}).At("completed");
            }
            catch(Exception ex) when(ex is IOException or InvalidDataException)
            {
                task=(task with {Result=ex.Message,Verification="failed"}).At("failed");
            }
        }
        journal.Save(task);
        return task;
    }
    public void Dispose()=>journal.Dispose();
}
