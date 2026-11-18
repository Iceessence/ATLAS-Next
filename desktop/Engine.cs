using System.IO;

namespace AtlasNext;

// Single dispatcher owns task commands. The exclusive journal lease also fences other processes.
internal sealed class Engine(string profile) : IDisposable
{
    private readonly Routing routing=new();
    private readonly Library library=new();
    private readonly Journal journal=new(profile);
    private Documents documents=new(profile);
    public string Project { get; private set; }="default";
    public void SelectProject(string name)
    {
        name=name.Trim().ToLowerInvariant();
        if(!System.Text.RegularExpressions.Regex.IsMatch(name,@"\A[a-z][a-z0-9-]{0,31}\z"))
            throw new ArgumentException("Use a project name of 1–32 letters, digits or hyphens, starting with a letter.");
        documents=new Documents(profile,name);
        Project=name;
    }
    public Imported Import(string path)=>documents.Import(path);
    public string RecoveryNotice => journal.RecoveryNotice;
    public IEnumerable<Work> History()=>journal.History().Where(w=>w.Project==Project).OrderByDescending(w=>w.Events.Last().At);
    public Work Submit(Guid id,string request)
    {
        request=request.Trim();
        if(request.Length is 0 or >2048) throw new ArgumentException("Enter a request of 1–2048 characters.");
        var old=journal.Read(id);
        if(old!=null) return old.Project==Project && old.Request==request ? old : throw new InvalidOperationException("Conflicting task identity");
        var task=(new Work(id,request) {Project=Project}).At("queued");
        journal.Save(task);
        try
        {
            var clock=System.Diagnostics.Stopwatch.StartNew();
            var proposed=routing.Infer(request);
            clock.Stop();
            task=task with {InferenceMicroseconds=(long)clock.Elapsed.TotalMicroseconds, ProcessWorkingSetBytes=Environment.WorkingSet};
            var exact=library.Exact(request);
            var route=exact?.Record.Type ?? Routing.Rule(request);
            var candidate=exact ?? library.Find(request,route);
            var passages=documents.Active();
            if(exact==null && route!="procedure") {
                var words=Assets.Tokens(request).ToHashSet();
                var passage=passages.Where(p=>Assets.Tokens(p.Subject).All(words.Contains))
                    .OrderByDescending(p=>p.Subject.Length).FirstOrDefault();
                if(passage!=null && (candidate==null || passage.Subject!=Research.CatalogSubject(candidate))) {
                    route="fact";
                    candidate=Research.FromPassage(passage);
                }
            }
            var finding=Research.Assess(request,library,passages);
            task=task with {Proposal=proposed,Specialist=route,Router="deterministic-route-v3",ResearchReview=finding.Assessment};
            if(candidate==null || candidate.Citation!=finding.Source?.Citation)
                task=(task with {Authorization="no matching scoped record",Verification="answer withheld",AnswerRelevance="unsupported",
                    Result=finding.Source==null ? finding.Assessment.Detail : "Primary could not resolve a source for this request."}).At("needs clarification");
            else
                task=(task with {Authorization="allowed scoped record read",Scope=candidate.Record.Scope,
                    Capability=candidate.Citation.Span==null ? task.Capability : "read owner-imported text",
                    Budget="2048 chars / 32 documents / 256 KiB per import / 8 matching passages / CPU",Evidence=candidate.Citation}).At("review");
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            task=(task with {Result=ex.Message,Verification="failed"}).At("failed");
        }
        journal.Save(task);
        return task;
    }
    public Work Finish(Guid id,bool cancel=false)
    {
        var task=journal.Read(id) ?? throw new ArgumentException("Task missing");
        if(task.Project!=Project) throw new InvalidOperationException("Task belongs to a different project.");
        if(task.State!="review") return task;
        if(cancel) task=(task with {Verification="cancelled before delivery",Result=null}).At("cancelled");
        else
        {
            try
            {
                var cited=task.Evidence!;
                var active=documents.Active();
                var passage=cited.Span==null ? null : active.FirstOrDefault(p=>p.Citation==cited);
                var source=passage!=null ? Research.FromPassage(passage) : library.Exact(cited.Id);
                if(source==null || source.Citation!=cited || task.Scope!=source.Record.Scope ||
                    task.Specialist!=source.Record.Type) throw new InvalidDataException("Source failed verification");
                var finding=Research.Assess(task.Request,library,active);
                task=task with {SourceIntegrity="passed",ResearchReview=finding.Assessment};
                if(finding.Source!=null && finding.Assessment.Sources.Contains(cited))
                    task=(task with {AnswerRelevance="supported",Result=source.Record.Body,
                        Verification="source and answer checked"}).At("completed");
                else {
                    if(finding.Source!=null) task=task with {ResearchReview=finding.Assessment with {
                        Verdict="unsupported",Detail="The selected citation does not support this request."}};
                    task=(task with {AnswerRelevance="unsupported",Verification="answer withheld",
                        Result=task.ResearchReview!.Detail}).At("needs clarification");
                }
            }
            catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                task=(task with {Result=ex.Message,SourceIntegrity="failed",Verification="failed"}).At("failed");
            }
        }
        journal.Save(task);
        return task;
    }
    public int StopAll()
    {
        var waiting=journal.History().Where(task=>task.State=="review").ToList();
        foreach(var task in waiting) journal.Save((task with {Verification="cancelled before delivery",Result=null}).At("cancelled"));
        return waiting.Count;
    }
    public void Dispose()=>journal.Dispose();
}
