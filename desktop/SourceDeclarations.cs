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
                    int after=SkipPythonTrivia(text,name.Index+name.Length,text.Length,allowNewlines:false);
                    bool isClass=text.AsSpan(i).StartsWith("class");
                    if(after>=text.Length || !(text[after]=='(' || (isClass && text[after]==':')))
                        throw new InvalidDataException("Unsupported Python declaration header.");
                }
                statementStart=false;
            }
            if(c is '\'' or '"') { i=AfterString(text,i); continue; }
            if(c>127) throw new InvalidDataException("Python navigation supports ASCII identifiers and UTF-8 strings and comments.");
            if(header>=0 && nesting.Count==0 && i>=name!.Index+name.Length && (char.IsAsciiLetter(c) || c=='_') &&
                (i==0 || !(char.IsAsciiLetterOrDigit(text[i-1]) || text[i-1]=='_' || text[i-1]=='.'))) {
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

    private static int SkipPythonTrivia(string text,int pos,int limit,bool allowNewlines=true)
    {
        while(pos<limit) {
            char c=text[pos];
            if(c is ' ' or '\t') { pos++; continue; }
            if(allowNewlines && c is '\r' or '\n') { pos++; continue; }
            if(allowNewlines && c=='#') { while(pos<limit && text[pos] is not ('\r' or '\n')) pos++; continue; }
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
        int i=SkipPythonTrivia(text,tailStart,colon,allowNewlines:false);
        if(i==colon) return true;
        if(text[i]!='(') return false;
        if(!TryScanMatchingParens(text,i,colon,out int close)) return false;
        int after=SkipPythonTrivia(text,close+1,colon,allowNewlines:false);
        return after==colon && PythonHeaderParser.TryParseCallArgList(text,i+1,close);
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
        int i=SkipPythonTrivia(text,tailStart,colon,allowNewlines:false);
        if(i>=colon || text[i]!='(') return (false,null,null,null);
        int open=i;
        if(!TryScanMatchingParens(text,open,colon,out int close)) return (false,null,null,null);
        var paramsSpan=TextSpan.FromBounds(open,close+1);
        if(!PythonHeaderParser.TryParseParameters(text,open+1,close,out var shape))
            return (false,null,null,null);

        i=SkipPythonTrivia(text,close+1,colon,allowNewlines:false);
        if(i==colon) return (true,shape,paramsSpan,null);
        if(i+1>=colon || text[i]!='-' || text[i+1]!='>')
            return (false,null,null,null);
        if(!PythonHeaderParser.TryParseExpression(text,i+2,colon,implicitLineJoin:false,out int retStart,out int retEnd))
            return (false,null,null,null);
        return (true,shape,paramsSpan,TextSpan.FromBounds(retStart,retEnd));
    }

    // Bounded offline recursive-descent parser and Python 3.12 token validator for
    // Python declaration parameters, annotations, defaults, and class bases.
    private sealed class PythonHeaderParser
    {
        private const int MaxDepth=64;

        private enum TokKind
        {
            Eof, Ident, Number, String, Ellipsis,
            KwNone, KwTrue, KwFalse, KwAnd, KwOr, KwNot, KwIn, KwIs, KwIf, KwElse, KwFor, KwLambda,
            LParen, RParen, LBracket, RBracket, LBrace, RBrace, Comma, Colon, Dot, Assign, Walrus, Arrow,
            Plus, Minus, Star, DoubleStar, Slash, DoubleSlash, Percent, At, Amp, Pipe, Caret, Tilde, LShift, RShift,
            Eq, Ne, Lt, Le, Gt, Ge
        }

        private readonly record struct Tok(TokKind Kind,int Start,int End,bool IsBytes=false,bool HasNewline=false);

        private readonly string source;
        private readonly List<Tok> tokens;
        private int pos;
        private int recursionDepth;
        private int walrusCount;
        private bool inComp;

        private PythonHeaderParser(string source,List<Tok> tokens)
        {
            this.source=source;
            this.tokens=tokens;
        }

        public static bool TryParseExpression(string text,int start,int end,bool implicitLineJoin,out int exprStart,out int exprEnd)
        {
            exprStart=start;
            exprEnd=start;
            if(!Lex(text,start,end,implicitLineJoin,out var toks) || toks.Count<2) return false;
            var parser=new PythonHeaderParser(text,toks);
            if(!parser.ParseExpression() || parser.Peek()!=TokKind.Eof) return false;
            exprStart=toks[0].Start;
            exprEnd=toks[^2].End;
            return exprEnd>exprStart;
        }

        public static bool TryParseParameters(string text,int start,int end,out ParameterShape? shape)
        {
            shape=null;
            if(!Lex(text,start,end,implicitLineJoin:true,out var toks)) return false;
            var parser=new PythonHeaderParser(text,toks);
            return parser.ParseParameters(allowAnnotations:true,TokKind.Eof,out shape) && parser.Peek()==TokKind.Eof;
        }

        public static bool TryParseCallArgList(string text,int start,int end)
        {
            if(!Lex(text,start,end,implicitLineJoin:true,out var toks)) return false;
            var parser=new PythonHeaderParser(text,toks);
            return parser.ParseCallArgsUntil(TokKind.Eof) && parser.Peek()==TokKind.Eof;
        }

        private TokKind Peek(int offset=0)=>pos+offset<tokens.Count ? tokens[pos+offset].Kind : TokKind.Eof;
        private string Lexeme(int tokenIndex)=>source[tokens[tokenIndex].Start..tokens[tokenIndex].End];
        private bool ConsumeIf(TokKind kind) { if(Peek()!=kind) return false; pos++; return true; }
        private bool Consume(TokKind kind) { if(Peek()!=kind) return false; pos++; return true; }

        private bool Enter()
        {
            if(++recursionDepth>MaxDepth) return false;
            return true;
        }
        private void Exit()=>recursionDepth--;

        private bool ParseExpression()
        {
            if(!Enter()) { Exit(); return false; }
            try {
                if(Peek()==TokKind.KwLambda) return ParseLambda();
                if(!ParseOr()) return false;
                if(ConsumeIf(TokKind.KwIf))
                    return ParseOr() && Consume(TokKind.KwElse) && ParseExpression();
                return true;
            } finally { Exit(); }
        }

        private bool ParseNamedExpression(bool allowWalrus,out bool usedWalrus)
        {
            usedWalrus=false;
            if(allowWalrus && Peek()==TokKind.Ident && Peek(1)==TokKind.Walrus) {
                pos+=2;
                usedWalrus=true;
                walrusCount++;
                return ParseExpression();
            }
            return ParseExpression();
        }

        private bool ParseLambda()
        {
            if(!Consume(TokKind.KwLambda)) return false;
            if(!ParseParameters(allowAnnotations:false,TokKind.Colon,out _)) return false;
            return Consume(TokKind.Colon) && ParseExpression();
        }

        private bool ParseOr()
        {
            if(!ParseAnd()) return false;
            while(ConsumeIf(TokKind.KwOr)) if(!ParseAnd()) return false;
            return true;
        }

        private bool ParseAnd()
        {
            if(!ParseNot()) return false;
            while(ConsumeIf(TokKind.KwAnd)) if(!ParseNot()) return false;
            return true;
        }

        private bool ParseNot()
        {
            if(!Enter()) { Exit(); return false; }
            try {
                if(ConsumeIf(TokKind.KwNot)) return ParseNot();
                return ParseComparison();
            } finally { Exit(); }
        }

        private bool ParseComparison()
        {
            if(!ParseBitOr()) return false;
            while(true) {
                TokKind k=Peek();
                if(k is TokKind.Eq or TokKind.Ne or TokKind.Lt or TokKind.Le or TokKind.Gt or TokKind.Ge or TokKind.KwIn) {
                    pos++;
                    if(!ParseBitOr()) return false;
                } else if(k==TokKind.KwIs) {
                    pos++;
                    ConsumeIf(TokKind.KwNot);
                    if(!ParseBitOr()) return false;
                } else if(k==TokKind.KwNot && Peek(1)==TokKind.KwIn) {
                    pos+=2;
                    if(!ParseBitOr()) return false;
                } else {
                    break;
                }
            }
            return true;
        }

        private bool ParseBitOr()
        {
            if(!ParseBitXor()) return false;
            while(ConsumeIf(TokKind.Pipe)) if(!ParseBitXor()) return false;
            return true;
        }

        private bool ParseBitXor()
        {
            if(!ParseBitAnd()) return false;
            while(ConsumeIf(TokKind.Caret)) if(!ParseBitAnd()) return false;
            return true;
        }

        private bool ParseBitAnd()
        {
            if(!ParseShift()) return false;
            while(ConsumeIf(TokKind.Amp)) if(!ParseShift()) return false;
            return true;
        }

        private bool ParseShift()
        {
            if(!ParseAdd()) return false;
            while(Peek() is TokKind.LShift or TokKind.RShift) { pos++; if(!ParseAdd()) return false; }
            return true;
        }

        private bool ParseAdd()
        {
            if(!ParseMul()) return false;
            while(Peek() is TokKind.Plus or TokKind.Minus) { pos++; if(!ParseMul()) return false; }
            return true;
        }

        private bool ParseMul()
        {
            if(!ParseUnary()) return false;
            while(Peek() is TokKind.Star or TokKind.Slash or TokKind.DoubleSlash or TokKind.Percent or TokKind.At) {
                pos++;
                if(!ParseUnary()) return false;
            }
            return true;
        }

        private bool ParseUnary()
        {
            if(!Enter()) { Exit(); return false; }
            try {
                if(Peek() is TokKind.Plus or TokKind.Minus or TokKind.Tilde) { pos++; return ParseUnary(); }
                return ParsePower();
            } finally { Exit(); }
        }

        private bool ParsePower()
        {
            if(!ParsePrimary()) return false;
            if(ConsumeIf(TokKind.DoubleStar)) return ParseUnary();
            return true;
        }

        private bool ParsePrimary()
        {
            if(!ParseAtom()) return false;
            while(true) {
                if(ConsumeIf(TokKind.Dot)) {
                    if(!Consume(TokKind.Ident)) return false;
                } else if(ConsumeIf(TokKind.LParen)) {
                    if(!ParseCallArgsUntil(TokKind.RParen) || !Consume(TokKind.RParen)) return false;
                } else if(ConsumeIf(TokKind.LBracket)) {
                    if(!ParseSubscript()) return false;
                } else {
                    break;
                }
            }
            return true;
        }

        private bool ParseAtom()
        {
            TokKind k=Peek();
            if(k is TokKind.Ident or TokKind.KwNone or TokKind.KwTrue or TokKind.KwFalse or TokKind.Number or TokKind.Ellipsis) {
                pos++;
                return true;
            }
            if(k==TokKind.String) {
                bool expectBytes=tokens[pos].IsBytes;
                pos++;
                while(Peek()==TokKind.String) {
                    if(tokens[pos].IsBytes!=expectBytes) return false;
                    pos++;
                }
                return true;
            }
            if(k==TokKind.LParen) return ParseParenGroup();
            if(k==TokKind.LBracket) return ParseListGroup();
            if(k==TokKind.LBrace) return ParseBraceGroup();
            return false;
        }

        private bool ParseParenGroup()
        {
            if(!Enter()) { Exit(); return false; }
            try {
                if(!Consume(TokKind.LParen)) return false;
                if(ConsumeIf(TokKind.RParen)) return true;
                if(ConsumeIf(TokKind.Star)) {
                    if(!ParseBitOr() || !ConsumeIf(TokKind.Comma)) return false;
                } else {
                    int wb=walrusCount;
                    if(!ParseNamedExpression(!inComp,out _)) return false;
                    if(walrusCount==wb && Peek()==TokKind.KwFor)
                        return ParseCompClauses() && Consume(TokKind.RParen);
                    if(!ConsumeIf(TokKind.Comma)) return Consume(TokKind.RParen);
                }
                while(Peek()!=TokKind.RParen) {
                    bool ok=ConsumeIf(TokKind.Star) ? ParseBitOr() : ParseNamedExpression(!inComp,out _);
                    if(!ok) return false;
                    if(!ConsumeIf(TokKind.Comma)) break;
                }
                return Consume(TokKind.RParen);
            } finally { Exit(); }
        }

        private bool ParseListGroup()
        {
            if(!Enter()) { Exit(); return false; }
            try {
                if(!Consume(TokKind.LBracket)) return false;
                if(ConsumeIf(TokKind.RBracket)) return true;
                if(ConsumeIf(TokKind.Star)) {
                    if(!ParseBitOr()) return false;
                    if(!ConsumeIf(TokKind.Comma)) return Consume(TokKind.RBracket);
                } else {
                    int wb=walrusCount;
                    if(!ParseNamedExpression(!inComp,out _)) return false;
                    if(walrusCount==wb && Peek()==TokKind.KwFor)
                        return ParseCompClauses() && Consume(TokKind.RBracket);
                    if(!ConsumeIf(TokKind.Comma)) return Consume(TokKind.RBracket);
                }
                while(Peek()!=TokKind.RBracket) {
                    bool ok=ConsumeIf(TokKind.Star) ? ParseBitOr() : ParseNamedExpression(!inComp,out _);
                    if(!ok) return false;
                    if(!ConsumeIf(TokKind.Comma)) break;
                }
                return Consume(TokKind.RBracket);
            } finally { Exit(); }
        }

        private bool ParseBraceGroup()
        {
            if(!Enter()) { Exit(); return false; }
            try {
                if(!Consume(TokKind.LBrace)) return false;
                if(ConsumeIf(TokKind.RBrace)) return true;
                if(ConsumeIf(TokKind.DoubleStar)) {
                    if(!ParseBitOr()) return false;
                    return ParseDictTail();
                }
                if(ConsumeIf(TokKind.Star)) {
                    if(!ParseBitOr()) return false;
                    return ParseSetTail();
                }
                int wb=walrusCount;
                if(!ParseNamedExpression(!inComp,out bool usedWalrus)) return false;
                if(ConsumeIf(TokKind.Colon)) {
                    if(usedWalrus || !ParseExpression()) return false;
                    if(walrusCount==wb && Peek()==TokKind.KwFor) return ParseCompClauses() && Consume(TokKind.RBrace);
                    return ParseDictTail();
                }
                if(walrusCount==wb && Peek()==TokKind.KwFor)
                    return ParseCompClauses() && Consume(TokKind.RBrace);
                return ParseSetTail();
            } finally { Exit(); }
        }

        private bool ParseDictTail()
        {
            while(ConsumeIf(TokKind.Comma)) {
                if(Peek()==TokKind.RBrace) break;
                if(ConsumeIf(TokKind.DoubleStar)) {
                    if(!ParseBitOr()) return false;
                } else {
                    if(!ParseExpression() || !Consume(TokKind.Colon) || !ParseExpression()) return false;
                }
            }
            return Consume(TokKind.RBrace);
        }

        private bool ParseSetTail()
        {
            while(ConsumeIf(TokKind.Comma)) {
                if(Peek()==TokKind.RBrace) break;
                bool ok=ConsumeIf(TokKind.Star) ? ParseBitOr() : ParseNamedExpression(!inComp,out _);
                if(!ok) return false;
            }
            return Consume(TokKind.RBrace);
        }

        private bool ParseSubscript()
        {
            if(!Enter()) { Exit(); return false; }
            try {
                if(Peek()==TokKind.RBracket) return false;
                while(true) {
                    if(ConsumeIf(TokKind.Star)) {
                        if(!ParseExpression()) return false;
                    } else {
                        bool usedWalrus=false;
                        if(Peek()!=TokKind.Colon && !ParseNamedExpression(!inComp,out usedWalrus)) return false;
                        if(ConsumeIf(TokKind.Colon)) {
                            if(usedWalrus) return false;
                            if(Peek() is not (TokKind.Colon or TokKind.Comma or TokKind.RBracket) && !ParseExpression()) return false;
                            if(ConsumeIf(TokKind.Colon) && Peek() is not (TokKind.Comma or TokKind.RBracket) && !ParseExpression()) return false;
                        }
                    }
                    if(!ConsumeIf(TokKind.Comma)) break;
                    if(Peek()==TokKind.RBracket) break;
                }
                return Consume(TokKind.RBracket);
            } finally { Exit(); }
        }

        private bool ParseCallArgsUntil(TokKind terminator)
        {
            if(!Enter()) { Exit(); return false; }
            try {
                bool seenKw=false,seenDoubleStar=false;
                HashSet<string>? kwNames=null;
                int count=0;
                while(Peek()!=terminator) {
                    count++;
                    if(ConsumeIf(TokKind.DoubleStar)) {
                        if(!ParseExpression()) return false;
                        seenDoubleStar=true;
                    } else if(ConsumeIf(TokKind.Star)) {
                        if(seenDoubleStar || !ParseExpression()) return false;
                    } else if(Peek()==TokKind.Ident && Peek(1)==TokKind.Assign) {
                        int keyIdx=pos;
                        pos+=2;
                        kwNames??=new HashSet<string>(StringComparer.Ordinal);
                        if(!kwNames.Add(Lexeme(keyIdx))) return false;
                        seenKw=true;
                        if(!ParseExpression()) return false;
                    } else {
                        if(seenKw || seenDoubleStar) return false;
                        int wb=walrusCount;
                        if(!ParseNamedExpression(!inComp,out _)) return false;
                        if(walrusCount==wb && Peek()==TokKind.KwFor)
                            return count==1 && ParseCompClauses() && Peek()==terminator;
                    }
                    if(!ConsumeIf(TokKind.Comma)) break;
                }
                return Peek()==terminator;
            } finally { Exit(); }
        }

        private bool ParseCompClauses()
        {
            bool savedComp=inComp;
            inComp=true;
            try {
                if(!Consume(TokKind.KwFor)) return false;
                while(true) {
                    if(!ParseCompTargetList() || !Consume(TokKind.KwIn) || !ParseOr()) return false;
                    while(ConsumeIf(TokKind.KwIf)) if(!ParseOr()) return false;
                    if(!ConsumeIf(TokKind.KwFor)) break;
                }
                return true;
            } finally { inComp=savedComp; }
        }

        private bool ParseCompTargetList()
        {
            if(!ParseCompTarget()) return false;
            while(ConsumeIf(TokKind.Comma)) {
                if(Peek() is TokKind.KwIn or TokKind.RParen or TokKind.RBracket) break;
                if(!ParseCompTarget()) return false;
            }
            return true;
        }

        private bool ParseCompTarget()
        {
            if(!Enter()) { Exit(); return false; }
            try {
                if(ConsumeIf(TokKind.Ident)) return true;
                if(ConsumeIf(TokKind.LParen)) {
                    if(ConsumeIf(TokKind.RParen) || !ParseCompTargetList()) return false;
                    return Consume(TokKind.RParen);
                }
                if(ConsumeIf(TokKind.LBracket)) {
                    if(ConsumeIf(TokKind.RBracket) || !ParseCompTargetList()) return false;
                    return Consume(TokKind.RBracket);
                }
                return false;
            } finally { Exit(); }
        }

        private bool ParseParameters(bool allowAnnotations,TokKind terminator,out ParameterShape? shape)
        {
            shape=null;
            if(Peek()==terminator) { shape=new(0); return true; }

            var names=new HashSet<string>(StringComparer.Ordinal);
            bool seenDefault=false,seenSlash=false,seenStar=false,seenBareStar=false,seenKwargs=false,simplePositional=true;
            int bareStarIndex=-1,count=0;

            while(Peek()!=terminator) {
                if(seenKwargs) return false;
                int idx=count++;

                if(ConsumeIf(TokKind.Slash)) {
                    if(idx==0 || seenSlash || seenStar) return false;
                    seenSlash=true;
                    simplePositional=false;
                } else if(ConsumeIf(TokKind.DoubleStar)) {
                    if(seenBareStar && idx==bareStarIndex+1) return false;
                    int idIdx=pos;
                    if(!Consume(TokKind.Ident) || !names.Add(Lexeme(idIdx))) return false;
                    if(allowAnnotations && ConsumeIf(TokKind.Colon) && !ParseExpression()) return false;
                    if(Peek()==TokKind.Assign) return false;
                    seenKwargs=true;
                    seenBareStar=false;
                    simplePositional=false;
                } else if(ConsumeIf(TokKind.Star)) {
                    if(seenStar) return false;
                    seenStar=true;
                    simplePositional=false;
                    if(Peek()==TokKind.Comma || Peek()==terminator) {
                        seenBareStar=true;
                        bareStarIndex=idx;
                    } else {
                        int idIdx=pos;
                        if(!Consume(TokKind.Ident) || !names.Add(Lexeme(idIdx))) return false;
                        if(allowAnnotations && ConsumeIf(TokKind.Colon)) {
                            ConsumeIf(TokKind.Star);
                            if(!ParseExpression()) return false;
                        }
                        if(Peek()==TokKind.Assign) return false;
                    }
                } else {
                    int idIdx=pos;
                    if(!Consume(TokKind.Ident) || !names.Add(Lexeme(idIdx))) return false;
                    seenBareStar=false;
                    if(allowAnnotations && ConsumeIf(TokKind.Colon) && !ParseExpression()) return false;
                    if(ConsumeIf(TokKind.Assign)) {
                        int defStart=pos;
                        if(!ParseExpression()) return false;
                        int defEnd=pos;
                        if(!seenStar) seenDefault=true;
                        if(!IsSimpleDefaultTokens(defStart,defEnd)) simplePositional=false;
                    } else if(seenDefault && !seenStar) {
                        return false;
                    }
                }

                if(!ConsumeIf(TokKind.Comma)) break;
            }
            if(seenBareStar || Peek()!=terminator) return false;
            shape=simplePositional ? new(count) : null;
            return true;
        }

        private bool IsSimpleDefaultTokens(int startIdx,int endIdx)
        {
            int len=endIdx-startIdx;
            if(len==1) {
                Tok t=tokens[startIdx];
                return t.Kind is TokKind.KwNone or TokKind.KwTrue or TokKind.KwFalse or TokKind.Ellipsis or TokKind.Number ||
                    (t.Kind==TokKind.String && !t.HasNewline);
            }
            if(len==2) {
                TokKind a=tokens[startIdx].Kind,b=tokens[startIdx+1].Kind;
                return (a is TokKind.Plus or TokKind.Minus && b==TokKind.Number) ||
                    (a==TokKind.LParen && b==TokKind.RParen) ||
                    (a==TokKind.LBracket && b==TokKind.RBracket) ||
                    (a==TokKind.LBrace && b==TokKind.RBrace);
            }
            return false;
        }

        private static bool Lex(string text,int start,int end,bool implicitLineJoin,out List<Tok> tokens)
        {
            tokens=[];
            int i=start,depth=0;
            while(i<end) {
                char c=text[i];
                if(c is ' ' or '\t') { i++; continue; }
                if(c=='\\') {
                    if(i+1>=end || text[i+1] is not ('\r' or '\n')) return false;
                    i+=2;
                    if(text[i-1]=='\r' && i<end && text[i]=='\n') i++;
                    continue;
                }
                if(c is '\r' or '\n') {
                    if(!implicitLineJoin && depth==0) return false;
                    i++;
                    continue;
                }
                if(c=='#') {
                    if(!implicitLineJoin && depth==0) return false;
                    while(i<end && text[i] is not ('\r' or '\n')) i++;
                    continue;
                }
                if(c>127) return false;
                if(c is '\'' or '"') {
                    if(!TryScanString(text,i,end,isBytes:false,isFormatted:false,isRaw:false,out int strEnd,out bool hasNl)) return false;
                    tokens.Add(new(TokKind.String,i,strEnd,HasNewline:hasNl));
                    i=strEnd;
                    continue;
                }
                if(char.IsAsciiLetter(c) || c=='_') {
                    int idEnd=i+1;
                    while(idEnd<end && (char.IsAsciiLetterOrDigit(text[idEnd]) || text[idEnd]=='_')) idEnd++;
                    string word=text[i..idEnd];
                    if(idEnd<end && text[idEnd] is '\'' or '"') {
                        string lower=word.ToLowerInvariant();
                        if(lower is not ("r" or "u" or "b" or "f" or "br" or "rb" or "fr" or "rf")) return false;
                        bool isBytes=lower.Contains('b'),isFormatted=lower.Contains('f'),isRaw=lower.Contains('r');
                        if(!TryScanString(text,idEnd,end,isBytes,isFormatted,isRaw,out int strEnd,out bool hasNl)) return false;
                        tokens.Add(new(TokKind.String,i,strEnd,IsBytes:isBytes,HasNewline:hasNl));
                        i=strEnd;
                        continue;
                    }
                    TokKind kind=word switch {
                        "None"=>TokKind.KwNone,
                        "True"=>TokKind.KwTrue,
                        "False"=>TokKind.KwFalse,
                        "and"=>TokKind.KwAnd,
                        "or"=>TokKind.KwOr,
                        "not"=>TokKind.KwNot,
                        "in"=>TokKind.KwIn,
                        "is"=>TokKind.KwIs,
                        "if"=>TokKind.KwIf,
                        "else"=>TokKind.KwElse,
                        "for"=>TokKind.KwFor,
                        "lambda"=>TokKind.KwLambda,
                        _=>TokKind.Ident
                    };
                    if(kind==TokKind.Ident && PythonKeywords.Contains(word)) return false;
                    tokens.Add(new(kind,i,idEnd));
                    i=idEnd;
                    continue;
                }
                if(char.IsAsciiDigit(c) || (c=='.' && i+1<end && char.IsAsciiDigit(text[i+1]))) {
                    if(!TryScanNumber(text,i,end,out int numEnd)) return false;
                    tokens.Add(new(TokKind.Number,i,numEnd));
                    i=numEnd;
                    continue;
                }
                if(c=='.') {
                    if(i+2<end && text[i+1]=='.' && text[i+2]=='.') {
                        tokens.Add(new(TokKind.Ellipsis,i,i+3));
                        i+=3;
                    } else {
                        tokens.Add(new(TokKind.Dot,i,i+1));
                        i++;
                    }
                    continue;
                }
                // Reject all 3-char and 2-char augmented assignments before scanning regular operators.
                char n1=i+1<end ? text[i+1] : '\0';
                char n2=i+2<end ? text[i+2] : '\0';
                if(n2=='=' && ((c=='*' && n1=='*') || (c=='/' && n1=='/') || (c=='<' && n1=='<') || (c=='>' && n1=='>')))
                    return false;
                TokKind? two=null;
                if(c==':' && n1=='=') two=TokKind.Walrus;
                else if(c=='-' && n1=='>') two=TokKind.Arrow;
                else if(c=='*' && n1=='*') two=TokKind.DoubleStar;
                else if(c=='/' && n1=='/') two=TokKind.DoubleSlash;
                else if(c=='<' && n1=='<') two=TokKind.LShift;
                else if(c=='>' && n1=='>') two=TokKind.RShift;
                else if(c=='<' && n1=='=') two=TokKind.Le;
                else if(c=='>' && n1=='=') two=TokKind.Ge;
                else if(c=='=' && n1=='=') two=TokKind.Eq;
                else if(c=='!' && n1=='=') two=TokKind.Ne;
                else if(n1=='=' && c is '+' or '-' or '*' or '/' or '%' or '@' or '&' or '|' or '^') return false;
                if(two is {} twoKind) {
                    tokens.Add(new(twoKind,i,i+2));
                    i+=2;
                    continue;
                }
                TokKind one;
                switch(c) {
                    case '(': depth++; one=TokKind.LParen; break;
                    case '[': depth++; one=TokKind.LBracket; break;
                    case '{': depth++; one=TokKind.LBrace; break;
                    case ')': if(--depth<0) return false; one=TokKind.RParen; break;
                    case ']': if(--depth<0) return false; one=TokKind.RBracket; break;
                    case '}': if(--depth<0) return false; one=TokKind.RBrace; break;
                    case ',': one=TokKind.Comma; break;
                    case ':': one=TokKind.Colon; break;
                    case '=': one=TokKind.Assign; break;
                    case '+': one=TokKind.Plus; break;
                    case '-': one=TokKind.Minus; break;
                    case '*': one=TokKind.Star; break;
                    case '/': one=TokKind.Slash; break;
                    case '%': one=TokKind.Percent; break;
                    case '@': one=TokKind.At; break;
                    case '&': one=TokKind.Amp; break;
                    case '|': one=TokKind.Pipe; break;
                    case '^': one=TokKind.Caret; break;
                    case '~': one=TokKind.Tilde; break;
                    case '<': one=TokKind.Lt; break;
                    case '>': one=TokKind.Gt; break;
                    default: return false;
                }
                tokens.Add(new(one,i,i+1));
                i++;
            }
            if(depth!=0) return false;
            tokens.Add(new(TokKind.Eof,end,end));
            return true;
        }

        private static bool ScanDigits(string text,ref int p,int end,Func<char,bool> isDigit,bool allowFirstUnderscore)
        {
            int digits=0;
            bool needDigit=false;
            if(allowFirstUnderscore && p<end && text[p]=='_') { needDigit=true; p++; }
            while(p<end) {
                char c=text[p];
                if(isDigit(c)) { digits++; needDigit=false; p++; }
                else if(c=='_' && !needDigit) { needDigit=true; p++; }
                else break;
            }
            return digits>0 && !needDigit;
        }

        private static bool TryScanNumber(string text,int start,int end,out int numEnd)
        {
            numEnd=start;
            static bool IsDec(char c)=>c is >='0' and <='9';
            static bool IsBin(char c)=>c is '0' or '1';
            static bool IsOct(char c)=>c is >='0' and <='7';
            static bool IsHex(char c)=>c is (>='0' and <='9') or (>='a' and <='f') or (>='A' and <='F');

            int p=start;
            if(text[p]=='.') {
                p++;
                if(p>=end || !IsDec(text[p]) || !ScanDigits(text,ref p,end,IsDec,allowFirstUnderscore:false)) return false;
                if(p<end && text[p] is 'e' or 'E') {
                    p++;
                    if(p<end && text[p] is '+' or '-') p++;
                    if(p>=end || !IsDec(text[p]) || !ScanDigits(text,ref p,end,IsDec,allowFirstUnderscore:false)) return false;
                }
                if(p<end && text[p] is 'j' or 'J') p++;
                if(p<end && (char.IsAsciiLetterOrDigit(text[p]) || text[p]=='_')) return false;
                numEnd=p;
                return true;
            }

            if(text[p]=='0' && p+1<end && text[p+1] is 'x' or 'X' or 'b' or 'B' or 'o' or 'O') {
                char radix=text[p+1];
                p+=2;
                Func<char,bool> pred=radix is 'x' or 'X' ? IsHex : radix is 'b' or 'B' ? IsBin : IsOct;
                if(!ScanDigits(text,ref p,end,pred,allowFirstUnderscore:true)) return false;
                if(p<end && (char.IsAsciiLetterOrDigit(text[p]) || text[p]=='_')) return false;
                numEnd=p;
                return true;
            }

            if(!ScanDigits(text,ref p,end,IsDec,allowFirstUnderscore:false)) return false;
            bool isFloatOrImag=false;
            if(p<end && text[p]=='.') {
                isFloatOrImag=true;
                p++;
                if(p<end && text[p]=='_') return false;
                if(p<end && IsDec(text[p]) && !ScanDigits(text,ref p,end,IsDec,allowFirstUnderscore:false)) return false;
            }
            if(p<end && text[p] is 'e' or 'E') {
                isFloatOrImag=true;
                p++;
                if(p<end && text[p] is '+' or '-') p++;
                if(p>=end || !IsDec(text[p]) || !ScanDigits(text,ref p,end,IsDec,allowFirstUnderscore:false)) return false;
            }
            if(p<end && text[p] is 'j' or 'J') {
                isFloatOrImag=true;
                p++;
            }
            if(!isFloatOrImag && text[start]=='0') {
                for(int k=start+1;k<p;k++) if(text[k] is >='1' and <='9') return false;
            }
            if(p<end && (char.IsAsciiLetterOrDigit(text[p]) || text[p]=='_')) return false;
            numEnd=p;
            return true;
        }

        private static bool TryScanString(string text,int quoteStart,int end,bool isBytes,bool isFormatted,bool isRaw,out int strEnd,out bool hasNewline)
        {
            strEnd=quoteStart;
            hasNewline=false;
            char quote=text[quoteStart];
            int width=quoteStart+2<end && text[quoteStart+1]==quote && text[quoteStart+2]==quote ? 3 : 1;
            for(int i=quoteStart+width;i<end;i++) {
                char c=text[i];
                if(isBytes && c>127) return false;
                if(c is '\r' or '\n') {
                    if(width==1) return false;
                    hasNewline=true;
                    continue;
                }
                if(c=='\\') {
                    if(isFormatted && i+1<end && text[i+1] is '{' or '}') continue;
                    i++;
                    if(i>=end) return false;
                    if(isBytes && text[i]>127) return false;
                    if(text[i] is '\r' or '\n') {
                        hasNewline=true;
                        if(text[i]=='\r' && i+1<end && text[i+1]=='\n') i++;
                    } else if(!isRaw && !ScanLiteralEscape(text,ref i,end,isBytes)) {
                        return false;
                    }
                    continue;
                }
                if(c==quote && (width==1 || (i+2<end && text[i+1]==quote && text[i+2]==quote))) {
                    strEnd=i+width;
                    return true;
                }
                if(isFormatted && c is '{' or '}') {
                    if(i+1<end && text[i+1]==c) { i++; continue; }
                    return false;
                }
            }
            return false;
        }

        // Verify Python 3.12's recognized escape forms without interpreting the
        // source or changing its citation. Names require a Unicode-name table.
        private static bool ScanLiteralEscape(string text,ref int next,int end,bool bytes)
        {
            char escaped=text[next];
            if(escaped is '\\' or '\'' or '"' or 'a' or 'b' or 'f' or 'n' or 'r' or 't' or 'v') return true;
            if(escaped is >= '0' and <= '7') {
                int octal=escaped-'0';
                for(int count=1;count<3 && next+1<end && text[next+1] is >= '0' and <= '7';count++)
                    octal=octal*8+text[++next]-'0';
                return octal<=0xFF;
            }

            int digits=escaped switch { 'x'=>2, 'u' when !bytes=>4, 'U' when !bytes=>8, _=>0 };
            if(digits==0 || end-next<=digits) return false;
            uint value=0;
            for(int index=0;index<digits;index++) {
                char c=text[++next];
                uint digit=c switch {
                    >= '0' and <= '9'=>(uint)(c-'0'),
                    >= 'a' and <= 'f'=>(uint)(c-'a'+10),
                    >= 'A' and <= 'F'=>(uint)(c-'A'+10),
                    _=>uint.MaxValue
                };
                if(digit>15) return false;
                value=(value<<4)|digit;
            }
            return value<=0x10FFFF;
        }
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
