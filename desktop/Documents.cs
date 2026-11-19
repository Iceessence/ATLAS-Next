using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace AtlasNext;

internal record SpanAnchor(string Path,int Line,int Start,int Length);
internal record Passage(string Subject,string Value,Citation Citation,DateOnly? ValidThrough,bool InstalledAudit=false,string? Symbol=null,string? Qualifier=null,int? DefinitionStart=null,ParameterShape? Parameters=null,string? LiteralSubject=null);
internal record Imported(string Id,int Version,string Name,int Claims);
internal record SourceSummary(string Id,int Version,string Name,int Claims,string Digest);

internal record OwnedSource(string Id,int Version,string Digest,string Path,string Text)
{
    public string Name=>System.IO.Path.GetFileName(Path);
    public string Extension=>System.IO.Path.GetExtension(Path).ToLowerInvariant();
    public Citation Cite(string type,int line,int start,int length,string trust)=>
        new(Id,Version,Digest,Name,type,"owner import",trust,new(Path,line,start,length));
}

internal sealed class Documents(string profile,string project="default")
{
    private readonly string root=Path.GetFullPath(Path.Combine(profile,project=="default" ? "" : Path.Combine("projects","project-"+project),"knowledge"));
    private const int MaxBytes=256*1024, MaxDocuments=32;
    private static readonly UTF8Encoding Utf8=new(false,true);
    private static readonly Regex Claim=new(@"^(?:- )?(?<key>[\p{L}\p{N}][\p{L}\p{N} _-]{0,95}?)(?::[ \t]+| is )(?<value>\S.{0,511}?)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    // The selected Windows handle must name a regular single-link file. Compare its
    // resolved path as well as inspecting ancestors to exclude redirected selections.
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Created,Accessed,Written;
        public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;
    }
    [DllImport("kernel32.dll",SetLastError=true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file,out FileInformation info);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle file,StringBuilder path,uint size,uint flags);

    private static void CheckLocalPath(string path)
    {
        if(!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\") || path.IndexOf(':',2)>=0 ||
            new DriveInfo(Path.GetPathRoot(path)!).DriveType is not (DriveType.Fixed or DriveType.Removable))
            throw new InvalidDataException("Select an ordinary file on a local drive.");
        for(string? current=path;current!=null;current=Path.GetDirectoryName(current))
            if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)
                throw new InvalidDataException("Linked paths are outside the document store.");
    }

    private static void CreateLocalDirectory(string path)
    {
        if(!Directory.Exists(path)) {
            CreateLocalDirectory(Path.GetDirectoryName(path) ?? throw new InvalidDataException("Missing local drive."));
            Directory.CreateDirectory(path);
        }
        CheckLocalPath(path);
    }

    private static byte[] ReadFile(string path)
    {
        CheckLocalPath(path);
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        var final=new StringBuilder(32768);
        uint length=GetFinalPathNameByHandle(stream.SafeFileHandle,final,(uint)final.Capacity,0);
        if(!GetFileInformationByHandle(stream.SafeFileHandle,out var info) || info.Links!=1 ||
            length==0 || length>=final.Capacity || !string.Equals(final.ToString(),@"\\?\"+path,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Linked or unresolved document.");
        if(stream.Length is 0 or > MaxBytes) throw new InvalidDataException("Document must be 1 byte to 256 KiB.");
        var bytes=new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static string Decode(byte[] bytes)
    {
        try {
            string text=Utf8.GetString(bytes);
            if(text.Any(c=>char.IsControl(c) && c is not ('\t' or '\r' or '\n')))
                throw new InvalidDataException("Control characters in document.");
            return text;
        }
        catch(DecoderFallbackException ex) { throw new InvalidDataException("Document must use UTF-8.",ex); }
    }

    public Imported Import(string selected)
    {
        string path=Path.GetFullPath(selected);
        string ext=Path.GetExtension(path).ToLowerInvariant();
        if(ext is not (".txt" or ".md" or ".cs" or ".py")) throw new InvalidDataException("Select a TXT, Markdown, C# or Python file.");
        var bytes=ReadFile(path);
        string digest=Convert.ToHexStringLower(SHA256.HashData(bytes));
        string id="doc-"+Convert.ToHexStringLower(SHA256.HashData(Utf8.GetBytes(path.ToUpperInvariant())));
        CreateLocalDirectory(root);
        var directory=Path.Combine(root,id);
        if(Directory.Exists(directory)) CheckLocalPath(directory);
        var previous=Directory.Exists(directory) ? Latest(directory) : null;
        int version=previous?.Version ?? 0;
        bool same=previous?.Digest==digest;
        if(!same) version=checked(version+1);
        string revision=Path.Combine(directory,$"{version:D8}-{digest}");
        string name=Path.GetFileName(path);
        var claims=Parse(new(id,version,digest,Path.GetRelativePath(profile,Path.Combine(revision,name)),Decode(bytes)));
        if(previous==null && Published().Take(MaxDocuments).Count()>=MaxDocuments)
            throw new InvalidDataException("Profile document limit reached (32).");
        if(same) _=ReadRevision(directory,previous!.Value); // Verify an idempotent re-import before reporting success.
        else {
            CreateLocalDirectory(directory);
            // Only fully written revision directories are published into the active index.
            string stage=Path.Combine(directory,"pending-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            using(var stream=new FileStream(Path.Combine(stage,name),FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                stream.Write(bytes); stream.Flush(true);
            }
            Directory.Move(stage,revision);
        }
        return new(id,version,name,claims.Count);
    }

    private static (string Path,int Version,string Digest)? Latest(string directory)
    {
        var revisions=Directory.GetDirectories(directory).Where(p=>!Path.GetFileName(p).StartsWith("pending-",StringComparison.Ordinal)).Order(StringComparer.Ordinal).ToArray();
        if(revisions.Length==0) return null;
        var path=revisions[^1];
        var fields=Path.GetFileName(path).Split('-');
        if(fields.Length!=2 || fields[0].Length!=8 || !int.TryParse(fields[0],NumberStyles.None,CultureInfo.InvariantCulture,out int v) ||
            v<1 || fields[1].Length!=64 || !fields[1].All(Uri.IsHexDigit))
            throw new InvalidDataException("Invalid knowledge revision.");
        return (path,v,fields[1]);
    }

    public IReadOnlyList<Passage> Active()=>Sources().SelectMany(Parse).ToArray();

    public IReadOnlyList<OwnedSource> Sources(IReadOnlySet<string>? allowed=null)
    {
        if(!Directory.Exists(root)) return [];
        CheckLocalPath(root);
        var entries=Published(allowed).Take(MaxDocuments+1).ToArray();
        if(entries.Length>MaxDocuments) throw new InvalidDataException("Too many stored documents.");
        return entries.Select(entry=>ReadRevision(entry.Directory,entry.Revision)).ToArray();
    }

    public IReadOnlyList<SourceSummary> Inventory()=>Sources()
        .Select(source=>new SourceSummary(source.Id,source.Version,source.Name,Parse(source).Count,source.Digest))
        .OrderBy(item=>item.Name,StringComparer.OrdinalIgnoreCase)
        .ThenBy(item=>item.Id,StringComparer.Ordinal)
        .ToArray();

    private IEnumerable<(string Directory,(string Path,int Version,string Digest) Revision)> Published(IReadOnlySet<string>? allowed=null)
    {
        foreach(var directory in Directory.EnumerateDirectories(root,"doc-*").Order(StringComparer.Ordinal)) {
            if(allowed!=null && !allowed.Contains(Path.GetFileName(directory))) continue;
            CheckLocalPath(directory);
            if(Latest(directory) is {} revision) yield return (directory,revision);
        }
    }

    private OwnedSource ReadRevision(string directory,(string Path,int Version,string Digest) rev)
    {
        CheckLocalPath(rev.Path);
        var files=Directory.GetFiles(rev.Path);
        if(files.Length!=1) throw new InvalidDataException("Incomplete knowledge revision.");
        var bytes=ReadFile(files[0]);
        if(!string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)),rev.Digest,StringComparison.Ordinal))
            throw new InvalidDataException("Stored document digest mismatch.");
        return new(Path.GetFileName(directory),rev.Version,rev.Digest,Path.GetRelativePath(profile,files[0]),Decode(bytes));
    }

    public static List<Passage> Parse(OwnedSource source)
    {
        var claims=new List<Passage>();
        string text=source.Text, ext=source.Extension;
        if(ext is ".cs" or ".py") {
            int count=0;
            foreach(var d in SourceDeclarations.Read(text,ext)) {
                if(++count>256) throw new InvalidDataException("Source exceeds 256 declarations.");
                void Add(string type,SourcePart? part) {
                    if(part==null) return;
                    var citation=source.Cite(type,part.Line,part.Start,part.End-part.Start,"owner-selected source");
                    claims.Add(new(d.Name,text[part.Start..part.End],citation,null,Symbol:d.Name,Qualifier:d.Qualifier,
                        DefinitionStart:d.Identifier.Start,Parameters:d.Parameters));
                }
                Add("code.definition",d.Identifier);
                Add("code.signature",d.Header);
                Add("code.container",d.Container);
                if(d.ContainerIsClass) Add("code.class",d.Container);
                Add("code.documentation",d.Documentation);
                Add("code.summary",d.Summary);
                Add("code.namespace",d.Namespace);
                Add("code.parameters",d.ParametersSpan);
                Add("code.return",d.ReturnType);
            }
            return claims;
        }
        DateOnly? valid=null;
        int line=0;
        string? fence=null;
        bool installedAudit=false;
        foreach(Match row in Regex.Matches(text,@"[^\r\n]*(?:\r\n|\r|\n|\z)")) {
            line++;
            string raw=row.Value.TrimEnd('\r','\n');
            int bom=row.Index==0 && raw.StartsWith('\uFEFF') ? 1 : 0;
            string content=raw[bom..];
            var marker=Regex.Match(content,@"^\s*(\x60{3,}|~{3,})");
            if(marker.Success) {
                string run=marker.Groups[1].Value;
                if(fence==null) fence=run;
                else if(run.StartsWith(fence,StringComparison.Ordinal) && content.Trim()==run) fence=null;
                continue;
            }
            if(fence!=null) continue;
            if(Regex.IsMatch(content,@"^\s*#{1,6}(?:\s|$)")) {
                installedAudit=Regex.IsMatch(content.Trim(),@"^#{1,6}\s+(?:[\w.-]+ — )?Installed payload audit$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                continue;
            }
            if(content.StartsWith("Valid through:",StringComparison.OrdinalIgnoreCase)) {
                if(valid!=null || !DateOnly.TryParseExact(content[14..].Trim(),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))
                    throw new InvalidDataException("Supply one Valid through date in yyyy-MM-dd form.");
                valid=date;
                continue;
            }
            var match=Claim.Match(content);
            if(!match.Success) continue;
            var value=match.Groups["value"];
            string key=match.Groups["key"].Value;
            string subject=Research.Subject(key);
            var citation=source.Cite("fact",line,row.Index+bom+value.Index,value.Length,"owner-selected text");
            claims.Add(new(subject,value.Value,citation,null,installedAudit,LiteralSubject:Research.LiteralSubject(key)));
            if(claims.Count>256) throw new InvalidDataException("Document exceeds 256 indexed claims.");
        }
        return claims.Select(p=>p with {ValidThrough=valid}).ToList();
    }
}
