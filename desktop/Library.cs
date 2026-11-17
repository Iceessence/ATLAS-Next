using System.IO;

namespace AtlasNext;

internal record Knowledge(string Id,int Version,string Type,string Title,string Body,string[] Terms,
    string Scope,string Origin,string Trust,string Source);
internal record Citation(string Id,int Version,string Digest,string Source,string Type,string Origin,string Trust);
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

    public Hit? Exact(string id) => paths.TryGetValue(id,out var path) ? Load(path) : null;

    public Hit? Find(string query,string route)
    {
        var words=Assets.Tokens(query).ToHashSet();
        var matches=paths.Values.Select(Load).Where(x=>x.Record.Type==route)
            .Select(x=>(hit:x, score:x.Record.Terms.Count(words.Contains)))
            .Where(x=>x.score>0).OrderByDescending(x=>x.score).Take(2).ToArray();
        return matches.Length==0 || matches.Length==2 && matches[0].score==matches[1].score ? null : matches[0].hit;
    }
}
