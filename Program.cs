using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

class Program {
    static void Main(string[] args) {
        var stream = args.Length >= 1 ? File.OpenText(args[0]) : Console.In;
        var code = stream.ReadToEnd();
        SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
        new GodotWalker().Visit(tree.GetRoot());
    }
}
