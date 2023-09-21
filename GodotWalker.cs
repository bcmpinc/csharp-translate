using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

class GodotWalker : CSharpSyntaxWalker {
    const string INDENT_STRING = "    ";
    int indent = 0;

    void printline(string format, params Object?[]? args) {
        Console.Write(string.Concat(Enumerable.Repeat(INDENT_STRING, indent)));
        Console.WriteLine(format, args);
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        printline("# class {0} {1}", node.Identifier, node.BaseList);
        base.VisitClassDeclaration(node);
        printline("# end of class {0}", node.Identifier);
    }

    public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
    {
        var typewalker = new GodotTypeWalker();
        typewalker.Visit(node.Type);
        string current_type = typewalker.type;
        foreach (var variable in node.Variables) {
            printline("var {0} : {1}", variable.Identifier, current_type);
        }
    }


    public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
    {
        base.VisitVariableDeclarator(node);
    }
}

class GodotTypeWalker : CSharpSyntaxWalker {
    public string type = "<err>";
    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        type = node.Identifier.Text;
    }

    public override void VisitPredefinedType(PredefinedTypeSyntax node)
    {
        type = node.Keyword.Text;
    }

    public override void VisitArrayRankSpecifier(ArrayRankSpecifierSyntax node)
    {
        type = "Array[" + type + "]";
    }

}
