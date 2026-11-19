using System.IO;
using System.Windows;

namespace AtlasNext;

public partial class ResearchWindow : Window
{
    private readonly Engine engine;
    private Work? current;
    private sealed record SourceChoice(SourceSummary Source)
    {
        public string Label=>$"{Source.Name} · revision {Source.Version} · {Source.Id[4..12]}";
    }
    private sealed record HistoryEntry(Work Work)
    {
        public string Label=>$"{Display(Work)} · {Work.Plan!.Objective}";
    }

    internal ResearchWindow(Engine engine)
    {
        InitializeComponent();
        this.engine=engine;
        ResearchProject.Text="Local research · "+engine.Project;
        Selections.ItemsSource=engine.ProjectSources().Where(s=>
            Path.GetExtension(s.Name).ToLowerInvariant() is ".txt" or ".md").Select(s=>new SourceChoice(s)).ToArray();
        Refresh(null);
    }

    private static string Display(Work work)=>work.State switch {
        "review"=>"Provisional",
        "completed"=>"Verified",
        "needs clarification" when work.ResearchReview?.Verdict=="contradiction"=>"Contradicted",
        "needs clarification"=>"Withheld",
        "failed"=>"Rejected",
        _=>work.State
    };

    private void Refresh(Work? work)
    {
        var rows=engine.History().Where(w=>w.Plan!=null).Select(w=>new HistoryEntry(w)).ToArray();
        Studies.ItemsSource=rows;
        Studies.SelectedItem=rows.FirstOrDefault(row=>row.Work.Id==work?.Id);
        Show(work);
    }

    private void Show(Work? work)
    {
        current=work;
        VerifyStudy.IsEnabled=CancelStudy.IsEnabled=work?.State=="review";
        LoadStudy.IsEnabled=work!=null;
        StudyStatus.Text=work==null ? "Select authorized sources to prepare research." :
            $"{Display(work)} · {work.Verification} · {work.Plan!.SourceIds.Length} authorized sources";
        if(work==null) { StudyEvidence.Clear(); return; }
        var assessment=work.ResearchReview;
        string evidence=assessment?.Claims==null ? "" : string.Join("\n\n",assessment.Claims.Select(claim=>
            $"{claim.Subject} · {claim.Verdict} · {claim.Freshness}\n"+
            string.Join("\n",claim.Sources.Select(CodingGuide.Place))));
        StudyEvidence.Text=$"{work.Plan!.Objective}\n{work.Request}\n\n"+
            "1. Open selected source revisions\n2. Extract and compare requested claims\n3. Verify before delivery\n\n"+
            $"{work.Result ?? assessment?.Detail}\n\n{evidence}";
    }

    private void SelectStudy(object sender,System.Windows.Controls.SelectionChangedEventArgs e)=>
        Show((Studies.SelectedItem as HistoryEntry)?.Work);

    private void Perform(Func<Work> action)
    {
        try { Refresh(action()); }
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or IOException or InvalidDataException or UnauthorizedAccessException) {
            StudyStatus.Text=ex.Message;
        }
    }

    private void Prepare(object sender,RoutedEventArgs e)=>Perform(()=>engine.Study(Guid.NewGuid(),
        new(Objective.Text.Trim(),Fields.Text.Trim(),Selections.SelectedItems.Cast<SourceChoice>().Select(c=>c.Source.Id).ToArray())));

    private void Verify(object sender,RoutedEventArgs e) { if(current!=null) Perform(()=>engine.Finish(current.Id)); }
    private void Cancel(object sender,RoutedEventArgs e) { if(current!=null) Perform(()=>engine.Finish(current.Id,true)); }

    private void LoadPlan(object sender,RoutedEventArgs e)
    {
        if(current?.Plan is not {} plan) return;
        Objective.Text=plan.Objective;
        Fields.Text=plan.Fields;
        Selections.UnselectAll();
        foreach(var choice in Selections.Items.Cast<SourceChoice>())
            if(plan.SourceIds.Contains(choice.Source.Id)) Selections.SelectedItems.Add(choice);
        StudyStatus.Text="Plan loaded. Review authorized sources before preparing a fresh result.";
    }
}
