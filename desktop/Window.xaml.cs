using System.IO;
using System.Text.Json;
using System.Windows;

namespace AtlasNext;

public partial class WorkWindow : Window
{
    private readonly Engine engine;
    private readonly string profile;
    private Work? current;
    private bool refreshing;
    internal WorkWindow(Engine engine,string profile)
    {
        InitializeComponent();
        this.engine=engine;
        this.profile=profile;
        RecoveryInfo.Text=engine.RecoveryNotice;
        ProjectLabel.Text=$"Active project: {engine.Project}";
        ProjectName.Text=engine.Project;
        RefreshSources();
        Refresh(engine.History().FirstOrDefault());
    }
    private void Refresh(Work? work)
    {
        refreshing=true;
        var history=engine.History().ToList();
        History.ItemsSource=history;
        TaskActivity.Text=$"{history.Count(t=>t.State=="completed")} completed · {history.Count(t=>t.State=="review")} pending · "+
            $"{history.Count(t=>t.State is "needs clarification" or "failed" or "interrupted")} need attention";
        History.SelectedItem=History.Items.Cast<Work>().FirstOrDefault(x=>x.Id==work?.Id);
        refreshing=false;
        ShowTask(work);
    }
    private void ShowTask(Work? task)
    {
        current=task;
        if(task==null) {
            Status.Text="Ready";
            Summary.Text="Find evidence to start a lookup.";
            SourceInfo.Text=RoutingInfo.Text=ChecksInfo.Text=ResearchInfo.Text=Notice.Text="";
            Verify.IsEnabled=Stop.IsEnabled=Export.IsEnabled=false;
            return;
        }
        Status.Text=task.State;
        Summary.Text=task.Result ?? (task.State=="review" ? "A matching record is ready for verification." : "No result delivered.");
        ChecksInfo.Text=$"Source integrity: {task.SourceIntegrity} · Answer relevance: {task.AnswerRelevance}";
        SourceInfo.Text=task.Evidence is {} e ? $"{e.Id} · revision {e.Version}\n{e.Source} · {e.Trust}" : "";
        if(task.Evidence?.Span is {} span)
            SourceInfo.Text+=$"\n{span.Path} · line {span.Line}, characters [{span.Start}, {span.Start+span.Length})";
        ResearchInfo.Text=task.ResearchReview is {} review ?
            $"Research & Verification: {review.Verdict}\n{review.Detail}\nSupport: {review.FactualSupport}\nFreshness: {review.Freshness} · Trust: {review.Trust}\n"+
            (review.Claims is {} claims ? string.Join("\n",claims.Select(c=>
                $"{c.Subject} · {c.Verdict} · {c.Freshness}\n"+string.Join("\n",c.Sources.Select(DescribeSource)))) :
                string.Join("\n",review.Sources.Select(DescribeSource))) :
            "Research & Verification: no assessment recorded.";
        RoutingInfo.Text=task.Proposal is {} p ? $"Active rule: {task.Router} → {task.Specialist}\nOriginal model: {p.Model} → {p.Route} ({p.Probability:P1})\nStatus: {p.Qualification} · SHA-256 {p.Sha256[..16]}…\nCPU inference {task.InferenceMicroseconds} µs · process {task.ProcessWorkingSetBytes/1048576} MiB" : "Model inference unavailable.";
        Verify.IsEnabled=Stop.IsEnabled=task.State=="review";
        Export.IsEnabled=true;
        Notice.Text="";
    }
    private static string DescribeSource(Citation citation)=>citation.Span is {} span ?
        $"{span.Path}, line {span.Line}, characters [{span.Start}, {span.Start+span.Length}) · revision {citation.Version} · {citation.Digest}" :
        citation.Source;

    private void SelectTask(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(!refreshing && History.SelectedItem is Work work) ShowTask(work);
    }
    private void Handle(Action action)
    {
        try { action(); }
        catch(Exception ex) { Notice.Text=ex.Message; }
    }
    private void Submit(object sender,RoutedEventArgs e)=>Handle(()=>Refresh(engine.Submit(Guid.NewGuid(),Prompt.Text,null,current?.Id)));
    private void FinishTask(object sender,RoutedEventArgs e)=>Handle(()=>Refresh(engine.Finish(current!.Id)));
    private void CancelTask(object sender,RoutedEventArgs e)=>Handle(()=>Refresh(engine.Finish(current!.Id,true)));
    private void StopAllTasks(object sender,RoutedEventArgs e)=>Handle(()=>
    {
        int stopped=engine.StopAll();
        Refresh(engine.History().FirstOrDefault());
        Notice.Text=$"Stopped {stopped} pending tasks.";
    });
    private void ChangeProject(object sender,RoutedEventArgs e)=>Handle(()=>
    {
        engine.SelectProject(ProjectName.Text);
        ProjectLabel.Text=$"Active project: {engine.Project}";
        ProjectName.Text=engine.Project;
        Prompt.Clear();
        RefreshSources();
        Refresh(engine.History().FirstOrDefault());
        WorkspaceState.Remember(profile,engine.Project);
    });
    private void ImportFile(object sender,RoutedEventArgs e)=>Handle(()=>
    {
        var dialog=new Microsoft.Win32.OpenFileDialog {Title="Import local text",Filter="Supported project files|*.txt;*.md;*.cs;*.py|C# and Python|*.cs;*.py|Text and Markdown|*.txt;*.md",
            CheckFileExists=true,Multiselect=false,DereferenceLinks=false};
        if(dialog.ShowDialog(this)!=true) return;
        var doc=engine.Import(dialog.FileName);
        RefreshSources();
        Notice.Text=$"Imported {doc.Name}, revision {doc.Version}. Indexed {doc.Claims} passages. {doc.Id}";
    });
    private void ExportTask(object sender,RoutedEventArgs e)=>Handle(()=>
    {
        string folder=Path.Combine(profile,"exports",engine.Project=="default" ? "" : "project-"+engine.Project);
        Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,$"{current!.Id:N}-{Guid.NewGuid():N}.json");
        using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        JsonSerializer.Serialize(stream,current,Assets.Json);
        stream.Flush(true);
        Notice.Text=$"Receipt saved to {path}";
    });

    internal static string FormatSources(IReadOnlyList<SourceSummary> sources)
    {
        if(sources.Count==0) return "Imported sources (0/32): none in this project.";
        var shown=sources.Take(8).Select(s=>$"{s.Name} (rev {s.Version}, {s.Claims} {(s.Claims==1 ? "passage" : "passages")}, {s.Digest[..Math.Min(8,s.Digest.Length)]}…)");
        string extra=sources.Count>8 ? $"; (+{sources.Count-8} more)" : "";
        return $"Imported sources ({sources.Count}/32): {string.Join("; ",shown)}{extra}";
    }

    private void OpenModels(object sender,RoutedEventArgs e)=>Handle(()=>
        new ModelEvidenceWindow {Owner=this}.ShowDialog());

    private void SelectSource(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        SourceDetails.Text=SourceCatalog.SelectedItem is SourceSummary source ?
            $"{source.Name} · revision {source.Version} · {source.Claims} indexed passages\nSHA-256 {source.Digest}" : "";
    }

    private void OpenResearch(object sender,RoutedEventArgs e)=>Handle(()=>{
        new ResearchWindow(engine) {Owner=this}.ShowDialog();
        Refresh(engine.History().FirstOrDefault());
    });

    private void OpenCoding(object sender,RoutedEventArgs e)=>Handle(()=>{
        if(current==null || engine.Guide(current.Id) is not {} guide) {
            Notice.Text="Select a completed definition from the current project and revision.";
            return;
        }
        new CodingWindow(engine,current.Id,guide) {Owner=this}.ShowDialog();
    });

    private void OpenReference(object sender,RoutedEventArgs e)=>Handle(()=>
        new ReferenceWindow(engine.Project) {Owner=this}.ShowDialog());

    private void RefreshSources()
    {
        try {
            var sources=engine.ProjectSources();
            SourcesInfo.Text=FormatSources(sources);
            SourceCatalog.ItemsSource=sources;
        }
        catch(Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) {
            SourcesInfo.Text=$"Imported sources unavailable: {ex.Message}";
            SourceCatalog.ItemsSource=null;
            SourceDetails.Text="";
        }
    }
}
