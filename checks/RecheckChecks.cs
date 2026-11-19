using System.IO;
using System.Text.Json.Nodes;
using AtlasNext;

internal static class RecheckChecks
{
    public static void Run(string root,Action<bool,string> check)
    {
        Directory.CreateDirectory(root);
        string profile=Path.Combine(root,"profile");
        using var engine=new Engine(profile);
        const string compare="Compare project claims: Check count | Inventory count";
        const string recheck="Recheck these claims.";
        Imported Import(string name,string text) {
            string path=Path.Combine(root,name);
            File.WriteAllText(path,text);
            return engine.Import(path);
        }
        Work Start(Guid? id)=>engine.Submit(Guid.NewGuid(),recheck,null,id);
        Work Finish(Work task)=>engine.Finish(task.Id);
        Work Ask(Guid? id)=>Finish(Start(id));

        Import("checks.txt","Check count: 20");
        Import("inventory.txt","Inventory count: 40");
        var original=Finish(engine.Submit(Guid.NewGuid(),compare));
        check(original.State=="completed","initial project comparison for recheck uses normal authorization");
        check(Ask(null) is {State:"needs clarification",ResearchReview.Verdict:"unsupported",Evidence:null},
            "recheck requires a selected completed comparison");
        check(Ask(Ask(original.Id).Id).State=="needs clarification","rechecks preserve an explicit original comparison boundary");
        var same=Ask(original.Id);
        check(same is {State:"completed",ResearchReview.ChangedClaims:[],Dependencies.Length:2} &&
            same.ParentTaskId==original.Id && same.ResearchReview.Sources.SequenceEqual(original.ResearchReview!.Sources),
            "unchanged recheck pins original receipt and verifies exact current spans");

        Import("checks.txt","# New report\nCheck count: 21");
        var updated=Ask(original.Id);
        check(updated is {State:"completed",ResearchReview.ChangedClaims:["check count"]} &&
            updated.Result=="check count: 21\ninventory count: 40" && updated.Evidence!.Version==2 &&
            updated.Evidence.Span!.Line==2 && updated.Evidence.Digest!=original.Evidence!.Digest,
            "recheck returns changed values from current direct citations");
        check(engine.History().Single(w=>w.Id==original.Id).Result=="check count: 20\ninventory count: 40",
            "completed source receipt retains historical values");

        var queued=Start(original.Id);
        Import("other.txt","Unrelated count: 4");
        check(Finish(queued).State=="completed","unrelated imported revision does not stale a reviewed comparison");
        queued=Start(original.Id);
        Import("inventory.txt","Inventory count: 41");
        check(Finish(queued) is {State:"needs clarification",ResearchReview.Verdict:"stale"},
            "changed secondary source after submit prevents completion");
        check(engine.Finish(Start(original.Id).Id,true).State=="cancelled","recheck retains a cancellation point");

        var restricted=engine.Submit(Guid.NewGuid(),recheck,updated.Evidence!.Id,original.Id);
        check(restricted.State=="needs clarification" && restricted.Evidence==null,"recheck cannot bypass the Primary source restriction");
        Import("conflict.txt","Inventory count: 0");
        check(Ask(original.Id) is {State:"needs clarification",ResearchReview.Verdict:"contradiction"} conflict &&
            conflict.ResearchReview!.ChangedClaims!.SequenceEqual(new[]{"check count","inventory count"}) && conflict.Evidence==null,
            "conflict withholding preserves current evidence and changed claim identity");
        Import("conflict.txt","Unrelated topic: test");

        queued=Start(original.Id);
        var substitute=Finish(engine.Submit(Guid.NewGuid(),compare));
        string pendingFile=Path.Combine(profile,"tasks",queued.Id.ToString("N")+".json");
        var pending=JsonNode.Parse(File.ReadAllText(pendingFile))!.AsObject();
        pending["parentTaskId"]=substitute.Id;
        pending["dependencies"]![0]=substitute.Id.ToString("N");
        File.WriteAllText(pendingFile,pending.ToJsonString());
        check(Finish(queued) is {State:"failed",SourceIntegrity:"failed"},
            "substituting a legitimate parent and task ID fails the stored receipt binding");

        queued=Start(original.Id);
        engine.SelectProject("different");
        check(Ask(original.Id) is {State:"needs clarification",Evidence:null,ResearchReview.Sources:[]},
            "foreign project cannot use or disclose a historical comparison");
        try {
            Finish(queued);
            check(false,"cross-project finish should throw");
        } catch(InvalidOperationException) {
            check(true,"foreign project cannot finish a queued recheck");
        }
    }
}
