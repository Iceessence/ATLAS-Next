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
    SourcePart? Container,bool ContainerIsClass,SourcePart? Documentation,SourcePart? Summary,SourcePart? Namespace,ParameterShape? Parameters,
    SourcePart? ParametersSpan=null,SourcePart? ReturnType=null);


internal static class SourceDeclarations
{
    private sealed record Declaration(string Name,TextSpan Identifier,TextSpan Header,string? Qualifier,
        TextSpan? Container,bool ContainerIsClass,TextSpan? Documentation,TextSpan? Summary=null,TextSpan? Namespace=null,ParameterShape? Parameters=null,
        TextSpan? ParametersSpan=null,TextSpan? ReturnType=null);

    public static IEnumerable<SourceDeclaration> Read(string source,string extension)
    {
        int bom=source.StartsWith('\uFEFF') ? 1 : 0;
        var text=SourceText.From(source[bom..]);
        SourcePart Anchor(TextSpan s)=>new(bom+s.Start,bom+s.End,text.Lines.GetLineFromPosition(s.Start).LineNumber+1);
        SourcePart? Optional(TextSpan? s)=>s is {Length:>0 and <=16384} value ? Anchor(value) : null;
        foreach(var d in extension==".cs" ? CSharp(text) : Python(text)) {
            if(d.Header.Length>8192) throw new InvalidDataException("Declaration header exceeds 8192 characters.");
            yield return new(d.Name,Anchor(d.Identifier),Anchor(d.Header),d.Qualifier,
                Optional(d.Container),d.ContainerIsClass,Optional(d.Documentation),Optional(d.Summary),Optional(d.Namespace),d.Parameters,
                Optional(d.ParametersSpan),Optional(d.ReturnType));
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
            TextSpan? paramsSpan=node switch {
                MethodDeclarationSyntax m=>m.ParameterList.Span,
                LocalFunctionStatementSyntax l=>l.ParameterList.Span,
                _=>null
            };
            TextSpan? returnSpan=node switch {
                MethodDeclarationSyntax m=>m.ReturnType.Span,
                LocalFunctionStatementSyntax l=>l.ReturnType.Span,
                _=>null
            };
            yield return new(id.ValueText,id.Span,TextSpan.FromBounds(first,end),qualifier,container?.Identifier.Span,container is ClassDeclarationSyntax || container is RecordDeclarationSyntax record && !record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword),doc,summary,namespaces is [var ns] ? ns.Name.Span : null,MethodParameters(node),paramsSpan,returnSpan);
        }
    }

    private static string? CanonicalType(TypeSyntax? type)=>type switch {
        PredefinedTypeSyntax or IdentifierNameSyntax=>type.ToString(),
        NullableTypeSyntax nullable when CanonicalType(nullable.ElementType) is {} inner=>inner+"?",
        ArrayTypeSyntax array when array.RankSpecifiers is [{Rank:1}] && CanonicalType(array.ElementType) is {} elem=>elem+"[]",
        GenericNameSyntax generic when generic.TypeArgumentList.Arguments is {Count:>=1 and <=8} args=>
            args.Select(CanonicalType).ToArray() is var parts && parts.All(p=>p!=null)
                ? $"{generic.Identifier.ValueText}<{string.Join(',',parts!)}>" : null,
        _=>null
    };

    private static ParameterShape? MethodParameters(SyntaxNode node)
    {
        if(node is not MethodDeclarationSyntax {TypeParameterList:null} method) return null;
        var parameters=method.ParameterList.Parameters;
        var canonical=parameters.All(p=>p.Modifiers.Count==0)
            ? parameters.Select(p=>CanonicalType(p.Type)).ToArray() : null;
        var types=canonical!=null && canonical.All(t=>t!=null) ? canonical! : null;
        return new(parameters.Count,types!);
    }

    // Python source navigation uses lexical declaration headers. Strings, comments,
    // continuations and nested delimiters cannot introduce a new statement header.
    // Malformed declarations are never published or used to authorize follow-ups.
    private static readonly Regex PythonHead=new(@"\G(?:(?:async[ \t]+)?def|class)[ \t]+(?<id>[\p{L}_][\p{L}\p{N}\p{M}_]*)",
        RegexOptions.CultureInvariant);
    private static readonly HashSet<string> PythonKeywords=new(StringComparer.Ordinal) {
        "False","None","True","and","as","assert","async","await","break","class","continue","def","del",
        "elif","else","except","finally","for","from","global","if","import","in","is","lambda","nonlocal",
        "not","or","pass","raise","return","try","while","with","yield"
    };
    private static readonly HashSet<string> PythonStatementKeywords=new(StringComparer.Ordinal) {
        "assert","break","class","continue","def","del","elif","except","finally","from","global","import",
        "nonlocal","pass","raise","return","try","while","with"
    };
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
        int header=-1,headerIndent=0,topLevelLambda=0;
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
                    topLevelLambda=0;
                    name=match.Groups["id"];
                    int after=SkipPythonTrivia(text,name.Index+name.Length,text.Length);
                    bool isClass=text.AsSpan(i).StartsWith("class");
                    if(after>=text.Length || !(text[after]=='(' || (isClass && text[after]==':')))
                        throw new InvalidDataException("Unsupported Python declaration header.");
                }
                statementStart=false;
            }
            if(c is '\'' or '"') { i=AfterString(text,i); continue; }
            if(c>127) throw new InvalidDataException("Python navigation supports ASCII identifiers and UTF-8 strings and comments.");
            if(header>=0 && nesting.Count==0 && i>=name!.Index+name.Length && (char.IsAsciiLetter(c) || c=='_') &&
                (i==0 || !(char.IsAsciiLetterOrDigit(text[i-1]) || text[i-1]=='_'))) {
                int idEnd=i+1;
                while(idEnd<text.Length && (char.IsAsciiLetterOrDigit(text[idEnd]) || text[idEnd]=='_')) idEnd++;
                if(text[i..idEnd]=="lambda") topLevelLambda++;
                i=idEnd;
                continue;
            }
            if(c is '(' or '[' or '{') nesting.Push(c);
            else if(c is ')' or ']' or '}') {
                if(!nesting.TryPop(out char open) || open!=(c==')' ? '(' : c==']' ? '[' : '{'))
                    throw new InvalidDataException("Unbalanced Python delimiters.");
            }
            else if(c==':' && nesting.Count==0 && header>=0) {
                if(i+1<text.Length && text[i+1]=='=') { i+=2; continue; }
                if(topLevelLambda>0) { topLevelLambda--; i++; continue; }
                var identifier=new TextSpan(name!.Index,name.Length);
                var container=scopes.FirstOrDefault(s=>s.Class);
                string? qualifier=scopes.TryPeek(out var direct) && direct.Class ? direct.Name : null;
                bool isClass=text.AsSpan(header).StartsWith("class");
                bool valid=!PythonKeywords.Contains(name.Value);
                ParameterShape? shape=null;
                TextSpan? paramsSpan=null,returnSpan=null;
                if(valid) {
                    if(isClass) valid=ValidatePythonClassHeader(text,identifier.End,i);
                    else (valid,shape,paramsSpan,returnSpan)=PythonSignature(text,identifier.End,i);
                }
                if(valid) {
                    yield return new(name.Value,identifier,TextSpan.FromBounds(header,i+1),qualifier,
                        container?.Identifier,container!=null,PythonDocumentation(text,i,headerIndent),
                        Parameters:shape,ParametersSpan:paramsSpan,ReturnType:returnSpan);
                }
                scopes.Push(new(headerIndent,isClass && valid,name.Value,identifier));
                header=-1;
                topLevelLambda=0;
            }
            i++;
        }
        if(nesting.Count!=0 || header>=0) throw new InvalidDataException("Incomplete Python source.");
    }

    private static int SkipPythonTrivia(string text,int pos,int limit)
    {
        while(pos<limit) {
            char c=text[pos];
            if(c is ' ' or '\t' or '\r' or '\n') { pos++; continue; }
            if(c=='#') { while(pos<limit && text[pos] is not ('\r' or '\n')) pos++; continue; }
            if(c=='\\' && pos+1<limit && text[pos+1] is '\r' or '\n') {
                pos+=2;
                if(text[pos-1]=='\r' && pos<limit && text[pos]=='\n') pos++;
                continue;
            }
            break;
        }
        return pos;
    }

    private static bool ValidatePythonClassHeader(string text,int tailStart,int colon)
    {
        int i=SkipPythonTrivia(text,tailStart,colon);
        if(i==colon) return true;
        if(text[i]!='(') return false;
        if(!TryScanMatchingParens(text,i,colon,out int close)) return false;
        int after=SkipPythonTrivia(text,close+1,colon);
        if(after!=colon) return false;
        return ValidatePythonCommaExpressions(text,i+1,close,allowKeywordArgs:true);
    }

    private static bool TryScanMatchingParens(string text,int open,int limit,out int close)
    {
        close=-1;
        int i=open+1,depth=1;
        while(i<limit && depth>0) {
            char c=text[i];
            if(c=='#') { while(i<limit && text[i] is not ('\r' or '\n')) i++; continue; }
            if(c is '\'' or '"') { i=AfterString(text,i); continue; }
            if(c is '(' or '[' or '{') depth++;
            else if(c is ')' or ']' or '}') {
                depth--;
                if(depth==0 && c==')') { close=i; return true; }
            }
            i++;
        }
        return false;
    }

    private static (bool Valid,ParameterShape? Shape,TextSpan? Params,TextSpan? Return) PythonSignature(string text,int tailStart,int colon)
    {
        int i=SkipPythonTrivia(text,tailStart,colon);
        if(i>=colon || text[i]!='(') return (false,null,null,null);
        int open=i;
        if(!TryScanMatchingParens(text,open,colon,out int close)) return (false,null,null,null);
        var paramsSpan=TextSpan.FromBounds(open,close+1);
        if(!ValidatePythonParameters(text,open+1,close,out var shape))
            return (false,null,null,null);

        i=SkipPythonTrivia(text,close+1,colon);
        if(i==colon) return (true,shape,paramsSpan,null);
        if(i+1>=colon || text[i]!='-' || text[i+1]!='>')
            return (false,null,null,null);
        if(!TryScanPythonExpression(text,i+2,colon,allowTopComma:false,out int retStart,out int retEnd))
            return (false,null,null,null);
        return (true,shape,paramsSpan,TextSpan.FromBounds(retStart,retEnd));
    }

    private static List<string>? SplitTopLevelComma(string text,int start,int end,out bool trailingComma)
    {
        trailingComma=false;
        var segments=new List<string>();
        var current=new System.Text.StringBuilder();
        int depth=0;
        for(int i=start;i<end;) {
            char c=text[i];
            if(c=='#') { while(i<end && text[i] is not ('\r' or '\n')) i++; continue; }
            if(c=='\\' && i+1<end && text[i+1] is '\r' or '\n') {
                i+=2;
                if(text[i-1]=='\r' && i<end && text[i]=='\n') i++;
                current.Append(' ');
                continue;
            }
            if(c is '\'' or '"') {
                int after=AfterString(text,i);
                current.Append(text,i,after-i);
                i=after;
                continue;
            }
            if(c is '(' or '[' or '{') depth++;
            else if(c is ')' or ']' or '}') {
                if(--depth<0) return null;
            }
            if(c==',' && depth==0) {
                segments.Add(current.ToString().Trim());
                current.Clear();
                i++;
                continue;
            }
            current.Append(c);
            i++;
        }
        if(depth!=0) return null;
        string last=current.ToString().Trim();
        if(segments.Count>0 && last.Length==0) {
            trailingComma=true;
            return segments;
        }
        if(segments.Count==0 && last.Length==0) return [];
        segments.Add(last);
        return segments;
    }

    private static bool ValidatePythonCommaExpressions(string text,int start,int end,bool allowKeywordArgs)
    {
        if(SplitTopLevelComma(text,start,end,out _) is not {} segments) return false;
        foreach(var seg in segments) {
            if(seg.Length==0) return false;
            string expr=seg;
            if(allowKeywordArgs && (expr.StartsWith("**",StringComparison.Ordinal) || expr.StartsWith('*')))
                expr=expr[(expr.StartsWith("**",StringComparison.Ordinal) ? 2 : 1)..].Trim();
            else if(allowKeywordArgs && FindTopLevelAssign(expr,0) is >=0 and var eq) {
                string key=expr[..eq].Trim();
                if(!Regex.IsMatch(key,@"\A[A-Za-z_]\w*\z",RegexOptions.CultureInvariant) || PythonKeywords.Contains(key))
                    return false;
                expr=expr[(eq+1)..];
            }
            if(!TryScanPythonExpression(expr,0,expr.Length,allowTopComma:false,out _,out _)) return false;
        }
        return true;
    }

    private static bool ValidatePythonParameters(string text,int start,int end,out ParameterShape? shape)
    {
        shape=null;
        if(SplitTopLevelComma(text,start,end,out _) is not {} segments) return false;
        if(segments.Count==0) { shape=new(0); return true; }
        if(segments.Any(s=>s.Length==0)) return false;

        var names=new HashSet<string>(StringComparer.Ordinal);
        bool seenDefault=false,seenSlash=false,seenStar=false,seenBareStar=false,seenKwargs=false,simplePositional=true;
        int bareStarIndex=-1;

        for(int idx=0;idx<segments.Count;idx++) {
            string seg=segments[idx];
            if(seenKwargs) return false;
            if(seg=="/") {
                if(idx==0 || seenSlash || seenStar) return false;
                seenSlash=true;
                simplePositional=false;
                continue;
            }
            if(seg=="*") {
                if(seenStar) return false;
                seenStar=true;
                seenBareStar=true;
                bareStarIndex=idx;
                simplePositional=false;
                continue;
            }
            if(seg.StartsWith("**",StringComparison.Ordinal)) {
                if(seenBareStar && idx==bareStarIndex+1) return false;
                string rest=seg[2..].TrimStart();
                if(!ParseAnnotatedParam(rest,out string? kwName,out bool hasAnn,out string? defExpr)) return false;
                if(PythonKeywords.Contains(kwName!) || !names.Add(kwName!) || defExpr!=null) return false;
                seenKwargs=true;
                seenBareStar=false;
                simplePositional=false;
                continue;
            }
            if(seg.StartsWith('*')) {
                if(seenStar) return false;
                string rest=seg[1..].TrimStart();
                if(!ParseAnnotatedParam(rest,out string? starName,out _,out string? defExpr)) return false;
                if(PythonKeywords.Contains(starName!) || !names.Add(starName!) || defExpr!=null) return false;
                seenStar=true;
                simplePositional=false;
                continue;
            }
            if(!ParseAnnotatedParam(seg,out string? paramName,out _,out string? defaultExpr)) return false;
            if(PythonKeywords.Contains(paramName!) || !names.Add(paramName!)) return false;
            seenBareStar=false;
            if(defaultExpr!=null) {
                if(!seenStar) seenDefault=true;
                if(!IsSimplePythonDefault(defaultExpr)) simplePositional=false;
            } else if(seenDefault && !seenStar) {
                // Non-default argument follows default argument.
                return false;
            }
        }
        if(seenBareStar) return false;
        shape=simplePositional ? new(segments.Count) : null;
        return true;
    }

    private static bool ParseAnnotatedParam(string seg,out string? paramName,out bool hasAnn,out string? defaultExpr)
    {
        paramName=null;
        hasAnn=false;
        defaultExpr=null;
        var idMatch=Regex.Match(seg,@"\A(?<id>[A-Za-z_]\w*)(?<rest>[\s\S]*)\z",RegexOptions.CultureInvariant);
        if(!idMatch.Success) return false;
        paramName=idMatch.Groups["id"].Value;
        string rest=idMatch.Groups["rest"].Value.Trim();
        if(rest.Length==0) return true;
        if(rest[0]==':') {
            hasAnn=true;
            int split=FindTopLevelAssign(rest,1);
            string ann=split>=0 ? rest[1..split] : rest[1..];
            if(!TryScanPythonExpression(ann,0,ann.Length,allowTopComma:false,out _,out _)) return false;
            if(split<0) return true;
            rest=rest[split..].Trim();
        }
        if(rest.Length==0 || rest[0]!='=') return false;
        string defText=rest[1..].Trim();
        if(!TryScanPythonExpression(defText,0,defText.Length,allowTopComma:false,out _,out _)) return false;
        defaultExpr=defText;
        return true;
    }

    private static bool TryScanPythonExpression(string text,int start,int end,bool allowTopComma,out int exprStart,out int exprEnd)
    {
        exprStart=SkipPythonTrivia(text,start,end);
        exprEnd=exprStart;
        if(exprStart>=end) return false;

        int pos=exprStart,depth=0,tokens=0;
        var lambdaColons=new Stack<int>();
        bool expectOperand=true,prevOpenOrDelim=false;

        while(pos<end) {
            pos=SkipPythonTrivia(text,pos,end);
            if(pos>=end) break;
            char c=text[pos];
            if(c is '\'' or '"') {
                if(!expectOperand) return false;
                int after=AfterString(text,pos);
                if(after>end) return false;
                exprEnd=pos=after;
                expectOperand=false;
                prevOpenOrDelim=false;
                tokens++;
                continue;
            }
            if(char.IsAsciiLetter(c) || c=='_') {
                int idEnd=pos+1;
                while(idEnd<end && (char.IsAsciiLetterOrDigit(text[idEnd]) || text[idEnd]=='_')) idEnd++;
                string word=text[pos..idEnd];
                if(idEnd<end && text[idEnd] is '\'' or '"' && word.ToLowerInvariant() is "r" or "u" or "b" or "f" or "br" or "rb" or "fr" or "rf" or "t" or "tr" or "rt") {
                    if(!expectOperand) return false;
                    int after=AfterString(text,idEnd);
                    if(after>end) return false;
                    exprEnd=pos=after;
                    expectOperand=false;
                    prevOpenOrDelim=false;
                    tokens++;
                    continue;
                }
                if(PythonStatementKeywords.Contains(word)) return false;
                if(word=="lambda") {
                    if(!expectOperand) return false;
                    lambdaColons.Push(depth);
                    exprEnd=pos=idEnd;
                    // Allow immediate ':' (zero-arg lambda) or parameter identifiers.
                    int peek=SkipPythonTrivia(text,pos,end);
                    if(peek<end && text[peek]==':') expectOperand=false;
                    else expectOperand=true;
                    prevOpenOrDelim=false;
                    tokens++;
                    continue;
                }
                if(word is "not" or "await") {
                    if(expectOperand) { exprEnd=pos=idEnd; expectOperand=true; prevOpenOrDelim=false; tokens++; continue; }
                    if(word=="not") { exprEnd=pos=idEnd; expectOperand=true; prevOpenOrDelim=false; tokens++; continue; }
                    return false;
                }
                if(word is "and" or "or" or "in" or "is" or "if" or "else" or "for") {
                    if(expectOperand) return false;
                    exprEnd=pos=idEnd;
                    expectOperand=true;
                    prevOpenOrDelim=false;
                    tokens++;
                    continue;
                }
                if(!expectOperand) return false;
                exprEnd=pos=idEnd;
                expectOperand=false;
                prevOpenOrDelim=false;
                tokens++;
                continue;
            }
            if(char.IsAsciiDigit(c) || (c=='.' && pos+1<end && char.IsAsciiDigit(text[pos+1]))) {
                if(!expectOperand) return false;
                int numEnd=pos+1;
                while(numEnd<end && (char.IsAsciiLetterOrDigit(text[numEnd]) || text[numEnd] is '_' or '.')) numEnd++;
                exprEnd=pos=numEnd;
                expectOperand=false;
                prevOpenOrDelim=false;
                tokens++;
                continue;
            }
            if(c=='.' && pos+2<end && text[pos+1]=='.' && text[pos+2]=='.') {
                if(!expectOperand) return false;
                exprEnd=pos+=3;
                expectOperand=false;
                prevOpenOrDelim=false;
                tokens++;
                continue;
            }
            if(c is '(' or '[' or '{') {
                depth++;
                exprEnd=++pos;
                expectOperand=true;
                prevOpenOrDelim=true;
                tokens++;
                continue;
            }
            if(c is ')' or ']' or '}') {
                if(--depth<0) return false;
                if(expectOperand && !prevOpenOrDelim) return false;
                exprEnd=++pos;
                expectOperand=false;
                prevOpenOrDelim=false;
                tokens++;
                continue;
            }
            if(c==',') {
                bool inLambdaHeader=lambdaColons.TryPeek(out int ld) && ld==depth;
                if(!allowTopComma && depth==0 && !inLambdaHeader) return false;
                if(expectOperand) return false;
                exprEnd=++pos;
                expectOperand=true;
                prevOpenOrDelim=true;
                tokens++;
                continue;
            }
            if(c==':') {
                if(pos+1<end && text[pos+1]=='=') {
                    if(expectOperand) return false;
                    exprEnd=pos+=2;
                    expectOperand=true;
                    prevOpenOrDelim=false;
                    tokens++;
                    continue;
                }
                if(lambdaColons.TryPeek(out int ld) && ld==depth) {
                    lambdaColons.Pop();
                    exprEnd=++pos;
                    expectOperand=true;
                    prevOpenOrDelim=false;
                    tokens++;
                    continue;
                }
                if(depth==0) return false;
                exprEnd=++pos;
                expectOperand=true;
                prevOpenOrDelim=true;
                tokens++;
                continue;
            }
            if(c=='.') {
                if(expectOperand) return false;
                exprEnd=++pos;
                expectOperand=true;
                prevOpenOrDelim=false;
                tokens++;
                continue;
            }
            if(c is '+' or '-' or '*' or '~') {
                int opEnd=pos+1;
                if(opEnd<end && ((c=='*' && text[opEnd]=='*') || text[opEnd]=='=')) opEnd++;
                exprEnd=pos=opEnd;
                expectOperand=true;
                prevOpenOrDelim=false;
                tokens++;
                continue;
            }
            if(c is '/' or '%' or '&' or '|' or '^' or '<' or '>' or '=' or '!') {
                if(expectOperand) return false;
                int opEnd=pos+1;
                if(opEnd<end && (text[opEnd]==c || text[opEnd]=='=')) opEnd++;
                if(c=='=' && opEnd==pos+1) {
                    bool inLambdaHeader=lambdaColons.TryPeek(out int ld) && ld==depth;
                    if(!inLambdaHeader && depth==0) return false;
                }
                exprEnd=pos=opEnd;
                expectOperand=true;
                prevOpenOrDelim=false;
                tokens++;
                continue;
            }
            return false;
        }
        return tokens>0 && depth==0 && lambdaColons.Count==0 && !expectOperand && exprEnd>exprStart;
    }

    private static int FindTopLevelAssign(string text,int start)
    {
        int depth=0;
        for(int i=start;i<text.Length;i++) {
            char c=text[i];
            if(c=='#') { while(i<text.Length && text[i] is not ('\r' or '\n')) i++; continue; }
            if(c is '\'' or '"') { i=AfterString(text,i)-1; continue; }
            if(c is '(' or '[' or '{') depth++;
            else if(c is ')' or ']' or '}') depth--;
            else if(c=='=' && depth==0) {
                char prev=i>0 ? text[i-1] : '\0';
                char next=i+1<text.Length ? text[i+1] : '\0';
                if(prev is not ('=' or '!' or '<' or '>' or ':') && next!='=') return i;
                if(next=='=') i++;
            }
        }
        return -1;
    }

    private static bool IsSimplePythonDefault(string value)
    {
        if(value is "None" or "True" or "False" or "..." or "()" or "[]" or "{}") return true;
        if(Regex.IsMatch(value,@"\A[+-]?(?:\d+(?:_\d+)*(?:\.\d+(?:_\d+)*)?|\.\d+(?:_\d+)*)\z",RegexOptions.CultureInvariant))
            return true;
        if(value.Length>=2 && value[0] is '\'' or '"' && !value.Contains('\r') && !value.Contains('\n')) {
            try { return AfterString(value,0)==value.Length; }
            catch(InvalidDataException) { return false; }
        }
        return false;
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
