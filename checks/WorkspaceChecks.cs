using System.IO;
using System.Windows.Controls;
using AtlasNext;

internal static class WorkspaceChecks
{
    internal static void Run(string profile,Action<bool,string> check)
    {
        using(var engine=new Engine(profile)) {
            engine.SelectProject("try-work");
            WorkspaceState.Remember(profile,engine.Project);
            WorkspaceState.Remember(profile,engine.Project);
        }
        using(var engine=new Engine(profile)) {
            WorkspaceState.Restore(engine,profile);
            var window=new WorkWindow(engine,profile);
            check(engine.Project=="try-work" && ((TextBlock)window.FindName("ProjectLabel")).Text=="Active project: try-work",
                "workspace restores the selected project on reopen");
            window.Close();
        }
        File.WriteAllText(Path.Combine(profile,"active-project.txt"),"../elsewhere");
        using(var engine=new Engine(profile)) {
            WorkspaceState.Restore(engine,profile);
            check(engine.Project=="default","invalid persisted project selection falls back safely");
        }
        var report=ModelEvidenceWindow.ReadReport();
        check(report.Contains("fitting blocked",StringComparison.Ordinal) && report.Contains("native · 15/16",StringComparison.Ordinal) &&
            report.Contains("project specialist · shadow",StringComparison.Ordinal),"workspace displays pinned experiment qualification and measured outcomes");
        check(report.Contains("Primary Master · 4 → 6 → 1 tanh · 37 parameters",StringComparison.Ordinal) &&
            report.Contains("Native-original lab · shadow",StringComparison.Ordinal) &&
            report.Contains("External inference calls 0",StringComparison.Ordinal) &&
            report.Contains("Refit rejected · 13/20",StringComparison.Ordinal),
            "workspace displays original model lineage and rejected refit");
    }
}
