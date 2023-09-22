using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

class GodotWalker : CSharpSyntaxWalker 
{
#region Auxiliary
    const string INDENT_STRING = "    ";
    int indent = 0;
    int consecutive_line_ends = 2;

    Dictionary<string,string> method_dictionary = new Dictionary<string, string>(){
        // LLM's can confidentely mix these up.
        ["Awake"] = "_init",
        ["Start"] = "_ready",
    };

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
        if (node.ToFullString().Contains("\n")) {
            newline();
        }
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
            print("# extends {0}", node.BaseList);
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
            print("var {0}: {1}", variable.Identifier, current_type);
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
        var text = node.Token.Text;
        if (text.EndsWith('f') && !text.Contains('.')) text = text.Replace("f",".0");
        print(text);
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        // Method name
        var method_name = node.Identifier.Text;
        if (method_dictionary.ContainsKey(method_name)) {
            method_name = method_dictionary[method_name];
        }
        print("func {0}(", method_name);

        // Parameter list
        var needs_separator = false;
        foreach (var param in node.ParameterList.Parameters) {
            if (needs_separator) Console.Write(", ");
            Visit(param);
            needs_separator = true;
        }
        print(")");

        // Return type
        if (node.ReturnType.ToString() != "void") {
            var typewalker = new GodotTypeWalker();
            typewalker.Visit(node.ReturnType);
            print(" -> {0}", typewalker.GetTypeName());
        }
        print(":");

        // Method body
        indent++;
        Visit(node.Body);
        indent--;
    }

    public override void VisitParameter(ParameterSyntax node)
    {
        print(node.Identifier.Text);
        if (node.Type != null) {
            var typewalker = new GodotTypeWalker();
            typewalker.Visit(node.Type);
            string current_type = typewalker.GetTypeName();
            print(" : {0}", current_type);
        }
    }

    public override void VisitBlock(BlockSyntax node)
    {
        if (node.Statements.Count > 0) {
            VisitChildren<StatementSyntax>(node);
        } else {
            throw new NotSupportedException("Blocks should at the very least contain a EmptyStatement.");
        }
    }

    public override void VisitEmptyStatement(EmptyStatementSyntax node)
    {
        print("pass");
        newline();
    }

    public override void VisitIfStatement(IfStatementSyntax node)
    {
        print("if ");
        Visit(node.Condition);
        print(":");
        newline();
        indent++;
        Visit(node.Statement);
        indent--;
        if (node.Else != null) {
            print("else:");
            newline();
            indent++;
            Visit(node.Else);
            indent--;
        }
    }

    public override void VisitForStatement(ForStatementSyntax node)
    {
        // This is a bit tricky as godot does not have a for(;;) syntax.
        // We might be able to recognize simple `for(int i=0; i<10; i++)` loops though and replace these with `for i in range(0,10)`.
        // Otherwise translate to a while loop.
        print("# for");
        newline();
        if (node.Declaration != null) {
            Visit(node.Declaration);
            newline();
        }
        foreach (var init in node.Initializers) {
            Visit(init);
            newline();
        }

        print("while ");
        Visit(node.Condition);
        print(":");
        newline();

        indent++;
        if (node.Statement is EmptyStatementSyntax) {
            print("# empty for-body");
            newline();
        } else {
            Visit(node.Statement);
        }

        print("# increment");
        newline();
        foreach (var incr in node.Incrementors) {
            Visit(incr);
            newline();
        }
        indent--;
    }

    public override void VisitWhileStatement(WhileStatementSyntax node)
    {
        print("while ");
        Visit(node.Condition);
        print(":");
        newline();
        indent++;
        Visit(node.Statement);
        indent--;
    }

    public override void VisitReturnStatement(ReturnStatementSyntax node)
    {
        print("return ");
        Visit(node.Expression);
        newline();
    }

    public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
    {
        if (node.IsConst) {
            // Godot does not support const variables.
        }
        Visit(node.Declaration);
        newline();
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        print(node.Identifier.Text);
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
                var needs_newline = false;
                foreach (var line in text.Split('\n')) {
                    if (needs_newline) newline();
                    var foo = line.Trim(' ','\t','/', '*');
                    if (consecutive_line_ends == 0) Console.Write(' ');
                    print("# {0}", foo);
                    needs_newline = true;
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
