using System.IO;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AtlasNext;

internal record SourcePart(int Start,int End,int Line);
internal record ParameterShape(int Arity,string[]? Types=null);
internal record SourceDeclaration(string Name,SourcePart Identifier,SourcePart Header,string? Qualifier,
    SourcePart? Container,bool ContainerIsClass,SourcePart? Documentation,SourcePart? Summary,SourcePart? Namespace,ParameterShape? Parameters);


internal static class SourceDeclarations
{
    private sealed record Declaration(string Name,TextSpan Identifier,TextSpan Header,string? Qualifier,
        TextSpan? Container,bool ContainerIsClass,TextSpan? Documentation,TextSpan? Summary=null,TextSpan? Namespace=null,ParameterShape? Parameters=null);

    public static IEnumerable<SourceDeclaration> Read(string source,string extension)
    {
        int bom=source.StartsWith('\uFEFF') ? 1 : 0;
        var text=SourceText.From(source[bom..]);
        SourcePart Anchor(TextSpan s)=>new(bom+s.Start,bom+s.End,text.Lines.GetLineFromPosition(s.Start).LineNumber+1);
        SourcePart? Optional(TextSpan? s)=>s is {Length:>0 and <=16384} value ? Anchor(value) : null;
        foreach(var d in extension==".cs" ? CSharp(text) : Python(text)) {
            if(d.Header.Length>8192) throw new InvalidDataException("Declaration header exceeds 8192 characters.");
            yield return new(d.Name,Anchor(d.Identifier),Anchor(d.Header),d.Qualifier,
                Optional(d.Container),d.ContainerIsClass,Optional(d.Documentation),Optional(d.Summary),Optional(d.Namespace),d.Parameters);
        }
    }

    private static (TextSpan? Doc,TextSpan? Summary) CSharpDocumentation(SyntaxNode node,SourceText text)
    {
        foreach(var trivia in node.GetLeadingTrivia().Reverse()) {
            if(trivia.IsKind(SyntaxKind.WhitespaceTrivia)) continue;
            if(!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                trivia.GetStructure() is not DocumentationCommentTriviaSyntax doc) break;
            var raw=text.ToString(trivia.FullSpan);
            int first=raw.IndexOf("///",StringComparison.Ordinal);
            if(first<0) break;
            var block=TextSpan.FromBounds(trivia.FullSpan.Start+first,trivia.FullSpan.Start+raw.TrimEnd('\r','\n').Length);
            var summaries=doc.Content.OfType<XmlElementSyntax>().Where(e=>e.StartTag.Name.ToString()=="summary").ToArray();
            return (block,summaries is [var summary] && !summary.ContainsDiagnostics ? summary.Span : null);
        }
        return (null,null);
    }

    private static IEnumerable<Declaration> CSharp(SourceText text)
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
            var container=node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
            string? qualifier=node.Parent is BaseTypeDeclarationSyntax direct && node is not MethodDeclarationSyntax {ExplicitInterfaceSpecifier:not null}
                ? direct.Identifier.ValueText : null;
            // A qualified namespace can be quoted exactly from a single declaration.
            // Nested blocks with separate names have no contiguous fully qualified span.
            var namespaces=node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Take(2).ToArray();
            var (doc,summary)=CSharpDocumentation(node,text);
            yield return new(id.ValueText,id.Span,TextSpan.FromBounds(first,end),qualifier,container?.Identifier.Span,container is ClassDeclarationSyntax || container is RecordDeclarationSyntax record && !record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword),doc,summary,namespaces is [var ns] ? ns.Name.Span : null,MethodParameters(node));
        }
    }

    private static ParameterShape? MethodParameters(SyntaxNode node)
    {
        if(node is not MethodDeclarationSyntax {TypeParameterList:null} method) return null;
        var parameters=method.ParameterList.Parameters;
        var types=parameters.All(p=>p.Modifiers.Count==0 && p.Type is PredefinedTypeSyntax or IdentifierNameSyntax)
            ? parameters.Select(p=>p.Type!.ToString()).ToArray() : null;
        return new(parameters.Count,types);
    }

    // Python source navigation uses lexical declaration headers. Strings, comments,
    // continuations and nested delimiters cannot introduce a new statement header.
    // Reject an unsupported lexical construct before publishing any part of a file.
    private static readonly Regex PythonHead=new(@"\G(?:(?:async[ \t]+)?def|class)[ \t]+(?<id>[\p{L}_][\p{L}\p{N}\p{M}_]*)",
        RegexOptions.CultureInvariant);
    private sealed record Scope(int Indent,bool Class,string Name,TextSpan Identifier);
    private static int Indent(string text,int start,int end)
    {
        int column=0;
        for(int i=start;i<end && text[i] is ' ' or '\t';i++)
            column=text[i]=='\t' ? column+8-column%8 : column+1;
        return column;
    }

    private static TextSpan? PythonDocumentation(string text,int colon,int headerIndent)
    {
        int i=colon+1,lineStart=-1;
        while(i<text.Length) {
            if(text[i] is ' ' or '\t') { i++; continue; }
            if(text[i]=='#') { while(i<text.Length && text[i] is not ('\r' or '\n')) i++; continue; }
            if(text[i] is '\r' or '\n') { lineStart=++i; continue; }
            break;
        }
        if(i>=text.Length || lineStart>=0 && Indent(text,lineStart,i)<=headerIndent) return null;
        int start=i;
        if(text[i] is 'r' or 'R' or 'u' or 'U') i++;
        if(i>=text.Length || text[i] is not ('\'' or '"')) return null;
        int end=AfterString(text,i);
        i=end;
        while(i<text.Length && text[i] is ' ' or '\t') i++;
        return i==text.Length || text[i] is '#' or ';' or '\r' or '\n' ? TextSpan.FromBounds(start,end) : null;
    }

    private static IEnumerable<Declaration> Python(SourceText source)
    {
        string text=source.ToString();
        foreach(var line in source.Lines.Take(2)) {
            var coding=Regex.Match(line.ToString(),@"^\s*#.*?coding[:=]\s*([-\w.]+)");
            if(coding.Success && coding.Groups[1].Value.ToLowerInvariant() is not ("utf8" or "utf-8" or "utf_8"))
                throw new InvalidDataException("Python source must declare UTF-8 or use its UTF-8 default.");
        }
        var nesting=new Stack<char>();
        bool statementStart=true;
        int header=-1,headerIndent=0;
        var scopes=new Stack<Scope>();
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
                int lineStart=source.Lines.GetLineFromPosition(i).Start;
                int indent=Indent(text,lineStart,i);
                while(scopes.TryPeek(out var scope) && indent<=scope.Indent) scopes.Pop();
                var match=PythonHead.Match(text,i);
                if(match.Success) {
                    header=i;
                    headerIndent=indent;
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
                var identifier=new TextSpan(name!.Index,name.Length);
                var container=scopes.FirstOrDefault(s=>s.Class);
                string? qualifier=scopes.TryPeek(out var direct) && direct.Class ? direct.Name : null;
                yield return new(name.Value,identifier,TextSpan.FromBounds(header,i+1),qualifier,
                    container?.Identifier,container!=null,PythonDocumentation(text,i,headerIndent),
                    Parameters:text.AsSpan(header).StartsWith("class") ? null : PythonParameters(text[identifier.End..i]));
                scopes.Push(new(headerIndent,text.AsSpan(header).StartsWith("class"),name.Value,identifier));
                header=-1;
            }
            i++;
        }
        if(nesting.Count!=0 || header>=0) throw new InvalidDataException("Incomplete Python source.");
    }

    private static ParameterShape? PythonParameters(string tail)
    {
        // Count simple positional parameters. Defaults, annotations, variadics
        // and positional/keyword separators require a fuller signature parser.
        var match=Regex.Match(tail,@"\A[ \t]*\((?<args>\s*(?:[A-Za-z_]\w*\s*(?:,\s*[A-Za-z_]\w*\s*)*,?\s*)?)\)(?:\s*->[^:\r\n]+)?\s*\z",
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
        if(!match.Success) return null;
        var names=match.Groups["args"].Value.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        return names.Distinct(StringComparer.Ordinal).Count()==names.Length ? new(names.Length) : null;
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
