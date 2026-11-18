using System.IO;
using System.Text.Json.Serialization;

namespace AtlasNext;

internal record Weights(double[][] W,double[] B,double[][] V,double[] C);
internal record Manifest(string Id,string Origin,string Qualification,string[] Routes,
    [property: JsonPropertyName("checkpoint_sha256")] string Digest);
internal record Recommendation(string Model,string Origin,string Qualification,string Sha256,string Route,double Probability);

internal sealed class Routing
{
    private readonly Weights weights;
    private readonly Manifest manifest;
    public Routing()
    {
        (weights,var hash) = Assets.Read<Weights>("native.json");
        manifest = Assets.Read<Manifest>("native.manifest.json").value;
        if (hash != manifest.Digest || manifest.Qualification != "shadow" || manifest.Origin != "original" ||
            !manifest.Routes.SequenceEqual(new[]{"fact","procedure","clarify"}) ||
            weights.W.Length!=256 || weights.W.Any(x=>x.Length!=16) || weights.B.Length!=16 ||
            weights.V.Length!=16 || weights.V.Any(x=>x.Length!=3) || weights.C.Length!=3)
            throw new InvalidDataException("Invalid original policy release contract");
    }

    public Recommendation Infer(string text)
    {
        var indices = new HashSet<int>();
        foreach (var word in Assets.Tokens(text))
        {
            uint hash = 2166136261;
            foreach (var c in word) hash = unchecked((hash ^ c) * 16777619);
            indices.Add((int)(hash % 256));
        }
        var logits = weights.C.ToArray();
        double norm = Math.Sqrt(Math.Max(1, indices.Count));
        for (int j=0;j<weights.B.Length;j++)
        {
            double h = Math.Tanh(weights.B[j]+indices.Sum(i=>weights.W[i][j])/norm);
            for(int k=0;k<logits.Length;k++) logits[k]+=h*weights.V[j][k];
        }
        double max = logits.Max();
        int index = Array.IndexOf(logits,max);
        return new(manifest.Id,manifest.Origin,manifest.Qualification,manifest.Digest,
            manifest.Routes[index],1/logits.Sum(x=>Math.Exp(x-max)));
    }

    // Interpret the part of the request outside the project subject. A word in a
    // project identifier has no authority to turn a value lookup into a procedure.
    public static Passage? ProjectFact(string request,IEnumerable<Passage> candidates)
    {
        var query=System.Text.RegularExpressions.Regex.Replace(request.Trim().ToLowerInvariant(),@"\s+"," ");
        query=System.Text.RegularExpressions.Regex.Replace(query,@"(?:,? please)?[?.!]?\z","");
        var passages=candidates.OrderByDescending(p=>p.Subject.Length).ToArray();
        // Preserve source qualifiers before applying search-term aliases. Use whole
        // noun phrases so "wheel" alone cannot eclipse a backend/generator question.
        var lookup=System.Text.RegularExpressions.Regex.Match(query,
            @"\A(?:please )?(?:what is(?: the value of)?|which value is recorded for|state|name|give me) (?:the )?(?<subject>.+?)\z");
        if(lookup.Success) {
            var phrase=lookup.Groups["subject"].Value;
            var literal=passages.FirstOrDefault(p=>p.Subject==phrase);
            if(literal!=null) return literal;
            phrase=System.Text.RegularExpressions.Regex.Replace(phrase,@" (?:set to|listed as|recorded as)\z","");
            literal=passages.FirstOrDefault(p=>p.Subject==phrase);
            if(literal!=null) return literal;
        }
        // Prefer the most specific full-content proposal before a literal suffix
        // (for example a separate "wheel" claim beside "wheel generator").
        if(System.Text.RegularExpressions.Regex.IsMatch(query,@"^(?:please )?(?:what|which|where|how many)\b")) {
            var requested=SearchTerms(query);
            var words=query.Split(' ').ToHashSet(StringComparer.Ordinal);
            string phrase=" "+query+" ";
            var matched=passages.Where(p=>requested.Count>0 && SearchTerms(p.Subject).SetEquals(requested))
                .OrderByDescending(p=>phrase.Contains(" "+p.Subject+" ",StringComparison.Ordinal))
                .ThenBy(p=>p.Subject.Split(' ').Count(w=>!words.Contains(w))).FirstOrDefault();
            if(matched!=null) return matched;
        }
        foreach(var p in passages) {
            if(!query.EndsWith(" "+p.Subject,StringComparison.Ordinal)) continue;
            if(Rule(query[..^p.Subject.Length])!="procedure") return p;
        }

        return null;
    }

    private static HashSet<string> SearchTerms(string text)
    {
        var terms=new HashSet<string>();
        foreach(System.Text.RegularExpressions.Match word in System.Text.RegularExpressions.Regex.Matches(text,@"[\p{L}\p{N}_-]+")) {
            var term=word.Value switch {
                "please" or "what" or "which" or "how" or "many" or "was" or "is" or "does" or "did" or
                "the" or "this" or "to" or "of" or "on" or "as" or "set" or "recorded" or "listed" or
                "value" or "setting" or "for" or "accept" or "take" or "installation" or "package" or "audit" or "number" => "",
                "arguments" => "parameters",
                "version" when text.Contains(" require",StringComparison.Ordinal) => "",
                "require" => "requirement",
                "tool" or "backend" or "produced" or "built" or "generated" => "generator",
                "installed" => "payload",
                "check" => "checked",
                "defined" or "declared" => "declaration",
                "where" => "line",
                _=>word.Value
            };
            if(term.Length>0) terms.Add(term);
        }
        return terms;
    }

    public static Passage? ProjectCode(string request,IEnumerable<Passage> eligible,Passage? parent,string type)
    {
        if(type!="code.definition")
            return parent==null ? null : eligible.FirstOrDefault(p=>p.DefinitionStart==parent.DefinitionStart &&
                p.Citation.Id==parent.Citation.Id && p.Citation.Type==type);
        // Resolve a Primary proposal against eligible identifiers only. Research
        // independently parses the entire request and checks all active definitions.
        var normalized=System.Text.RegularExpressions.Regex.Replace(request.Trim(),@"\s+"," ");
        var tail=System.Text.RegularExpressions.Regex.Replace(normalized,@"(?:,? please)?[?.!]?\z","",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        tail=System.Text.RegularExpressions.Regex.Replace(tail,@"\s+(?:defined|declared)\z","",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var token=System.Text.RegularExpressions.Regex.Match(tail,@"(?<symbol>[\p{L}_][\p{L}\p{N}\p{M}_]*(?:\.[\p{L}_][\p{L}\p{N}\p{M}_]*)?)(?:\((?<selector>[^()]{0,160})\))?\z");
        if(token.Success) {
            var parts=token.Groups["symbol"].Value.Split('.');
            var selector=token.Groups["selector"];
            var filter=selector.Success ? ParameterFilter.Parse(selector.Value) : null;
            return eligible.FirstOrDefault(p=>p.Citation.Type=="code.definition" && p.Symbol==parts[^1] &&
                (parts.Length==1 || p.Qualifier==parts[0]) && (!selector.Success || filter?.Matches(p.Parameters)==true));
        }
        return null;
    }

    public static string Rule(string text)
    {
        var words=Assets.Tokens(text).ToHashSet();
        if(words.Overlaps(["retention","duration","policy","format","formatted","encoding","expiry","days","retained","kept"]) ||
            words.Contains("how") && words.Overlaps(["long","many"])) return "fact";
        if(words.Overlaps(["how","steps","procedure","restore","rebuild","guide","instructions","recover","walkthrough","configure"]))
            return "procedure";
        if(words.Overlaps(["what","which","limit","policy","duration","where","size","version","when","retention","default"]))
            return "fact";
        return "clarify";
    }
}
