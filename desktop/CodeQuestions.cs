using System.Text.RegularExpressions;

namespace AtlasNext;

internal sealed record CodeQuestion(string? Name,bool FollowUp)
{
    private const string Identifier=@"(?<name>[\p{L}_][\p{L}\p{N}\p{M}_]*)";
    private static readonly Regex Locate=new(@"\A(?:please )?(?:where is (?:the symbol )?"+Identifier+
        @" (?:defined|declared)|(?:locate|find the definition of) "+Identifier+@")(?:,? please)?[?.!]?\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Signature=new(@"\A(?:please )?(?:what is its signature|show (?:me )?its signature|what is its declaration signature)(?:,? please)?[?.!]?\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static CodeQuestion? Parse(string request)
    {
        string query=Regex.Replace(request.Trim(),@"\s+"," ");
        if(Signature.IsMatch(query)) return new(null,true);
        var locate=Locate.Match(query);
        return locate.Success ? new(locate.Groups["name"].Value,false) : null;
    }

    // A signature uses only the explicitly selected completed definition. Re-read
    // the parent receipt and resolve its citation against this project's active data.
    public static Passage? Parent(Work? parent,string project,IReadOnlyList<Passage> active)
    {
        if(parent is not {State:"completed",Specialist:"code.definition",Authorization:"allowed scoped record read",
            Scope:"owner-local",SourceIntegrity:"passed",AnswerRelevance:"supported",Evidence:{} cite} ||
            parent.Project!=project || Parse(parent.Request) is not {FollowUp:false,Name:{} name}) return null;
        return active.FirstOrDefault(p=>p.Symbol==name && p.Citation.Type=="code.definition" && p.Citation==cite);
    }

    public Finding Verify(IReadOnlyList<Passage> active,Passage? parent)
    {
        string? requested=FollowUp ? parent?.Symbol : Name;
        string type=FollowUp ? "code.signature" : "code.definition";
        var matches=active.Where(p=>p.Symbol!=null && p.Symbol==requested && p.Citation.Type==type).Take(9).ToArray();
        var citations=matches.Take(8).Select(p=>p.Citation).ToArray();
        Finding Stop(string verdict,string detail)=>new(new(verdict,detail,"active project revisions","owner-selected source",citations),null);
        if(matches.Length==0) return Stop("unsupported",FollowUp ?
            "Select a completed definition from the current source revision first." : "No declaration matches this identifier in the active project.");
        if(matches.Length!=1) return Stop("ambiguous","Multiple active declarations use that identifier.");
        var match=matches[0];
        if(FollowUp && (parent!.Citation.Id!=match.Citation.Id || parent.Citation.Version!=match.Citation.Version ||
            parent.Citation.Digest!=match.Citation.Digest)) return Stop("unsupported","The selected declaration revision changed.");
        return new(new("supported","Unique source declaration supports the requested identifier and turn.",
            "active project revision","owner-selected source",citations),Research.FromPassage(match));
    }
}
