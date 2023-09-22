using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

class Program {

    static void usage() {
        Console.Error.WriteLine("Usage: csharp-translate.exe converter path_to_cs_file");
        Console.Error.WriteLine("where converter is any of: debug godot");
    }
    static void Main(string[] args) {
        if (args.Length != 2) {
            usage();
        } else {
            var file = args[1];
            var code = File.OpenText(file).ReadToEnd();
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code, path: file);
            switch (args[0]) {
                case "godot":
                    new GodotWalker(tree);
                    break;
                case "debug":
                    new DebugWalker(tree);
                    break;
                default:
                    usage();
                    break;
            }
        }
    }
}
