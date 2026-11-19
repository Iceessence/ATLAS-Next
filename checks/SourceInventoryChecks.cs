using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AtlasNext;

internal static class SourceInventoryChecks
{
    public static void Run(string root,Action<bool,string> check)
    {
        Directory.CreateDirectory(root);
        string profile=Path.Combine(root,"profile");
        string tamperProfile=Path.Combine(root,"tamper-profile");
        Imported ImportFile(Engine engine,string name,string content)
        {
            string path=Path.Combine(root,name);
            File.WriteAllText(path,content);
            return engine.Import(path);
        }

        IReadOnlyList<SourceSummary> sorted;
        using(var engine=new Engine(profile))
        {
            check(engine.ProjectSources().Count==0,"empty project starts with empty source inventory");
            var window=new WorkWindow(engine,profile);
            window.Show();
            check(((TextBlock)window.FindName("SourcesInfo")).Text=="Imported sources (0/32): none in this project.",
                "window shows empty project source status on startup");

            var beta=ImportFile(engine,"beta.txt","Channel: warm\n");
            var alpha=ImportFile(engine,"alpha.md","# Notes\nBuild: clean\nHost: offline\n");
            var calc=ImportFile(engine,"Calc.cs","class Calc { }\n");
            var py=ImportFile(engine,"helper.py","def ping() -> int:\n    return 1\n");

            sorted=engine.ProjectSources();
            check(sorted.Count==4 && sorted.Select(s=>s.Name).SequenceEqual(["alpha.md","beta.txt","Calc.cs","helper.py"]),
                "mixed project sources sort case-insensitively with exact count");
            check(sorted[0].Claims==alpha.Claims && sorted[1].Claims==beta.Claims && sorted[2].Claims==calc.Claims && sorted[3].Claims==py.Claims &&
                  sorted.All(s=>s.Version==1 && s.Digest.Length==64),
                "inventory preserves exact revision, indexed passage counts, and SHA-256 digests");
            string FormatCount(int count)=>count==1 ? "1 passage" : $"{count} passages";
            string mixedDisplay=WorkWindow.FormatSources(sorted);
            check(alpha.Claims==2 && beta.Claims==1 && calc.Claims>=1 && py.Claims>=1 &&
                  mixedDisplay.StartsWith("Imported sources (4/32): ",StringComparison.Ordinal) &&
                  mixedDisplay.Contains($"alpha.md (rev 1, 2 passages, {sorted[0].Digest[..8]}…)",StringComparison.Ordinal) &&
                  mixedDisplay.Contains($"beta.txt (rev 1, 1 passage, {sorted[1].Digest[..8]}…)",StringComparison.Ordinal) &&
                  mixedDisplay.Contains($"Calc.cs (rev 1, {FormatCount(calc.Claims)}, {sorted[2].Digest[..8]}…)",StringComparison.Ordinal) &&
                  mixedDisplay.Contains($"helper.py (rev 1, {FormatCount(py.Claims)}, {sorted[3].Digest[..8]}…)",StringComparison.Ordinal) &&
                  WorkWindow.FormatSources([new SourceSummary("doc-0",1,"notes.txt",0,new string('a',64))]).Contains("notes.txt (rev 1, 0 passages, aaaaaaaa…)",StringComparison.Ordinal),
                "source status formats singular/plural passage counts and 8-char digests cleanly");

            string oldBetaDigest=sorted[1].Digest;
            ImportFile(engine,"beta.txt","Channel: warm\n"); // identical re-import keeps revision 1
            check(engine.ProjectSources().Single(s=>s.Name=="beta.txt").Version==1,"idempotent re-import preserves revision number");
            var betaV2=ImportFile(engine,"beta.txt","Channel: hot\nMode: offline\n");
            var afterRev2=engine.ProjectSources();
            var betaSummary=afterRev2.Single(s=>s.Name=="beta.txt");
            check(afterRev2.Count==4 && betaV2.Version==2 && betaSummary.Version==2 && betaSummary.Claims==2 && betaSummary.Digest!=oldBetaDigest,
                "re-import updates revision, passage count, and digest without adding a duplicate slot");

            ((TextBox)window.FindName("ProjectName")).Text="sidecar";
            ((Button)window.FindName("SwitchProject")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            check(((TextBlock)window.FindName("SourcesInfo")).Text=="Imported sources (0/32): none in this project.",
                "switching to a clean project clears the visible source inventory");
            ImportFile(engine,"only_b.md","Region: west\n");
            check(engine.ProjectSources().Single() is {Name:"only_b.md",Version:1,Claims:1},
                "isolated project sees only its own imported sources");
            ((TextBox)window.FindName("ProjectName")).Text="default";
            ((Button)window.FindName("SwitchProject")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            string returnedDefault=((TextBlock)window.FindName("SourcesInfo")).Text;
            check(returnedDefault.StartsWith("Imported sources (4/32):",StringComparison.Ordinal) &&
                  returnedDefault.Contains("beta.txt (rev 2, 2 passages,",StringComparison.Ordinal),
                "switching back restores project source inventory display");
            window.Close();
        }

        using(var reopened=new Engine(profile))
        {
            var win=new WorkWindow(reopened,profile);
            win.Show();
            string startText=((TextBlock)win.FindName("SourcesInfo")).Text;
            check(startText.StartsWith("Imported sources (4/32):",StringComparison.Ordinal) &&
                  startText.Contains("beta.txt (rev 2, 2 passages,",StringComparison.Ordinal),
                "reopening persisted profile immediately shows prior source inventory");
            ((TextBox)win.FindName("ProjectName")).Text="ten-pack";
            ((Button)win.FindName("SwitchProject")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            for(int i=1;i<=10;i++) ImportFile(reopened,$"doc{i:D2}.txt",$"Key{i}: value-{i}\n");
            var tenSources=reopened.ProjectSources();
            string tenDisplay=WorkWindow.FormatSources(tenSources);
            check(tenSources.Count==10 &&
                  tenDisplay.StartsWith("Imported sources (10/32): ",StringComparison.Ordinal) &&
                  tenDisplay.Contains("doc08.txt (rev 1, 1 passage,",StringComparison.Ordinal) &&
                  !tenDisplay.Contains("doc09.txt",StringComparison.Ordinal) &&
                  tenDisplay.EndsWith("; (+2 more)",StringComparison.Ordinal),
                "inventory summary bounds visible items to 8 entries and reports overflow count");
            win.Close();
        }

        string quotaProfile=Path.Combine(root,"quota");
        using(var quota=new Engine(quotaProfile)) {
            int rejected=0;
            for(int i=0;i<32;i++) {
                try { ImportFile(quota,$"invalid-{i}.cs","class {"); }
                catch(InvalidDataException) { rejected++; }
            }
            string store=Path.Combine(quotaProfile,"knowledge");
            check(rejected==32 && !Directory.EnumerateDirectories(store).Any(),
                "rejected source inputs reserve no published-source slots");
            for(int i=0;i<33;i++) Directory.CreateDirectory(Path.Combine(store,$"doc-{i:x64}","pending-orphaned"));
            check(quota.ProjectSources().Count==0 && ImportFile(quota,"first.txt","Result: first").Version==1,
                "historical empty and pending folders consume no inventory slots");
            for(int i=0;i<31;i++) ImportFile(quota,$"valid-{i}.txt","Result: next");
            bool limited=false;
            try { ImportFile(quota,"overflow.txt","Result: overflow"); }
            catch(InvalidDataException) { limited=true; }
            check(limited && quota.ProjectSources().Count==32 && ImportFile(quota,"first.txt","Result: revised").Version==2,
                "quota applies to 32 published sources while allowing updates");
        }

        using(var tamperEngine=new Engine(tamperProfile))
        {
            ImportFile(tamperEngine,"tamper.txt","Signal: green\n");
            string storedFile=Directory.GetFiles(Path.Combine(tamperProfile,"knowledge"),"tamper.txt",SearchOption.AllDirectories).Single();
            File.AppendAllText(storedFile," ");
            bool threw=false;
            try { _=tamperEngine.ProjectSources(); }
            catch(InvalidDataException) { threw=true; }
            check(threw,"corrupted stored revision fails closed in Engine.ProjectSources");
            var tamperWindow=new WorkWindow(tamperEngine,tamperProfile);
            tamperWindow.Show();
            check(((TextBlock)tamperWindow.FindName("SourcesInfo")).Text.StartsWith("Imported sources unavailable:",StringComparison.Ordinal),
                "corrupted stored revision displays safe unavailable notice in WorkWindow without crashing");
            tamperWindow.Close();
        }
    }
}
