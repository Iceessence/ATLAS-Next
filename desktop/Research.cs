using System.Text.RegularExpressions;

namespace AtlasNext;

internal record Assessment(string Verdict,string Detail,string Freshness,string Trust,Citation[] Sources)
{
    public string RequestRelevance => Verdict switch {
        "supported" => "supported",
        "contradiction" or "stale" or "insufficient" or "ambiguous" => "subject and intent matched",
        _ => "unsupported"
    };
    public string FactualSupport => Verdict=="supported" ? "stated by cited sources" : "insufficient for delivery";
}
internal record Finding(Assessment Assessment,Hit? Source);

internal static class Research
{
    public static string Subject(string text)
    {
        string key=Regex.Replace(text.Trim().ToLowerInvariant(),@"\s+"," ");
        if(key.StartsWith("the ")) key=key[4..];
        return key switch {
            "archive retention policy" or "archive retention duration" or "archive retention period" => "archive retention",
            "export receipt encoding" or "export format" or "archive format" => "export receipt format",
            _=>key
        };
    }

    private static string? RequestedSubject(string request)
    {
        // Only whole affirmative lookup questions are supported. All modifiers are
        // retained in the key. No imported sentence is interpreted as an instruction.
        var m=Regex.Match(request.Trim(),@"\A(?:please )?(?:what is|which value is recorded for|state|name|give me) (?:the )?(?<key>[\p{L}\p{N}][\p{L}\p{N} _-]{0,95})[?.!]?\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
        return m.Success ? Subject(m.Groups["key"].Value) : null;
    }

    private static (string Key,string Value) HandbookClaim(Hit hit)=>hit.Record.Id switch {
        "fact.retention"=>("archive retention","30 days"),
        "fact.format"=>("export receipt format","UTF-8 JSON"),
        _=>("archive recovery steps",hit.Record.Body)
    };

    public static string CatalogSubject(Hit hit)=>HandbookClaim(hit).Key;

    public static Hit FromPassage(Passage p)=>new(new Knowledge(p.Citation.Id,p.Citation.Version,"fact",p.Subject,
        p.Value,[],"owner-local",p.Citation.Origin,p.Citation.Trust,p.Citation.Source),p.Citation);

    public static Finding Assess(string request,Library library,IReadOnlyList<Passage> documents)
    {
        // Verify the subject independently of Primary's route, including when a caller
        // supplies an authentic but irrelevant citation. Consider every active source.
        var records=library.All().ToList();
        var handbook=records.SingleOrDefault(h=>Relevance.Supports(request,h));
        string? subject=handbook==null ? RequestedSubject(request) : HandbookClaim(handbook).Key;
        handbook??=records.SingleOrDefault(h=>h.Record.Type=="fact" && HandbookClaim(h).Key==subject);
        var passages=documents.Where(p=>p.Subject==subject).ToArray();
        var citations=passages.Select(p=>p.Citation).ToList();
        if(handbook!=null) citations.Insert(0,handbook.Citation);
        var sources=citations.Take(8).ToArray();
        Finding Stop(string verdict,string detail,string freshness="undetermined")=>
            new(new(verdict,detail,freshness,"see cited source metadata",sources),null);

        if(citations.Count==0) return Stop("unsupported","No current source explicitly supports the requested subject and intent.");
        if(citations.Count>8) return Stop("ambiguous","More than eight passages match. Narrow the subject.");
        var today=DateOnly.FromDateTime(DateTime.UtcNow);
        if(passages.Any(p=>p.ValidThrough<today)) return Stop("stale","A matching source is past its declared validity date.","expired");
        var values=passages.Select(p=>p.Value).ToList();
        if(handbook!=null) values.Add(HandbookClaim(handbook).Value);
        static string Normalize(string v)=>Regex.Replace(v.Trim().TrimEnd('.').TrimEnd(),@"\s+"," ");
        bool Unconfirmed(string v)=>Regex.IsMatch(v.Trim(),@"\A(?:unknown|none recorded|unconfirmed|undetermined|not specified|tbd|to be determined|n/a|\?)[.!]?\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var definite=values.Where(v=>!Unconfirmed(v)).ToArray();
        if(definite.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).Skip(1).Any())
            return Stop("contradiction","Active sources give different definite values for the same subject.");
        if(definite.Length!=values.Count)
            return Stop("insufficient","At least one matching source leaves the value unconfirmed.");
        var chosen=handbook ?? FromPassage(passages[0]);
        string freshness=passages.Length==0 ? "pinned revision" : passages.Any(p=>p.ValidThrough==null) ?
            "active revision; undated source" : $"active revision; valid through {passages.Min(p=>p.ValidThrough):yyyy-MM-dd}";
        return new(new("supported","Exact subject and lookup intent supported by the listed source passages.",freshness,chosen.Citation.Trust,sources),chosen);
    }
}
