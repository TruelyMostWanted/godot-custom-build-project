using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue239FeatureCsharpChildrenOfType;

/// <summary>C# checks of 239-feature-csharp-children-of-type. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue239FeatureCsharpChildrenOfTypeCheck : FeatureCheckCs
{
    protected override string CheckName => "239-feature-csharp-children-of-type";

    // Root
    //   A (Button, owned)
    //   B (Issue239PlainNode, owned)
    //     C (Button, owned)
    //     D (Node2D, owned)
    //   E (Node, not owned, like the root of an instanced scene)
    //     F (Button, not owned)
    private static Node BuildTree()
    {
        var root = new Node { Name = "Root" };
        Node Add(Node parent, Node child, string name, bool owned)
        {
            child.Name = name;
            parent.AddChild(child);
            if (owned)
            {
                child.Owner = root;
            }
            return child;
        }
        Add(root, new Button(), "A", true);
        Node b = Add(root, new Issue239PlainNode(), "B", true);
        Add(b, new Button(), "C", true);
        Add(b, new Node2D(), "D", true);
        Node e = Add(root, new Node(), "E", false);
        Add(e, new Button(), "F", false);
        return root;
    }

    private static string Names<T>(List<T> nodes) where T : class => string.Join(",", nodes.Select(n => ((Node)(object)n).Name.ToString()));

    protected override Task RunChecks()
    {
        Node root = BuildTree();

        ExpectEqual(Names(root.GetChildren<Button>()), "A", "GetChildren<Button>(): direct children only");
        ExpectEqual(Names(root.FindChildren<Button>()), "A,C,F", "FindChildren<Button>(): all descendants in tree order, instanced ones included");
        ExpectEqual(Names(root.FindChildren<Button>(owned: true)), "A,C", "owned: true skips nodes without owner (and their children)");
        ExpectEqual(Names(root.FindChildren<Button>(recursive: false)), "A", "recursive: false checks only direct children");
        ExpectEqual(Names(root.FindChildren<Issue239PlainNode>()), "B", "a C# class without [GlobalClass]");
        ExpectEqual(Names(root.FindChildren<IIssue239Marker>()), "B", "an interface");
        ExpectEqual(Names(root.FindChildren<Node2D>()), "B,D", "inheritance: Issue239PlainNode is a Node2D");
        ExpectEqual(Names(root.FindChildren<CanvasItem>()), "A,B,C,D,F", "every CanvasItem");
        Expect(root.FindChildren<Camera3D>().Count == 0, "no match is an empty list");

        // Same result as find_children() for a native class.
        string native = string.Join(",", root.FindChildren("*", "Button", true, false).Select(n => n.Name.ToString()));
        ExpectEqual(native, "A,C,F", "find_children(\"*\", \"Button\", true, false) gives the same nodes");

        root.Free();
        return Task.CompletedTask;
    }
}
