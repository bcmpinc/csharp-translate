using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace GettingStartedCS
{
    class Program
    {
        static void Main(string[] args)
        {
            // demonstrate parsing
            SyntaxTree tree = CSharpSyntaxTree.ParseText(@"var x = new DateTime(2016,12,1);");
            Console.WriteLine(tree.ToString()); // new DateTime(2016,12,1)
        }
    }
}
