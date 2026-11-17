using System.Windows;
using System.Windows.Threading;
namespace AtlasNext;
static class Program {
    [STAThread] static void Main() {
        var app = new Application();
        var window = new Window { Content = new System.Windows.Controls.TextBlock { Text = "ATLAS packaging probe" } };
        window.Loaded += (_,_) => window.Dispatcher.BeginInvoke(window.Close, DispatcherPriority.ApplicationIdle);
        app.Run(window);
    }
}
