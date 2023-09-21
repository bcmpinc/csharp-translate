using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Text.RegularExpressions;

class GodotWalker : CSharpSyntaxWalker 
{
    const string INDENT_STRING = "    ";
    int indent = 0;
    bool need_newline = false;

    void printline(string format, params Object?[]? args) 
    {
        if (need_newline) Console.WriteLine();
        Console.Write(GetIndent());
        Console.Write(format, args);
        need_newline = true;
    }

    private string GetIndent()
    {
        return string.Concat(Enumerable.Repeat(INDENT_STRING, indent));
    }

    public override void DefaultVisit(SyntaxNode node)
    {
        foreach (var trivia in node.GetLeadingTrivia()) VisitLeadingTrivia(trivia.ToFullString());
        base.DefaultVisit(node);
        foreach (var trivia in node.GetTrailingTrivia()) VisitTrailingTrivia(trivia.ToFullString());
    }


    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        Console.WriteLine("# class {0} {1}", node.Identifier, node.BaseList);
        base.VisitClassDeclaration(node);
        Console.WriteLine("# end of class {0}", node.Identifier);
    }

    public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
    {
        var typewalker = new GodotTypeWalker();
        typewalker.Visit(node.Type);
        string current_type = typewalker.GetTypeName();
        foreach (var variable in node.Variables) {
            printline("var {0} : {1}", variable.Identifier, current_type);
        }
    }

    public void VisitLeadingTrivia(string text)
    {
        text = GetIndent() + text.Trim(' ','\t');
        text = Regex.Replace(text, "\n +(?:[*] )?", "\n" + GetIndent());
        if (text != "") {
            text = text
                .Replace("//","#")
                .Replace("/**","\"\"\"")
                .Replace("/*","\"\"\"")
                .Replace("*/","\"\"\"")
                ;
            Console.Write(text);
        }
    }

    public void VisitTrailingTrivia(string text)
    {
        text = text.TrimEnd();
        if (text != "") {
            text = text
                .Replace("//","#")
                .Replace("/**","\"\"\"")
                .Replace("/*","\"\"\"")
                .Replace("*/","\"\"\"")
                ;
            Console.Write(" " + text);
        }
    }
}

class GodotTypeWalker : CSharpSyntaxWalker {
    const string ERROR = "<err>";
    string type = ERROR;

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (type == ERROR) {
            type = node.Identifier.Text;
        } else {
            type += "." + node.Identifier.Text;
        }
    }

    public override void VisitPredefinedType(PredefinedTypeSyntax node)
    {
        type = node.Keyword.Text;
    }

    public override void VisitArrayRankSpecifier(ArrayRankSpecifierSyntax node)
    {
        type = "Array[" + type + "]";
    }

    public string GetTypeName() {
        switch (type) {
            case "GameObject": return "Node";
            default: return type;
        }
    }
}
