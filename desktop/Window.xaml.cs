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
        Refresh(engine.History().FirstOrDefault());
    }
    private void Refresh(Work? work)
    {
        refreshing=true;
        History.ItemsSource=engine.History().ToList();
        History.SelectedItem=History.Items.Cast<Work>().FirstOrDefault(x=>x.Id==work?.Id);
        refreshing=false;
        ShowTask(work);
    }
    private void ShowTask(Work? task)
    {
        current=task;
        if(task==null) return;
        Status.Text=task.State;
        Summary.Text=task.Result ?? (task.State=="review" ? "A matching record is ready for verification." : "No result delivered.");
        ChecksInfo.Text=$"Source integrity: {task.SourceIntegrity} · Answer relevance: {task.AnswerRelevance}";
        SourceInfo.Text=task.Evidence is {} e ? $"{e.Id} · revision {e.Version}\n{e.Source} · {e.Trust}" : "";
        RoutingInfo.Text=task.Proposal is {} p ? $"Active rule: {task.Router} → {task.Specialist}\nOriginal model: {p.Model} → {p.Route} ({p.Probability:P1})\nStatus: {p.Qualification} · SHA-256 {p.Sha256[..16]}…\nCPU inference {task.InferenceMicroseconds} µs · process {task.ProcessWorkingSetBytes/1048576} MiB" : "Model inference unavailable.";
        Verify.IsEnabled=Stop.IsEnabled=task.State=="review";
        Export.IsEnabled=true;
        Notice.Text="";
    }
    private void SelectTask(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(!refreshing && History.SelectedItem is Work work) ShowTask(work);
    }
    private void Handle(Action action)
    {
        try { action(); }
        catch(Exception ex) { Notice.Text=ex.Message; }
    }
    private void Submit(object sender,RoutedEventArgs e)=>Handle(()=>Refresh(engine.Submit(Guid.NewGuid(),Prompt.Text)));
    private void FinishTask(object sender,RoutedEventArgs e)=>Handle(()=>Refresh(engine.Finish(current!.Id)));
    private void CancelTask(object sender,RoutedEventArgs e)=>Handle(()=>Refresh(engine.Finish(current!.Id,true)));
    private void StopAllTasks(object sender,RoutedEventArgs e)=>Handle(()=>
    {
        int stopped=engine.StopAll();
        Refresh(engine.History().FirstOrDefault());
        Notice.Text=$"Stopped {stopped} pending tasks.";
    });
    private void ExportTask(object sender,RoutedEventArgs e)=>Handle(()=>
    {
        string folder=Path.Combine(profile,"exports");
        Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,$"{current!.Id:N}-{Guid.NewGuid():N}.json");
        using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        JsonSerializer.Serialize(stream,current,Assets.Json);
        stream.Flush(true);
        Notice.Text=$"Receipt saved to {path}";
    });
}
