using System.IO;
using System.Text;

namespace AtlasNext;

internal static class WorkspaceState
{
    internal static void Restore(Engine engine,string profile)
    {
        try {
            using var stream=File.OpenRead(Path.Combine(profile,"active-project.txt"));
            if(stream.Length is <1 or >32) return;
            byte[] bytes=new byte[(int)stream.Length];
            stream.ReadExactly(bytes);
            engine.SelectProject(Encoding.ASCII.GetString(bytes));
        } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException) {
            // The project picker stays available if the saved preference is missing or unreadable.
        }
    }

    internal static void Remember(string profile,string project)
    {
        string path=Path.Combine(profile,"active-project.txt");
        string stage=path+"."+Guid.NewGuid().ToString("N")+".pending";
        using(var stream=new FileStream(stage,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
            stream.Write(Encoding.ASCII.GetBytes(project));
            stream.Flush(true);
        }
        File.Move(stage,path,overwrite:true);
    }
}
