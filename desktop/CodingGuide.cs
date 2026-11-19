namespace AtlasNext;

internal sealed record GuidePart(string Text,Citation Citation);
internal sealed record CodingGuide(GuidePart Definition,GuidePart Signature,GuidePart? Documentation,GuidePart? Return,Citation[] Calls)
{
    internal static string Place(Citation c)=>
        $"{c.Span!.Path} · line {c.Span.Line} · UTF-16 [{c.Span.Start}, {c.Span.Start+c.Span.Length}) · revision {c.Version} · SHA-256 {c.Digest}";

    public string Render()=>string.Join("\n\n",new[]{Part("Declaration",Definition),Part("Signature",Signature),
        Part("Documentation",Documentation),Part("Return type",Return),
        Calls.Length==0 ? "" : "Verified cross-file invocations\n"+string.Join("\n",Calls.Select(Place))}.Where(s=>s.Length>0));
    private static string Part(string label,GuidePart? part)=>part==null ? "" : $"{label}\n{part.Text}\n{Place(part.Citation)}";

    public string? Attachment()
    {
        string text=$"Selected source declaration at {Place(Definition.Citation)}\n{Signature.Text}";
        if(Return?.Text is {} value) text+=$"\nDeclared return type: {value}";
        if(Documentation is {} doc) {
            string candidate=text+$"\nAttached source documentation at {Place(doc.Citation)}\n{doc.Text}";
            if(candidate.Length<=1700) text=candidate;
        }
        return text.Length<=1700 ? text : null;
    }
}
