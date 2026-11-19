using System.Diagnostics;
using System.IO;
using System.Text.Json;
using AtlasNext;

internal static class StudyChecks
{
    internal static void Run(string root,Action<bool,string> check)
    {
        Directory.CreateDirectory(root);
        string profile=Path.Combine(root,"profile");
        using var engine=new Engine(profile);
        Imported Put(string leaf,string content) {
            string path=Path.Combine(root,leaf);
            File.WriteAllText(path,content);
            return engine.Import(path);
        }
        var a=Put("validated.txt","Build status: clean\n");
        var b=Put("owner.md","Release channel: stable\n");
        var decoy=Put("draft.txt","Build status: broken\n");
        var plan=new ResearchPlan("Verify the package and its channel","Build status | Release channel",[a.Id,b.Id]);
        var pending=engine.Study(Guid.NewGuid(),plan);
        var success=engine.Finish(pending.Id);
        check(pending is {State:"review",Dependencies.Length:3} && success is {State:"completed",ResearchReview.Sources.Length:2} &&
             success.Result=="build status: clean\nrelease channel: stable" && success.ResearchReview.Sources.All(c=>c.Id!=decoy.Id),
             "multi-step study evaluates owner-authorized sources and preserves exact provenance");
        var conflict=engine.Study(Guid.NewGuid(),plan with {SourceIds=[a.Id,b.Id,decoy.Id]});
        check(conflict is {State:"needs clarification",ResearchReview.Verdict:"contradiction"} && conflict.Result!=success.Result,
            "selecting contradictory evidence prevents a conclusion");
        pending=engine.Study(Guid.NewGuid(),plan);
        Put("owner.md","Release channel: testing\n");
        check(engine.Finish(pending.Id) is {State:"needs clarification",ResearchReview.Verdict:"stale"},
            "study detects revision changes before delivery");
        var rerun=engine.Study(Guid.NewGuid(),success.Plan!);
        check(engine.Finish(rerun.Id).Result=="build status: clean\nrelease channel: testing" &&
            engine.History().Single(w=>w.Id==success.Id).Result==success.Result,
            "reusing a plan verifies current sources while preserving historical evidence");
        engine.SelectProject("foreign");
        check(engine.Study(Guid.NewGuid(),plan) is {State:"needs clarification",ResearchReview.Sources:[]},
            "foreign selected identifiers disclose no evidence");
    }

    internal static int Probe(string scratch,string first,string second,string fields)
    {
        if(!Directory.Exists(scratch) || Directory.EnumerateFileSystemEntries(scratch).Any())
            throw new ArgumentException("Expected empty scratch directory.");
        using var engine=new Engine(scratch);
        var a=engine.Import(first);
        var b=engine.Import(second);
        var clock=Stopwatch.StartNew();
        var draft=engine.Study(Guid.NewGuid(),new("Verify two recorded release measurements",fields,[a.Id,b.Id]));
        var outcome=engine.Finish(draft.Id);
        Console.WriteLine(JsonSerializer.Serialize(new {outcome,draftState=draft.State,elapsedMs=clock.Elapsed.TotalMilliseconds,
            workingSetBytes=Environment.WorkingSet},Assets.Json));
        return outcome.State=="completed" ? 0 : 1;
    }
}
