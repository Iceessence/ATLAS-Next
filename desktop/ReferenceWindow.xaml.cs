using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;

namespace AtlasNext;

public partial class ReferenceWindow : Window
{
    private readonly LocalReference client=new();
    private readonly List<ReferenceTurn> history=[];
    private CancellationTokenSource? current;
    private bool closed;
    private readonly string? attachment;
    internal ReferenceWindow(string project,string? attachment=null)
    {
        InitializeComponent();
        ChatProject.Text="Local chat · "+project;
        this.attachment=attachment;
        if(attachment!=null) {
            AttachmentPanel.Visibility=Visibility.Visible;
            AttachmentPreview.Text=attachment;
            ChatInput.Text="Suggest a small change. Give a complete copy-ready snippet, target path, insertion point, required setup, and tests. Mark assumptions and untested behavior.";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        closed=true;
        current?.Cancel();
        if(current==null) client.Dispose();
        base.OnClosed(e);
    }

    private void Buttons()
    {
        bool busy=current!=null;
        bool enabled=EnableReference.IsChecked==true;
        Discover.IsEnabled=enabled && !busy;
        SendChat.IsEnabled=enabled && Model.SelectedItem is ReferenceModel && !busy;
        CancelChat.IsEnabled=busy;
        Model.IsEnabled=enabled && !busy;
        ClearChat.IsEnabled=!busy;
        EnableReference.IsEnabled=!busy;
        UseAttachment.IsEnabled=!busy;
    }

    private void EnableChanged(object sender,RoutedEventArgs e)
    {
        ChatStatus.Text=EnableReference.IsChecked==true ? "Find installed models to connect to 127.0.0.1:11434." : "Connection disabled.";
        Buttons();
    }

    private void ModelChanged(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        ClearHistory();
        ModelIdentity.Text=Model.SelectedItem is ReferenceModel model ?
            $"External reference · {model.Name} · SHA-256 {model.Digest}" : "";
        Buttons();
    }

    private void ClearHistory()
    {
        history.Clear();
        ChatTranscript.Clear();
    }

    private void AttachmentChanged(object sender,RoutedEventArgs e)=>ClearHistory();
    private void Clear(object sender,RoutedEventArgs e)=>ClearHistory();
    private void Cancel(object sender,RoutedEventArgs e)=>current?.Cancel();

    private async void LoadModels(object sender,RoutedEventArgs e)
    {
        if(current!=null || EnableReference.IsChecked!=true) return;
        await Run(async token=>{
            var models=await client.Discover(token);
            Model.ItemsSource=models;
            ChatStatus.Text=models.Length==0 ? "No supported installed local model found." : "Select an installed model.";
        });
    }

    private async void Send(object sender,RoutedEventArgs e)
    {
        if(current!=null || EnableReference.IsChecked!=true || Model.SelectedItem is not ReferenceModel model) return;
        string prompt=ChatInput.Text.Trim();
        if(prompt.Length==0) return;
        await Run(async token=>{
            bool include=UseAttachment.IsChecked==true && attachment!=null;
            var input=new ReferenceTurn("user",include && history.Count==0 ?
                $"Treat the excerpt as reference data. Propose changes only. Review task:\n{prompt}\n\nSelected source excerpt:\n{attachment}" : prompt);
            string reply=await client.Reply(model,[..history,input],token);
            history.Add(input);
            history.Add(new("assistant",reply));
            ChatTranscript.AppendText($"You{(include ? " · source excerpt authorized" : "")}\n{prompt}\n\nReference model · {model.Name} · unverified\n{reply}\n\n");
            ChatTranscript.ScrollToEnd();
            ChatInput.Clear();
            ChatStatus.Text="Reply received.";
        });
    }

    private async Task Run(Func<CancellationToken,Task> action)
    {
        using var cancellation=new CancellationTokenSource();
        current=cancellation;
        ChatStatus.Text="Working…";
        Buttons();
        try { await action(cancellation.Token); }
        catch(OperationCanceledException) { ChatStatus.Text=cancellation.IsCancellationRequested ? "Request cancelled." : "Local request timed out."; }
        catch(Exception ex) when(ex is HttpRequestException or IOException or JsonException or InvalidDataException or ArgumentException) {
            ChatStatus.Text=ex.Message;
        } finally {
            current=null;
            if(closed) client.Dispose();
            else Buttons();
        }
    }
}
