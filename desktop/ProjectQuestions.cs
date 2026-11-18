using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AtlasNext;

internal record ParameterFilter(int Arity,string[]? Types=null)
{
    public static ParameterFilter? Parse(string text)
    {
        text=text.Trim();
        if(text.Length==0) return new(0);
        var arity=Regex.Match(text,@"\A([0-9]{1,2})\s+parameters?\z",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        if(arity.Success) return new(int.Parse(arity.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture));
        var types=text.Split(',',StringSplitOptions.TrimEntries);
        return types.Length<=16 && types.All(t=>Regex.IsMatch(t,@"\A[\p{L}_][\p{L}\p{N}\p{M}_]*\z"))
            ? new(types.Length,types) : null;
    }

    public bool Matches(ParameterShape? shape)=>shape!=null && Arity==shape.Arity &&
        (Types==null || shape.Types!=null && Types.SequenceEqual(shape.Types,StringComparer.Ordinal));
}

internal record ClaimAssessment(string Subject,string Verdict,string Freshness,Citation[] Sources);
internal record ProjectFinding(Assessment Assessment,string? Answer,Citation? Evidence);

internal static class ProjectQuestions
{
    public const string ResearchType="research.compare";
    public const string CallsType="code.calls";
    private const int MaxSources=8;
    private const int MaxCallChars=2048;
    private static readonly Lazy<CSharpCompilation> ReferenceContext=new(()=>CSharpCompilation.Create("OwnerImportedSource",
        references:new[]{typeof(object),typeof(Enumerable),typeof(Regex),typeof(System.Security.Cryptography.SHA256),typeof(System.Text.Json.JsonSerializer)}
            .Select(t=>t.Assembly.Location).Distinct().Select(path=>MetadataReference.CreateFromFile(path)),
        options:new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,concurrentBuild:false)));

    private const string ResearchCommand="Compare project claims:";
    public static bool IsResearch(string request)=>request.StartsWith(ResearchCommand,StringComparison.OrdinalIgnoreCase);

    private static ProjectFinding Stop(string verdict,string detail,Citation[] sources,ClaimAssessment[]? claims=null)=>
        new(new(verdict,detail,"see source status","owner-selected source",sources,claims),null,null);

    public static ProjectFinding Compare(string request,IReadOnlyList<Passage> active)
    {
        var requested=request[ResearchCommand.Length..].Trim().TrimEnd('?').Split('|',StringSplitOptions.TrimEntries);
        if(requested.Length is <2 or >4 || requested.Any(t=>!Regex.IsMatch(t,@"\A[\p{L}\p{N}][\p{L}\p{N} _-]{0,95}\z")))
            return Stop("unsupported","Enter two to four literal project claim keys separated by |.",[]);
        var keys=requested.Select(Research.LiteralSubject).ToArray();
        if(keys.Distinct(StringComparer.Ordinal).Count()!=keys.Length)
            return Stop("unsupported","Each requested key must be distinct.",[]);

        var groups=keys.Select(key=>active.Where(p=>p.Citation.Type=="fact" && p.LiteralSubject==key).Take(MaxSources+1).ToArray()).ToArray();
        var sources=groups.SelectMany(g=>g).Select(p=>p.Citation).Take(MaxSources+1).ToArray();
        if(sources.Length>MaxSources) return Stop("ambiguous","More than eight passages match. Narrow the comparison.",sources[..MaxSources]);
        var today=DateOnly.FromDateTime(DateTime.UtcNow);
        var claims=keys.Select((key,i)=>{
            var evidence=groups[i];
            var values=evidence.Select(p=>p.Value).ToArray();
            string verdict=Research.ValueVerdict(values,evidence.Any(p=>p.ValidThrough<today));
            string freshness=verdict=="stale" ? "expired" : evidence.Length==0 ? "no evidence" :
                evidence.Any(p=>p.ValidThrough==null) ? "active revision; undated source" :
                $"active revision; valid through {evidence.Min(p=>p.ValidThrough):yyyy-MM-dd}";
            return new ClaimAssessment(key,verdict,freshness,evidence.Select(p=>p.Citation).ToArray());
        }).ToArray();

        foreach(var (verdict,detail) in new[]{("stale","At least one matching passage is past its declared validity date."),
            ("contradiction","Current sources give conflicting values for a requested key."),
            ("insufficient","At least one requested key lacks a confirmed value.")})
            if(claims.Any(c=>c.Verdict==verdict)) return Stop(verdict,detail,sources,claims);
        if(sources.Select(c=>c.Id).Distinct().Count()<2)
            return Stop("insufficient","The comparison requires support from at least two imported documents.",sources,claims);

        var answer=string.Join("\n",keys.Select((key,i)=>$"{key}: {groups[i][0].Value}"));
        return new(new("supported","Every requested key has explicit support in the listed local passages.",
            "active project revisions","owner-selected text",sources,claims),answer,sources[0]);
    }

    public static ProjectFinding Calls(CodeQuestion question,IReadOnlyList<OwnedSource> active,IReadOnlyList<Passage> passages)
    {
        var definition=(question with {Type="code.definition"}).Verify(passages,null);
        var declared=definition.Source?.Citation;
        if(declared==null) return new(definition.Assessment,null,null);
        if(!declared.Source.EndsWith(".cs",StringComparison.OrdinalIgnoreCase))
            return Stop("unsupported","Call binding is available for C# declarations.",[declared]);
        var files=active.Where(source=>source.Extension==".cs").ToArray();
        if(files.Length<2) return Stop("unsupported","Import another C# file to look up cross-file calls.",[declared]);

        // Build symbols from imported text in memory. Metadata comes only from
        // the bundled runtime. The target has no resolver, emission or execution.
        var trees=files.Select(f=>CSharpSyntaxTree.ParseText(f.Text,path:f.Path)).ToArray();
        var compilation=ReferenceContext.Value.AddSyntaxTrees(trees);
        int index=Array.FindIndex(files,f=>f.Id==declared.Id);
        var targetTree=trees[index];
        var node=targetTree.GetRoot().FindToken(declared.Span!.Start).Parent as MethodDeclarationSyntax;
        if(node is null || compilation.GetSemanticModel(targetTree).GetDeclaredSymbol(node) is not IMethodSymbol method ||
            method.ContainingType.TypeKind==TypeKind.Error ||
            compilation.GetSemanticModel(targetTree).GetDiagnostics(node.ParameterList.Span).Any(d=>d.Severity==DiagnosticSeverity.Error))
            return Stop("unsupported","The declaration cannot be bound uniquely in this import set.",[declared]);

        var results=new List<(Citation Citation,string Text)>();
        for(int i=0;i<files.Length;i++) {
            if(i==index) continue;
            var tree=trees[i];
            var model=compilation.GetSemanticModel(tree);
            foreach(var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()) {
                var name=call.Expression switch {
                    SimpleNameSyntax bare=>bare.Identifier.ValueText,
                    MemberAccessExpressionSyntax member=>member.Name.Identifier.ValueText,
                    _=>null
                };
                if(name!=question.Name || model.GetSymbolInfo(call).Symbol is not IMethodSymbol bound ||
                    !SymbolEqualityComparer.Default.Equals((bound.ReducedFrom??bound).OriginalDefinition,method) ||
                    model.GetDiagnostics(call.Span).Any(d=>d.Severity==DiagnosticSeverity.Error)) continue;
                if(results.Count==MaxSources-1 || call.Span.Length>MaxCallChars)
                    return Stop("ambiguous","The bound call sites exceed the citation budget. Narrow the source set.",
                        [declared,..results.Select(r=>r.Citation)]);
                int line=tree.GetLineSpan(call.Span).StartLinePosition.Line+1;
                var cite=files[i].Cite(CallsType,line,call.SpanStart,call.Span.Length,"owner-selected source");
                results.Add((cite,files[i].Text.Substring(call.SpanStart,call.Span.Length)));
            }
        }
        if(results.Count==0) return Stop("unsupported","No cross-file call binds to this declaration in the imported sources.",[declared]);
        var sources=new[]{declared}.Concat(results.Select(r=>r.Citation)).ToArray();
        var answer=string.Join("\n",results.Select(r=>$"{r.Citation.Source}, line {r.Citation.Span!.Line}: {r.Text}"));
        return new(new("supported","These cross-file invocations bind to the declaration in the imported C# source set.",
            "active project revisions","owner-selected source",sources),answer,sources[0]);
    }
}
