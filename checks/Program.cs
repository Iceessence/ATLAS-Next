using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using AtlasNext;

internal static class Checks
{
    static int count;
    static void Check(bool ok,string name)
    {
        if(!ok) throw new InvalidOperationException(name);
        Console.WriteLine($"PASS {++count}: {name}");
    }
    static void Click(WorkWindow window,string name) =>
        ((Button)window.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    [STAThread] static int Main()
    {
        var root=Path.Combine(Path.GetTempPath(),"atlas-next-check-"+Guid.NewGuid().ToString("N"));
        var router=new Routing();
        using var golden=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"golden.json")));
        Check(golden.RootElement.EnumerateArray().All(row=>{
            var result=router.Infer(row.GetProperty("prompt").GetString()!);
            return result.Route==row.GetProperty("route").GetString() &&
                   Math.Abs(result.Probability-row.GetProperty("probability").GetDouble())<1e-10;
        }),"all public golden logits-derived predictions match Python to 1e-10");
        var ckpt=Path.Combine(Assets.Root,"native.json");
        var original=File.ReadAllBytes(ckpt);
        try {
            File.AppendAllText(ckpt," ");
            try { _=new Routing(); throw new Exception("Expected checkpoint rejection"); }
            catch(InvalidDataException){ Check(true,"altered checkpoint refused"); }
        } finally {File.WriteAllBytes(ckpt,original);}
        Guid id=Guid.NewGuid(), pending=Guid.NewGuid();
        using(var engine=new Engine(root))
        {
            var work=engine.Submit(id,"What is the archive retention policy?");
            Check(work.State=="review" && work.Evidence?.Id=="fact.retention","persisted scoped fact proposal");
            Check(work.Proposal is {Origin:"original",Qualification:"shadow",Sha256.Length:64},"local original CPU inference");
            Check(engine.Submit(id,work.Request).Events.Length==2,"idempotent task submission");
            try {engine.Submit(id,"different request");throw new Exception("Expected identity conflict");}
            catch(InvalidOperationException){Check(true,"conflicting retry refused");}
            Check(engine.Finish(id).Result!.Contains("30 days"),"verified actual record body");
            Check(engine.Finish(id,true).State=="completed","late cancellation preserves completed evidence");
            var step=engine.Submit(Guid.NewGuid(),"How do I restore an archive?");
            Check(step.Evidence?.Id=="procedure.restore","procedure retrieval");
            Check(engine.Finish(step.Id,true).Result==null && engine.Finish(step.Id).State=="cancelled","cancel prevents delivery");
            Check(engine.Submit(Guid.NewGuid(),"unknown topic").State=="needs clarification","no evidence handling");
            Check(engine.Submit(Guid.NewGuid(),"what is uranium?").Evidence==null,"out of corpus abstention");
            Check(engine.Submit(Guid.NewGuid(),"fact.format").Evidence?.Id=="fact.format","exact ID retrieval");
            engine.Submit(pending,"procedure.restore");
            try { using var second=new Engine(root);throw new Exception("Expected lease denial"); }
            catch(IOException){Check(true,"second writer denied");}
            try { engine.Submit(Guid.NewGuid(),new string('a',2049));throw new Exception("Budget ignored"); }
            catch(ArgumentException){Check(true,"request budget enforced");}
        }
        using(var engine=new Engine(root))
        {
            Check(engine.History().Single(x=>x.Id==pending).State=="interrupted","pending review safely recovered");
            Check(engine.History().Single(x=>x.Id==id).State=="completed","completed result survives reopen");
            var window=new WorkWindow(engine,root);
            window.Show();
            Click(window,"Search"); Click(window,"Verify"); Click(window,"Export");
            var receipt=JsonSerializer.Deserialize<Work>(File.ReadAllText(Directory.GetFiles(Path.Combine(root,"exports")).Single()),Assets.Json)!;
            Check(receipt.State=="completed" && receipt.Evidence?.Id=="fact.retention","real WPF find, verify and export handlers");
            Click(window,"Search"); Click(window,"Stop");
            Check(((TextBlock)window.FindName("Status")).Text=="cancelled","real WPF stop control");
            Click(window,"Search"); Click(window,"Search"); Click(window,"EmergencyStop");
            Check(!engine.History().Any(task=>task.State=="review"),"global stop cancels all pending tasks");
            window.Close();
            var path=Path.Combine(Assets.Root,"library","retention.json");
            var bytes=File.ReadAllBytes(path);
            var task=engine.Submit(Guid.NewGuid(),"fact.retention");
            try {File.AppendAllText(path," "); Check(engine.Finish(task.Id).State=="failed","modified evidence blocks delivery");}
            finally {File.WriteAllBytes(path,bytes);}
        }
        Console.WriteLine($"All {count} checks passed. Temporary profile: {root}");
        return 0;
    }
}
