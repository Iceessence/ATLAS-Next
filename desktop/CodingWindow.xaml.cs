using System.IO;
using System.Windows;

namespace AtlasNext;

public partial class CodingWindow : Window
{
    private readonly Engine engine;
    private readonly Guid selection;
    internal CodingWindow(Engine engine,Guid selection,CodingGuide guide)
    {
        InitializeComponent();
        this.engine=engine;
        this.selection=selection;
        GuideTitle.Text="Coding evidence · "+engine.Project;
        GuideContent.Text=guide.Render();
        ReferenceDraft.IsEnabled=guide.Attachment()!=null;
    }

    private void Draft(object sender,RoutedEventArgs e)
    {
        try {
            var attachment=engine.Guide(selection)?.Attachment();
            if(attachment==null) {
                GuideStatus.Text="Select a current definition with a signature that fits the attachment limit.";
                return;
            }
            new ReferenceWindow(engine.Project,attachment) {Owner=this}.ShowDialog();
        } catch(Exception ex) when(ex is IOException or InvalidDataException or UnauthorizedAccessException) {
            GuideStatus.Text=ex.Message;
        }
    }
}
