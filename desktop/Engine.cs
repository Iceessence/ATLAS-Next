using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

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
    public Work Submit(Guid id,string request)=>Submit(id,request,null);

    // A source restriction can only narrow Primary retrieval. The benchmark uses it
    // in fresh disposable profiles. Verification always sees the entire project.
    internal Work Submit(Guid id,string request,string? primaryDocument,Guid? selectedTask=null)
    {
        request=request.Trim();
        if(request.Length is 0 or >2048) throw new ArgumentException("Enter a request of 1–2048 characters.");
        var code=CodeQuestion.Parse(request);
        Guid? parentId=code?.FollowUp==true || ProjectQuestions.IsRecheck(request) ? selectedTask : null;
        var old=journal.Read(id);
        if(old!=null) return old.Project==Project && old.Request==request && old.ParentTaskId==parentId ? old : throw new InvalidOperationException("Conflicting task identity");
        var task=(new Work(id,request) {Project=Project,ParentTaskId=parentId}).At("queued");
        journal.Save(task);
        try
        {
            var clock=System.Diagnostics.Stopwatch.StartNew();
            var proposed=routing.Infer(request);
            clock.Stop();
            task=task with {InferenceMicroseconds=(long)clock.Elapsed.TotalMicroseconds, ProcessWorkingSetBytes=Environment.WorkingSet};
            string? projectRoute=ProjectRoute(request,code);
            task=projectRoute!=null
                ? QueueProjectAnswer(task,proposed,projectRoute,code,primaryDocument,parentId)
                : QueueLookup(task,request,proposed,primaryDocument,code,parentId);
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            task=(task with {Result=ex.Message,Verification="failed"}).At("failed");
        }
        journal.Save(task);
        return task;
    }

    private Work QueueLookup(Work task,string request,Recommendation proposed,string? primaryDocument,CodeQuestion? code,Guid? parentId)
    {
        var exact=library.Exact(request);
        var route=exact?.Record.Type ?? Routing.Rule(request);
        var candidate=exact ?? library.Find(request,route);
        var passages=documents.Active();
        var eligible=primaryDocument==null ? passages : passages.Where(p=>p.Citation.Id==primaryDocument);
        Finding finding;
        if(UseCode(code,passages,request)) {
            var parent=ReadParent(parentId,passages);
            var passage=Routing.ProjectCode(request,eligible,parent?.Passage,code!.Type);
            route=code.Type;
            candidate=passage==null ? null : Research.FromPassage(passage);
            finding=code.Verify(passages,parent);
            task=task with {Kind="code.lookup",Dependencies=parentId.HasValue ? [parentId.Value.ToString("N")] : []};
        } else {
            if(exact==null) {
                var passage=Routing.ProjectFact(request,eligible.Where(p=>p.Symbol==null));
                if(passage!=null && (candidate==null || passage.Subject!=Research.CatalogSubject(candidate))) {
                    route="fact";
                    candidate=Research.FromPassage(passage);
                }
            }
            finding=Research.Assess(request,library,passages.Where(p=>p.Symbol==null).ToArray());
        }
        task=task with {Proposal=proposed,Specialist=route,PrimaryCandidateId=candidate?.Citation.Id,Router="deterministic-route-v6",ResearchReview=finding.Assessment};
        if(candidate==null || finding.Source==null || !finding.Assessment.Sources.Contains(candidate.Citation))
            task=(task with {Authorization="no matching scoped record",Verification="answer withheld",
                AnswerRelevance=finding.Source==null ? finding.Assessment.RequestRelevance : "unsupported",
                Result=finding.Source==null ? finding.Assessment.Detail : "Primary could not resolve a source for this request."}).At("needs clarification");
        else
            task=(task with {Authorization="allowed scoped record read",Scope=candidate.Record.Scope,
                Capability=candidate.Citation.Span==null ? task.Capability : "read owner-imported text",
                Budget="2048 chars / 32 documents / 256 KiB per import / 8 matching passages / CPU",Evidence=candidate.Citation}).At("review");
        return task;
    }

    private static string? ProjectRoute(string request,CodeQuestion? code)=>
        ProjectQuestions.IsResearch(request) ? ProjectQuestions.ResearchType :
        ProjectQuestions.IsRecheck(request) ? ProjectQuestions.RecheckType :
        code?.Calls==true ? ProjectQuestions.CallsType : null;

    private ProjectFinding CheckProjectAnswer(string route,string request,CodeQuestion? code,IReadOnlyList<OwnedSource> sources,
        Guid? parentId,Work? comparison)
    {
        var passages=sources.SelectMany(Documents.Parse).ToArray();
        if(route==ProjectQuestions.RecheckType) return ProjectQuestions.Recheck(comparison,passages);
        if(code?.Calls!=true) return ProjectQuestions.Compare(request,passages);
        var parent=code.FollowUp ? ReadParent(parentId,passages) : null;
        return ProjectQuestions.Calls(code,sources,passages,parent);
    }

    private Work QueueProjectAnswer(Work task,Recommendation proposed,string route,CodeQuestion? code,string? primaryDocument,Guid? parentId)
    {
        var comparison=route==ProjectQuestions.RecheckType ? ReadComparison(parentId) : null;
        var finding=CheckProjectAnswer(route,task.Request,code,documents.Sources(),parentId,comparison);
        task=task with {Kind=route,Specialist=route,Proposal=proposed,Router="deterministic-route-v8",ResearchReview=finding.Assessment,
            Dependencies=comparison!=null ? ComparisonDependencies(comparison) :
                parentId.HasValue ? [parentId.Value.ToString("N")] : []};
        if(finding.Evidence is not {} evidence || primaryDocument!=null && finding.Assessment.Sources.Any(c=>c.Id!=primaryDocument))
            return (task with {Authorization="no matching scoped record",Verification="answer withheld",
                AnswerRelevance=finding.Evidence!=null ? "unsupported" : finding.Assessment.RequestRelevance,
                Result=finding.Evidence!=null ? "The Primary source restriction excludes required evidence." : finding.Assessment.Detail}).At("needs clarification");
        return (task with {Authorization="allowed scoped record read",Scope="owner-local",Capability="read owner-imported text",
            Budget="2048 chars / 32 documents / 256 KiB per import / 8 evidence passages / CPU",
            Evidence=evidence,PrimaryCandidateId=evidence.Id}).At("review");
    }

    private Work FinishProjectAnswer(Work task,string route,CodeQuestion? code)
    {
        bool recheck=route==ProjectQuestions.RecheckType;
        var comparison=recheck ? ReadComparison(task.ParentTaskId) : null;
        bool validParent=recheck
            ? comparison!=null && task.Dependencies!=null && task.Dependencies.SequenceEqual(ComparisonDependencies(comparison))
            : code?.FollowUp==true
                ? task.ParentTaskId is {} pid && task.Dependencies is [var dep] && dep==pid.ToString("N")
                : task.ParentTaskId==null && task.Dependencies is [];
        if(task.Kind!=route || task.Specialist!=route || task.Authorization!="allowed scoped record read" ||
            task.Scope!="owner-local" || task.Capability!="read owner-imported text" || !validParent ||
            task.Evidence?.Span==null || task.Evidence.Id!=task.PrimaryCandidateId ||
            task.ResearchReview is not {Verdict:"supported",Sources:[var first,..]} prior || first!=task.Evidence)
            throw new InvalidDataException("Project evidence failed authorization.");

        var active=documents.Sources();
        bool Active(Citation c)=>active.Any(s=>s.Id==c.Id && s.Version==c.Version && s.Digest==c.Digest && s.Path==c.Span?.Path);
        if(prior.Sources.Any(c=>!Active(c))) {
            var claims=prior.Claims?.Select(c=>c.Sources.All(Active) ? c :
                c with {Verdict="stale",Freshness="source revision changed"}).ToArray();
            return StaleProjectAnswer(task,prior with {Claims=claims});
        }

        var current=CheckProjectAnswer(route,task.Request,code,active,task.ParentTaskId,comparison);
        if(current.Assessment.Verdict!="supported")
            return (task with {ResearchReview=current.Assessment,SourceIntegrity="passed",Verification="answer withheld",
                AnswerRelevance=current.Assessment.RequestRelevance,Result=current.Assessment.Detail}).At("needs clarification");

        if(current.Evidence!=task.Evidence || !SameEvidence(prior,current.Assessment))
            return StaleProjectAnswer(task,prior);

        return (task with {ResearchReview=current.Assessment,SourceIntegrity="passed",AnswerRelevance="supported",
            Result=current.Answer,Verification="source and answer checked"}).At("completed");
    }

    private static Work StaleProjectAnswer(Work task,Assessment prior)
    {
        const string detail="The scoped evidence changed after review. Submit a fresh task.";
        return (task with {ResearchReview=prior with {Verdict="stale",Detail=detail,Freshness="revision or supporting set changed"},
            Verification="answer withheld",AnswerRelevance="subject and intent matched",Result=detail}).At("needs clarification");
    }

    private Work? ReadComparison(Guid? id)
    {
        var work=id is {} selected ? journal.Read(selected) : null;
        return ProjectQuestions.IsComparisonParent(work,Project) ? work : null;
    }

    private static string[] ComparisonDependencies(Work comparison)=>
        [comparison.Id.ToString("N"),Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(comparison,Assets.Json)))];

    private static bool SameEvidence(Assessment first,Assessment second)=>
        (first.ChangedClaims??[]).SequenceEqual(second.ChangedClaims??[]) && first.Sources.SequenceEqual(second.Sources) && (first.Claims is {} a && second.Claims is {} b
            ? a.Length==b.Length && a.Zip(b).All(pair=>pair.First.Subject==pair.Second.Subject &&
                pair.First.Verdict==pair.Second.Verdict && pair.First.Freshness==pair.Second.Freshness &&
                pair.First.Sources.SequenceEqual(pair.Second.Sources))
            : first.Claims==null && second.Claims==null);

    // Preserve existing named text lookups when the project has no code for that
    // identifier. Explicit source commands and contextual turns always use code verification.
    private static bool UseCode(CodeQuestion? code,IReadOnlyList<Passage> active,string request)=>
        code!=null && (code.FollowUp || active.Any(p=>p.Symbol!=null) ||
            !System.Text.RegularExpressions.Regex.IsMatch(request,@"\A(?:please\s+)?where\s+is\s+[\p{L}_][\p{L}\p{N}\p{M}_]*\s+(?:defined|declared)\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
            !active.Any(p=>p.Subject==Research.Subject(code.Name+" declaration line")));

    private SelectedDefinition? ReadParent(Guid? id,IReadOnlyList<Passage> active)=>
        id==null ? null : CodeQuestion.Parent(journal.Read(id.Value),Project,active);

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
                var code=CodeQuestion.Parse(task.Request);
                task=ProjectRoute(task.Request,code) is {} projectRoute
                    ? FinishProjectAnswer(task,projectRoute,code) : FinishLookup(task,code);
            }
            catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                task=(task with {Result=ex.Message,SourceIntegrity="failed",ResearchReview=null,Verification="failed"}).At("failed");
            }
        }
        journal.Save(task);
        return task;
    }

    private Work FinishLookup(Work task,CodeQuestion? code)
    {
        var cited=task.Evidence!;
        var active=documents.Active();
        var passage=cited.Span==null ? null : active.FirstOrDefault(p=>p.Citation==cited);
        var source=passage!=null ? Research.FromPassage(passage) : library.Exact(cited.Id);
        if(task.Authorization!="allowed scoped record read" || source==null || source.Citation!=cited || task.Scope!=source.Record.Scope ||
            task.Specialist!=source.Record.Type) throw new InvalidDataException("Source failed verification");
        var finding=UseCode(code,active,task.Request)
            ? code!.Verify(active,task.ParentTaskId is {} parentId && task.Dependencies is [var dependency] && dependency==parentId.ToString("N") ? ReadParent(parentId,active) : null)
            : Research.Assess(task.Request,library,active.Where(p=>p.Symbol==null).ToArray());
        task=task with {SourceIntegrity="passed",ResearchReview=finding.Assessment};
        if(finding.Source!=null && finding.Assessment.Sources.Contains(cited))
            task=(task with {AnswerRelevance="supported",Result=source.Record.Body,
                Verification="source and answer checked"}).At("completed");
        else {
            if(finding.Source!=null) task=task with {ResearchReview=finding.Assessment with {
                Verdict="unsupported",Detail="The selected citation does not support this request."}};
            task=(task with {AnswerRelevance=task.ResearchReview!.RequestRelevance,Verification="answer withheld",
                Result=task.ResearchReview!.Detail}).At("needs clarification");
        }
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
