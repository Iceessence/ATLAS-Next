using System.Text.RegularExpressions;

namespace AtlasNext;

internal sealed record SelectedDefinition(CodeQuestion Request,Passage Passage);
internal sealed record CodeQuestion(string? Name,string? Qualifier,string Type,string? Extension=null,ParameterFilter? Selector=null)
{
    public bool FollowUp=>Type is not ("code.definition" or "code.calls");
    public bool Calls=>Type=="code.calls";
    private const string Id=@"[\p{L}_][\p{L}\p{N}\p{M}_]*";
    private const string Symbol=@"(?:(?<owner>"+Id+@")\.)?(?<name>"+Id+")";
    private const string Signature=@"(?:\((?<selector>[^()]{0,160})\))?";
    private const RegexOptions Options=RegexOptions.IgnoreCase|RegexOptions.CultureInvariant;
    private static readonly Regex Locate=new(@"\A(?:please )?(?:where is (?:the symbol )?"+Symbol+Signature+
        @" (?<operation>defined|declared|called)|(?:locate|find the definition of) "+Symbol+Signature+@")(?:,? please)?[?.!]?\z",Options);
    private static readonly Regex Follow=new(@"\A(?:please )?(?:what is|show(?: me)?) its (?<relation>signature|declaration signature|documentation|docstring|xml documentation|summary|enclosing type|enclosing class|enclosing namespace|namespace)(?:,? please)?[?.!]?\z",Options);

    public static CodeQuestion? Parse(string request)
    {
        string query=Regex.Replace(request.Trim(),@"\s+"," ");
        var follow=Follow.Match(query);
        if(follow.Success) return follow.Groups["relation"].Value.ToLowerInvariant() switch {
            "signature" or "declaration signature"=>new(null,null,"code.signature"),
            "docstring"=>new(null,null,"code.documentation",".py"),
            "xml documentation"=>new(null,null,"code.documentation",".cs"),
            "documentation"=>new(null,null,"code.documentation"),
            "summary"=>new(null,null,"code.summary",".cs"),
            "namespace" or "enclosing namespace"=>new(null,null,"code.namespace",".cs"),
            "enclosing class"=>new(null,null,"code.class"),
            _=>new(null,null,"code.container")
        };
        var locate=Locate.Match(query);
        if(!locate.Success) return null;
        var owner=locate.Groups["owner"];
        var selector=locate.Groups["selector"];
        var filter=selector.Success ? ParameterFilter.Parse(selector.Value) : null;
        bool calls=string.Equals(locate.Groups["operation"].Value,"called",StringComparison.OrdinalIgnoreCase);
        if(selector.Success && (filter==null || !owner.Success) || calls && filter==null) return null;
        return new(locate.Groups["name"].Value,owner.Success ? owner.Value : null,calls ? "code.calls" : "code.definition",Selector:filter);
    }

    private bool Names(Passage p)=>p.Citation.Type=="code.definition" && p.Symbol==Name && (Qualifier==null || Qualifier==p.Qualifier);
    private bool Matches(Passage p)=>Names(p) && (Selector==null || Selector.Matches(p.Parameters));

    public static SelectedDefinition? Parent(Work? parent,string project,IReadOnlyList<Passage> active)
    {
        if(parent is not {State:"completed",Kind:"code.lookup",ResearchReview.Verdict:"supported",Specialist:"code.definition",Authorization:"allowed scoped record read",
            Scope:"owner-local",SourceIntegrity:"passed",AnswerRelevance:"supported",Evidence:{} cite} ||
            parent.Project!=project || parent.PrimaryCandidateId!=cite.Id || !parent.ResearchReview.Sources.Contains(cite) || Parse(parent.Request) is not {Type:"code.definition"} original) return null;
        var passage=active.FirstOrDefault(p=>original.Matches(p) && p.Citation==cite);
        return passage==null || parent.Result!=passage.Value ? null : new(original,passage);
    }

    public Finding Verify(IReadOnlyList<Passage> active,SelectedDefinition? parent)
    {
        Finding Stop(string verdict,string detail,Citation[] sources)=>new(new(verdict,detail,"active project revisions","owner-selected source",sources),null);
        if(FollowUp && parent==null) return Stop("unsupported","Select a completed definition from the current source revision first.",[]);
        var lookup=FollowUp ? parent!.Request : this;
        var definitions=active.Where(lookup.Matches).Take(9).ToArray();
        if(lookup.Selector!=null && definitions.Length==1 && definitions[0].Citation.Source.EndsWith(".py",StringComparison.OrdinalIgnoreCase)) {
            // Python redefinitions can replace an earlier binding. Arity is
            // a declaration filter only when the lexical name itself is unique.
            var named=active.Where(lookup.Names).Take(9).ToArray();
            if(named.Length>1) definitions=named;
        }
        if(definitions.Length==0) return Stop("unsupported","No declaration matches this identifier in the active project.",[]);
        if(definitions.Length>1) {
            var shown=definitions.Take(8).ToArray();
            var locations=shown.Select(p=>$"{(p.Qualifier==null ? "" : p.Qualifier+".")}{p.Symbol} in {p.Citation.Source}, line {p.Citation.Span!.Line} ({p.Citation.Id[..Math.Min(12,p.Citation.Id.Length)]})");
            return Stop("ambiguous","Multiple declarations match. "+string.Join("; ",locations)+(definitions.Length>8 ? ". Additional matches omitted." : "."),
                shown.Select(p=>p.Citation).ToArray());
        }
        var definition=definitions[0];
        var match=definition;
        if(FollowUp) {
            if(parent!.Passage.Citation!=definition.Citation) return Stop("unsupported","The selected declaration revision changed.",[]);
            match=active.FirstOrDefault(p=>p.Citation.Id==definition.Citation.Id && p.Citation.Version==definition.Citation.Version &&
                p.Citation.Digest==definition.Citation.Digest && p.DefinitionStart==definition.DefinitionStart && p.Citation.Type==Type &&
                (Extension==null || p.Citation.Source.EndsWith(Extension,StringComparison.OrdinalIgnoreCase)));
            if(match==null) return Stop("unsupported","This declaration has no supported source span for that follow-up.",[]);
        }
        return new(new("supported","Unique source declaration supports the requested identifier and turn.",
            "active project revision","owner-selected source",[match.Citation]),Research.FromPassage(match));
    }
}
