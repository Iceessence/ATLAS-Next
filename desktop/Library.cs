using System.IO;

namespace AtlasNext;

internal record Knowledge(string Id,int Version,string Type,string Title,string Body,string[] Terms,
    string Scope,string Origin,string Trust,string Source);
internal record Citation(string Id,int Version,string Digest,string Source,string Type,string Origin,string Trust,SpanAnchor? Span=null);
internal record Hit(Knowledge Record,Citation Citation);

internal sealed class Library
{
    private readonly Dictionary<string,string> paths=new(StringComparer.OrdinalIgnoreCase);
    public Library()
    {
        foreach(var path in Assets.Records)
        {
            var hit=Load(path);
            paths.Add(hit.Record.Id,path);
        }
    }

    private static Hit Load(string path)
    {
        var (r,digest)=Assets.Read<Knowledge>(path);
        if(r.Scope!="public-demo" || r.Type is not ("fact" or "procedure") || r.Version<1)
            throw new InvalidDataException("Record outside demo corpus");
        return new(r,new(r.Id,r.Version,digest,r.Source,r.Type,r.Origin,r.Trust));
    }

    public IEnumerable<Hit> All()=>paths.Values.Select(Load);

    public Hit? Exact(string id) => paths.TryGetValue(id,out var path) ? Load(path) : null;

    public Hit? Find(string query,string route)
    {
        var words=Assets.Tokens(query).ToHashSet();
        // A shared domain noun such as "archive" does not distinguish catalog entries.
        var matches=paths.Values.Select(Load).Where(hit=>hit.Record.Id switch {
            "fact.retention" => words.Overlaps(["retention","retained","retains","kept","keep","expiry","days","duration"]),
            "fact.format" => words.Overlaps(["format","formatted","encoding"]),
            "procedure.restore" => words.Overlaps(["restore","restored","recover","recovered","recovery","restoration"]),
            _ => false
        }).ToArray();
        return matches.Length==1 && matches[0].Record.Type==route ? matches[0] : null;
    }
}
