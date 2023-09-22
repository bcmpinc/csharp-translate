using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

class GodotWalker : CSharpSyntaxWalker 
{
#region Auxiliary
    const string INDENT_STRING = "    ";
    int indent = 0;
    int consecutive_line_ends = 2;

    public GodotWalker(SyntaxTree tree) : base(SyntaxWalkerDepth.Trivia)
    {
        print("# Converted from '{0}' using csharp-translate", tree.FilePath);
        newline();
        Visit(tree.GetRoot());
    }

    void print(string format, params Object?[]? args) 
    {
        if (consecutive_line_ends > 0) {
            Console.Write(INDENT_STRING.Repeat(indent));
        }
        Console.Write(format, args);
        consecutive_line_ends = 0;
    }

    void newline() {
        if (consecutive_line_ends < 2) {
            Console.WriteLine();
        }
        consecutive_line_ends++;
    }

    public override void DefaultVisit(SyntaxNode node)
    {
        // If this is called, that means the code contains nodes that are not supported by the converter.
        print("«{0}»", node.GetType().Name);
        newline();
        // base.DefaultVisit(node);
    }

    private void VisitChildren<NodeType>(SyntaxNode node)
    {
        foreach (var child in node.ChildNodesAndTokens()) {
            if (child.IsToken) {
                base.VisitToken(child.AsToken());
            } else {
                var sub_node = child.AsNode();
                if (sub_node is NodeType)
                    base.Visit(sub_node);
            }
        }
    }
#endregion Auxiliary
#region Visitors
    public override void VisitCompilationUnit(CompilationUnitSyntax node)
    {
        print("extends Node");
        newline();
        newline();
        VisitChildren<MemberDeclarationSyntax>(node);
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        print("# class_name {0}", node.Identifier);
        newline();
        if (node.BaseList != null) {
            print("# extends: {0}", node.BaseList);
            newline();
        }
        VisitChildren<MemberDeclarationSyntax>(node);
        print("# end of class {0}", node.Identifier);
        newline();
    }

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        VisitChildren<VariableDeclarationSyntax>(node);
    }

    public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
    {
        var typewalker = new GodotTypeWalker();
        typewalker.Visit(node.Type);
        string current_type = typewalker.GetTypeName();
        foreach (var variable in node.Variables) {
            print("var {0} : {1}", variable.Identifier, current_type);
            if (variable.Initializer != null) {
                Visit(variable.Initializer);
            }
        }
    }

    public override void VisitEqualsValueClause(EqualsValueClauseSyntax node)
    {
        print(" = ");
        Visit(node.Value);
    }

    public override void VisitLiteralExpression(LiteralExpressionSyntax node)
    {
        var text = node.ToFullString();
        if (text.EndsWith('f') && !text.Contains('.')) text = text.Replace("f",".0");
        print(text);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        string return_type = "";
        if (node.ReturnType.ToString() != "void") {
            var typewalker = new GodotTypeWalker();
            typewalker.Visit(node.ReturnType);
            return_type = " -> " + typewalker.GetTypeName();
        }
        print("func {0}(", node.Identifier);
        var needs_separator = false;
        foreach (var param in node.ParameterList.Parameters) {
            if (needs_separator) Console.Write(',');
            Visit(param);
            needs_separator = true;
        }
        print("){0}:", return_type);
        newline();
        indent++;
        Visit(node.Body);
        indent--;
    }

    public override void VisitTrivia(SyntaxTrivia trivia)
    {
        switch (trivia.Kind()) {
            case SyntaxKind.XmlComment: {
                throw new NotSupportedException("XmlComment not supported as we don't know when/if those are generated by the parser.");
            }

            case SyntaxKind.SingleLineCommentTrivia:
            case SyntaxKind.SingleLineDocumentationCommentTrivia:
            case SyntaxKind.MultiLineCommentTrivia:
            case SyntaxKind.MultiLineDocumentationCommentTrivia: {
                string text = trivia.ToFullString();
                foreach (var line in text.Split('\n')) {
                    var foo = line.Trim(' ','\t','/', '*');
                    if (consecutive_line_ends == 0) Console.Write(' ');
                    print("# {0}", foo);
                    newline();
                }
                break;
            }

            case SyntaxKind.EndOfLineTrivia: {
                newline();
                break;
            }
        }
    }
#endregion Visitors    
}

class GodotTypeWalker : CSharpSyntaxWalker {
    const string ERROR = "«err»";
    string type = ERROR;

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (type == ERROR) {
            type = node.Identifier.Text;
        } else {
            type += "." + node.Identifier.Text;
        }
        switch (type) {
            case "GameObject": type = "Node"; break;
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
        return type;
    }
}
