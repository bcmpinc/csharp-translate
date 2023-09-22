using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

class GodotWalker : CSharpSyntaxWalker 
{
#region Auxiliary
    const string INDENT_STRING = "    ";
    int indent = 0;
    int consecutive_line_ends = 2;

    bool skip_arguments = false;

    bool skip_newlines = false;

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
        skip_newlines = false;
    }

    void newline(bool suppress = false) {
        if (skip_newlines) return;
        skip_newlines = suppress;
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
        print("func ");
        switch (method_name) {
            // LLM's can confidentely mix these up.
            case "Awake": print("_init"); break;
            case "Start": print("_ready"); break;
            default: print("{0}", method_name); break;
        }

        // Parameter list
        print("(");
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
        newline(true);

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
        newline(true);
        indent++;
        Visit(node.Statement);
        indent--;
        if (node.Else != null) {
            print("else:");
            newline(true);
            indent++;
            Visit(node.Else.Statement);
            indent--;
        }
    }

    public bool VisitSimpleForStatement(ForStatementSyntax node) {
        if (node.Declaration == null) return false;
        if (node.Declaration.Variables.Count != 1) return false;
        if (node.Initializers.Count > 0) return false;
        if (node.Incrementors.Count != 1) return false;

        VariableDeclaratorSyntax variable = node.Declaration.Variables[0];
        if (variable.Initializer == null) return false;
        var var_name = variable.Identifier.Text;

        string offset = "1";
        switch (node.Incrementors[0]) {
            case PrefixUnaryExpressionSyntax prefix: {
                if (var_name != (prefix.Operand as IdentifierNameSyntax)?.Identifier.Text) return false;
                var op = prefix.OperatorToken.Text;
                offset = op == "++" ? "1" : "-1";
                break;
            }
            case PostfixUnaryExpressionSyntax postfix: {
                if (var_name != (postfix.Operand as IdentifierNameSyntax)?.Identifier.Text) return false;
                var op = postfix.OperatorToken.Text;
                offset = op == "++" ? "1" : "-1";
                break;
            }
            case AssignmentExpressionSyntax binary: {
                if (var_name != (binary.Left as IdentifierNameSyntax)?.Identifier.Text) return false;
                var right = binary.Right as LiteralExpressionSyntax;
                if (right == null) return false;
                var value = right.Token.Text;
                if (binary.OperatorToken.Text == "+=") { offset = value; break; }
                if (binary.OperatorToken.Text == "-=") { offset = "-" + value; break; }
                return false;
            }
            default:
                return false;
        }

        var cond = node.Condition as BinaryExpressionSyntax;
        if (cond == null) return false;
        if (var_name != (cond.Left as IdentifierNameSyntax)?.Identifier.Text) return false;
        
        print("for {0} in range(", var_name);
        Visit(variable.Initializer.Value);
        print(", ");
        Visit(cond.Right);
        if (cond.OperatorToken.Text == "<=") print(" + 1");
        if (cond.OperatorToken.Text == ">=") print(" - 1");
        if (offset != "1") {
            print(", {0}", offset);
        }
        print("):");
        newline(true);
        indent++;
        Visit(node.Statement);
        indent--;
        return true;
    }

    public override void VisitForStatement(ForStatementSyntax node)
    {
        // This is a bit tricky as godot does not have a for(;;) syntax.
        // We might be able to recognize simple `for(int i=0; i<10; i++)` loops though and replace these with `for i in range(0,10)`.
        if (VisitSimpleForStatement(node)) return;

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
        newline(true);

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

    public override void VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        Visit(node.Expression);
        newline();
    }

    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        Visit(node.Left);
        print(" {0} ", node.OperatorToken.Text);
        Visit(node.Right);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // TODO: This might need some translation table (e.g. Vector3.zero -> Vector3.ZERO);
        switch(node.Expression.ToFullString()) {
            case "Mathf":
                print(node.Name.Identifier.Text.ToLower());
                break;
            default:
                Visit(node.Expression);
                print(".");
                Visit(node.Name);
                break;
        }
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // Method
        Visit(node.Expression);

        // Arguments
        if (skip_arguments) {
            skip_arguments = false;
        } else {
            Visit(node.ArgumentList);
        }
    }

    public override void VisitArgumentList(ArgumentListSyntax node)
    {
        print("(");
        var needs_separator = false;
        foreach (var arg in node.Arguments) {
            if (needs_separator) Console.Write(", ");
            if (arg.NameColon != null) throw new NotSupportedException("Godot does not support named arguments");
            Visit(arg.Expression);
            needs_separator = true;
        }
        print(")");
    }

    public override void VisitGenericName(GenericNameSyntax node)
    {
        switch (node.Identifier.Text) {
            case "GetComponent":
                print("get_node(\"{0}\")", node.TypeArgumentList.Arguments[0].ToFullString());
                skip_arguments = true;
                break;
            default:
                print("{0}", node.Identifier);
                break;

        }
    }

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        Visit(node.Left);
        print(" {0} ", node.OperatorToken.Text);
        Visit(node.Right);
    }

    public override void VisitParenthesizedExpression(ParenthesizedExpressionSyntax node)
    {
        print("(");
        Visit(node.Expression);
        print(")");
    }

    public override void VisitElementAccessExpression(ElementAccessExpressionSyntax node)
    {
        Visit(node.Expression);
        foreach (var dim in node.ArgumentList.Arguments) {
            print("[");
            Visit(dim.Expression);
            print("]");
        }
    }

    public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
    {
        switch (node.OperatorToken.Text) {
            case "!":
                print ("not ");
                Visit(node.Operand);
                break;
            case "++":
            case "--":
                // GDScript does not have these
                print("(");
                Visit(node.Operand);
                print(" {0}= 1)", node.OperatorToken.Text[0]);
                break;
            default:
                print("{0}", node.OperatorToken.Text);
                Visit(node.Operand);
                break;
        }
    }

    public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
    {
        // This would be incorrect if used as a subexpression. 
        // However, such code should not be written anyway.
        Visit(node.Operand);
        print(" {0}= 1", node.OperatorToken.Text[0]);
    }

    public override void VisitCastExpression(CastExpressionSyntax node)
    {
        var type = node.Type as PredefinedTypeSyntax;
        if (type != null) {
            var type_name = type.Keyword.Text;
            switch (type_name) {
                case "string":
                    print("str");
                    break;
                default:
                    print("{0}", type_name);
                    break;
            }
        } else {
            Visit(node.Type);
        }
        print("(");
        Visit(node.Expression);
        print(")");
    }

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        // Type
        Visit(node.Type);

        // Argument list
        Visit(node.ArgumentList);

        // Initializers
        Visit(node.Initializer);
    }

    public override void VisitArrayCreationExpression(ArrayCreationExpressionSyntax node)
    {
        var value = "null";
        var type = node.Type.ElementType as PredefinedTypeSyntax;
        var rank = node.Type.RankSpecifiers;
        if (type != null) {
            switch(type.Keyword.Text) {
                case "int":
                    value = "0"; 
                    break;
                case "float":
                case "double":
                    value = "0.0";
                    break;
            }
        }
        int layers = 0;
        foreach (var dim in rank) {
            foreach (var size in dim.Sizes) {
                print("Array(");
                Visit(size);
                print(", ");
                layers++;
            }
        }
        print("{0}{1}", value, new string(')',layers));
        Visit(node.Initializer);
    }

    public override void VisitInitializerExpression(InitializerExpressionSyntax node)
    {
        newline();
        print("# Begin initializer");
        newline();
        foreach (var expr in node.Expressions) {
            Visit(expr);
            newline();
        }
        print("# End initializer");
        newline();
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
