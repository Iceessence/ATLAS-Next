using System.IO;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AtlasNext;

internal record SourceDeclaration(string Name,int NameStart,int NameEnd,int Start,int End,int NameLine,int Line);

internal static class SourceDeclarations
{
    public static IEnumerable<SourceDeclaration> Read(string source,string extension)
    {
        // Strip only the transport BOM. Every offset returned to Documents includes it.
        int bom=source.StartsWith('\uFEFF') ? 1 : 0;
        var text=SourceText.From(source[bom..]);
        var declarations=extension==".cs" ? CSharp(text) : Python(text);
        foreach(var (name,identifier,header) in declarations) {
            if(header.Length>8192) throw new InvalidDataException("Declaration header exceeds 8192 characters.");
            yield return new(name,bom+identifier.Start,bom+identifier.End,bom+header.Start,bom+header.End,
                text.Lines.GetLineFromPosition(identifier.Start).LineNumber+1,text.Lines.GetLineFromPosition(header.Start).LineNumber+1);
        }
    }

    private static IEnumerable<(string,TextSpan,TextSpan)> CSharp(SourceText text)
    {
        var tree=CSharpSyntaxTree.ParseText(text);
        var root=tree.GetRoot();
        if(tree.GetDiagnostics().Any(d=>d.Severity==DiagnosticSeverity.Error) ||
            root.DescendantTrivia(descendIntoTrivia:true).Any(t=>t.IsKind(SyntaxKind.IfDirectiveTrivia)))
            throw new InvalidDataException("C# source needs a valid declaration tree without conditional compilation.");
        foreach(var node in root.DescendantNodes()) {
            SyntaxToken id, boundary;
            switch(node) {
                case BaseTypeDeclarationSyntax t: id=t.Identifier; boundary=t.OpenBraceToken.RawKind!=0 ? t.OpenBraceToken : t.SemicolonToken; break;
                case MethodDeclarationSyntax m: id=m.Identifier; boundary=m.Body?.OpenBraceToken ?? m.ExpressionBody?.ArrowToken ?? m.SemicolonToken; break;
                case LocalFunctionStatementSyntax l: id=l.Identifier; boundary=l.Body?.OpenBraceToken ?? l.ExpressionBody?.ArrowToken ?? l.SemicolonToken; break;
                default: continue;
            }
            // Skip leading attribute lists, but retain modifiers, arguments, return
            // type and constraints up to the last token before the body.
            var first=node.ChildNodesAndTokens().First(n=>!n.IsKind(SyntaxKind.AttributeList)).Span.Start;
            int end=boundary.GetPreviousToken().Span.End;
            yield return (id.ValueText,id.Span,TextSpan.FromBounds(first,end));
        }
    }

    // Python source navigation uses lexical declaration headers. Strings, comments,
    // continuations and nested delimiters cannot introduce a new statement header.
    // Reject an unsupported lexical construct before publishing any part of a file.
    private static readonly Regex PythonHead=new(@"\G(?:(?:async[ \t]+)?def|class)[ \t]+(?<id>[\p{L}_][\p{L}\p{N}\p{M}_]*)",
        RegexOptions.CultureInvariant);
    private static IEnumerable<(string,TextSpan,TextSpan)> Python(SourceText source)
    {
        string text=source.ToString();
        foreach(var line in source.Lines.Take(2)) {
            var coding=Regex.Match(line.ToString(),@"^\s*#.*?coding[:=]\s*([-\w.]+)");
            if(coding.Success && coding.Groups[1].Value.ToLowerInvariant() is not ("utf8" or "utf-8" or "utf_8"))
                throw new InvalidDataException("Python source must declare UTF-8 or use its UTF-8 default.");
        }
        var nesting=new Stack<char>();
        bool statementStart=true;
        int header=-1;
        Group? name=null;
        for(int i=0;i<text.Length;) {
            char c=text[i];
            if(c is ' ' or '\t') { i++; continue; }
            if(c=='#') { while(i<text.Length && text[i] is not ('\r' or '\n')) i++; continue; }
            if(c=='\\' && i+1<text.Length && text[i+1] is '\r' or '\n') {
                i+=2;
                if(text[i-1]=='\r' && i<text.Length && text[i]=='\n') i++;
                statementStart=false;
                continue;
            }
            if(c is '\r' or '\n') {
                if(nesting.Count==0 && header>=0) throw new InvalidDataException("Incomplete Python declaration.");
                statementStart=nesting.Count==0;
                i++;
                continue;
            }
            if(statementStart) {
                var match=PythonHead.Match(text,i);
                if(match.Success) {
                    header=i;
                    name=match.Groups["id"];
                    int after=name.Index+name.Length;
                    while(after<text.Length && text[after] is ' ' or '\t') after++;
                    bool isClass=text.AsSpan(i).StartsWith("class");
                    if(after>=text.Length || !(text[after]=='(' || (isClass && text[after]==':')))
                        throw new InvalidDataException("Unsupported Python declaration header.");
                }
                statementStart=false;
            }
            if(c is '\'' or '"') { i=AfterString(text,i); continue; }
            if(c>127) throw new InvalidDataException("Python navigation supports ASCII identifiers and UTF-8 strings and comments.");
            if(c is '(' or '[' or '{') nesting.Push(c);
            else if(c is ')' or ']' or '}') {
                if(!nesting.TryPop(out char open) || open!=(c==')' ? '(' : c==']' ? '[' : '{'))
                    throw new InvalidDataException("Unbalanced Python delimiters.");
            }
            else if(c==':' && nesting.Count==0 && header>=0) {
                yield return (name!.Value,new TextSpan(name.Index,name.Length),TextSpan.FromBounds(header,i+1));
                header=-1;
            }
            i++;
        }
        if(nesting.Count!=0 || header>=0) throw new InvalidDataException("Incomplete Python source.");
    }

    private static int AfterString(string text,int start)
    {
        char quote=text[start];
        int width=start+2<text.Length && text[start+1]==quote && text[start+2]==quote ? 3 : 1;
        int prefix=start;
        while(prefix>0 && char.IsAsciiLetter(text[prefix-1])) prefix--;
        string kind=text[prefix..start].ToLowerInvariant();
        bool formatted=kind is "f" or "fr" or "rf" or "t" or "tr" or "rt";
        int depth=0;
        for(int i=start+width;i<text.Length;i++) {
            char c=text[i];
            if(depth==1) {
                if(c=='}') { depth=0; continue; }
                if(c==quote || c is '{' or '#' or '\\' or '\r' or '\n')
                    throw new InvalidDataException("Unsupported nested Python interpolation.");
                if(c is '\'' or '"') {
                    // Permit simple quoted arguments inside an interpolation, such
                    // as decode('utf8'). Never count braces in that inner string.
                    char inner=c;
                    do {
                        if(++i>=text.Length || text[i] is '\\' or '\r' or '\n' || text[i]==quote)
                            throw new InvalidDataException("Unsupported string in Python interpolation.");
                    } while(text[i]!=inner);
                }
                continue;
            }
            if(c=='\\') {
                // Escape sequences do not escape interpolation braces in Python.
                if(formatted && i+1<text.Length && text[i+1] is '{' or '}') continue;
                i++;
                if(i<text.Length && text[i]=='\r' && i+1<text.Length && text[i+1]=='\n') i++;
                continue;
            }
            if(c==quote && (width==1 || i+2<text.Length && text[i+1]==quote && text[i+2]==quote)) {
                if(depth!=0) throw new InvalidDataException("Nested Python interpolation requires a full parser.");
                return i+width;
            }
            if(width==1 && c is '\r' or '\n') throw new InvalidDataException("Unterminated Python string.");
            if(!formatted || c is not ('{' or '}')) continue;
            if(depth==0 && i+1<text.Length && text[i+1]==c) { i++; continue; }
            if(c=='{') depth=1;
            else throw new InvalidDataException("Unsupported nested Python interpolation.");
        }
        throw new InvalidDataException("Unterminated Python string.");
    }
}
