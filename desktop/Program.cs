using System.IO;
using System.Windows;
using System.Runtime.CompilerServices;
[assembly:InternalsVisibleTo("AtlasNext.Checks")]

namespace AtlasNext;

static class Program
{
    [STAThread]
    static int Main()
    {
        try
        {
            string profile=Environment.GetEnvironmentVariable("ATLAS_NEXT_PROFILE") ??
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ATLAS Next","profile");
            using var engine=new Engine(profile);
            var app=new Application();
            return app.Run(new WorkWindow(engine,profile));
        }
        catch(Exception ex)
        {
            MessageBox.Show(ex.Message,"ATLAS Next startup failed",MessageBoxButton.OK,MessageBoxImage.Error);
            return 1;
        }
    }
}
