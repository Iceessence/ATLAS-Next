using System.Text.RegularExpressions;

namespace AtlasNext;

internal record Assessment(string Verdict,string Detail,string Freshness,string Trust,Citation[] Sources,ClaimAssessment[]? Claims=null,string[]? ChangedClaims=null)
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
    public static string LiteralSubject(string text)=>Regex.Replace(text.Trim().ToLowerInvariant(),@"\s+"," ");

    public static string Subject(string text)
    {
        string key=LiteralSubject(text);
        if(key.StartsWith("the ")) key=key[4..];
        return key switch {
            "archive retention policy" or "archive retention duration" or "archive retention period" => "archive retention",
            "export receipt encoding" or "export format" or "archive format" => "export receipt format",
            _=>key
        };
    }

    // Only these complete question forms assert the named relation. Captured modifiers
    // remain part of the lookup key. Primary never supplies a key to this grammar.
    private const string Entity=@"(?<entity>[\p{L}\p{N}][\p{L}\p{N} _-]{0,95}?)";
    private record Lookup(string Key,bool Numeric=false,bool InstalledAudit=false,bool Version=false);
    private record Form(Regex Pattern,string Suffix,bool Numeric=false);
    private static Form Question(string pattern,string suffix="",bool numeric=false)=>new(
        new Regex(@"\A"+pattern+@"\z",RegexOptions.CultureInvariant | RegexOptions.NonBacktracking),suffix,numeric);
    private static readonly Form[] Forms=[
        Question(@"(?:what|which) (?:arguments|parameters) does (?:the )?"+Entity+@" (?:accept|take)"," parameters"),
        Question(@"(?:what|which) "+Entity+@" version does (?:this|the) (?:installation|package) require"," requirement"),
        Question(@"(?:which|what) (?:backend|tool) (?:produced|generated|built) (?:this|the) "+Entity," generator"),
        Question(@"where is (?:the )?"+Entity+@" (?:defined|declared)"," declaration line",true),
        Question(@"(?:what|which) (?:line|line number) is (?:the )?"+Entity+@" (?:defined|declared) on"," declaration line",true),
        Question(@"what is (?:the )?"+Entity+@" (?:set to|recorded as|listed as)"),
        Question(@"(?:which|what) (?:value|setting) (?:is|was) (?:listed|recorded) for (?:the )?"+Entity),
        Question(@"(?:which|what) "+Entity+@" (?:is|was) (?:listed|recorded)"),
        Question(@"(?:what is the value of|which value is recorded for|what is|state|name|give me) (?:the )?"+Entity)
    ];
    private static readonly Regex LiteralLookup=new(@"\A(?:please )?(?:what is|which value is recorded for|state|name|give me) (?:the )?"+Entity+@"[?.!]?\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex AuditCount=new(@"\Ahow many "+Entity+@" files did the audit check\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private static Lookup? RequestedSubject(string request)
    {
        string query=Regex.Replace(request.Trim().ToLowerInvariant(),@"\s+"," ");
        query=Regex.Replace(query,@"(?:,? please)?[?.!]?\z","");
        if(query.StartsWith("please ",StringComparison.Ordinal)) query=query[7..];
        var count=AuditCount.Match(query);
        if(count.Success) {
            var kind=count.Groups["entity"].Value;
            return kind=="installed" ? new("checked payload files",true,true) : new("checked "+kind+" files",true);
        }
        foreach(var form in Forms) {
            var match=form.Pattern.Match(query);
            if(match.Success) return new(Subject(match.Groups["entity"].Value+form.Suffix),form.Numeric,Version:form.Suffix==" requirement");
        }
        return null;
    }

    private static (string Key,string Value) HandbookClaim(Hit hit)=>hit.Record.Id switch {
        "fact.retention"=>("archive retention","30 days"),
        "fact.format"=>("export receipt format","UTF-8 JSON"),
        _=>("archive recovery steps",hit.Record.Body)
    };

    public static string CatalogSubject(Hit hit)=>HandbookClaim(hit).Key;

    public static Hit FromPassage(Passage p)=>new(new Knowledge(p.Citation.Id,p.Citation.Version,p.Citation.Type,p.Subject,
        p.Value,[],"owner-local",p.Citation.Origin,p.Citation.Trust,p.Citation.Source),p.Citation);

    private static bool Unconfirmed(string value)=>Regex.IsMatch(value.Trim(),
        @"\A(?:unknown|none recorded|unconfirmed|undetermined|not specified|tbd|to be determined|n/a|\?)[.!]?\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string ValueVerdict(IReadOnlyList<string> values,bool stale,bool literal=true)
    {
        if(stale) return "stale";
        static string Normalize(string value)=>Regex.Replace(value.Trim().TrimEnd('.').TrimEnd(),@"\s+"," ");
        var definite=values.Where(v=>!Unconfirmed(v)).ToArray();
        var comparable=literal ? definite : definite.Select(Normalize);
        if(comparable.Distinct(literal ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase).Skip(1).Any())
            return "contradiction";
        return definite.Length==0 || definite.Length!=values.Count ? "insufficient" : "supported";
    }

    public static Finding Assess(string request,Library library,IReadOnlyList<Passage> documents)
    {
        // Verify the subject independently of Primary's route, including when a caller
        // supplies an authentic but irrelevant citation. Consider every active source.
        var records=library.All().ToList();
        var handbook=records.SingleOrDefault(h=>Relevance.Supports(request,h));
        var lookup=handbook==null ? RequestedSubject(request) : new Lookup(HandbookClaim(handbook).Key);
        string? subject=lookup?.Key;
        handbook??=records.SingleOrDefault(h=>h.Record.Type=="fact" && HandbookClaim(h).Key==subject);
        var passages=documents.Where(p=>p.Subject==subject).ToArray();
        // A helper can itself be part of a literal indexed name. If both
        // readings have evidence, a user must disambiguate before delivery.
        string normalized=Regex.Replace(request.Trim().ToLowerInvariant(),@"\s+"," ");
        string courteous=Regex.Replace(normalized,@"(?:,? please)?[?.!]?\z","");
        var otherKeys=new[]{normalized,courteous}.Distinct().Select(s=>LiteralLookup.Match(s))
            .Where(m=>m.Success).Select(m=>Subject(m.Groups["entity"].Value)).Where(key=>key!=subject).ToHashSet();
        var alternative=documents.Where(p=>otherKeys.Contains(p.Subject)).ToArray();
        if(alternative.Length>0) {
            var both=passages.Concat(alternative).Select(p=>p.Citation);
            if(handbook!=null) both=both.Prepend(handbook.Citation);
            return new(new("ambiguous","The wording can also name a distinct literal project subject.",
                "undetermined","see cited source metadata",both.Take(8).ToArray()),null);
        }
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
        var valueVerdict=ValueVerdict(values,false,literal:false);
        if(valueVerdict=="contradiction")
            return Stop("contradiction","Active sources give different definite values for the same subject.");
        if(valueVerdict=="insufficient")
            return Stop("insufficient","At least one matching source leaves the value unconfirmed.");
        if(lookup?.Numeric==true && values.Any(v=>!Regex.IsMatch(v.Trim(),@"\A[0-9]+\.?\z",RegexOptions.CultureInvariant)))
            return Stop("insufficient","The requested number is not explicitly stated.");
        if(lookup?.Version==true && values.Any(v=>!Regex.IsMatch(v.Trim(),@"\A(?:(?:===|==|!=|~=|>=|<=|>|<)?\s*[0-9]+(?:\.(?:[0-9]+|\*))*)(?:\s*,\s*(?:===|==|!=|~=|>=|<=|>|<)?\s*[0-9]+(?:\.(?:[0-9]+|\*))*)*\.?\z",RegexOptions.CultureInvariant)))
            return Stop("insufficient","The requirement does not explicitly give a version or version constraint.");
        if(lookup?.InstalledAudit==true && passages.Any(p=>!p.InstalledAudit))
            return Stop("insufficient","The payload count lacks installed-payload audit context.");
        var chosen=handbook ?? FromPassage(passages[0]);
        string freshness=passages.Length==0 ? "pinned revision" : passages.Any(p=>p.ValidThrough==null) ?
            "active revision; undated source" : $"active revision; valid through {passages.Min(p=>p.ValidThrough):yyyy-MM-dd}";
        return new(new("supported","Exact subject and lookup intent supported by the listed source passages.",freshness,chosen.Citation.Trust,sources),chosen);
    }
}
