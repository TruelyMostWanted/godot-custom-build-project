using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.ChildrenOfAType;

/// <summary>Full API test of Children of a Type (Node.GetChildren&lt;T&gt;, Node.FindChildren&lt;T&gt;). C# only; scripts/gd/Test.gd tests find_children().</summary>
public partial class Test : FeatureTest
{
    // Root
    //   A (Button, owned)
    //   B (PlainNode, owned)
    //     C (Button, owned)
    //     D (Node2D, owned)
    //   E (Node, not owned, like the root of an instanced scene)
    //     F (Button, not owned)
    //   I (Button, internal child)
    private const string DirectButtons = "A";
    private const string AllButtons = "A,C,F";
    private const string OwnedButtons = "A,C";
    private const string AllNode2D = "B,D";
    private const string AllCanvasItems = "A,B,C,D,F";

    private Node _root;

    protected override Task RunTests()
    {
        _root = BuildTree();
        TestGetChildren();
        TestFindChildren();
        TestOwned();
        TestRecursive();
        TestPlainClass();
        TestInterface();
        TestInheritance();
        TestNoMatch();
        TestInternal();
        TestSameAsFindChildren();
        _root.Free();
        return Task.CompletedTask;
    }

    private static Node BuildTree()
    {
        var root = new Node { Name = "Root" };
        var a = new Button { Name = "A" };
        var b = new PlainNode { Name = "B" };
        var c = new Button { Name = "C" };
        var d = new Node2D { Name = "D" };
        var e = new Node { Name = "E" };
        var f = new Button { Name = "F" };
        var internalButton = new Button { Name = "I" };
        root.AddChild(a);
        root.AddChild(b);
        b.AddChild(c);
        b.AddChild(d);
        root.AddChild(e);
        e.AddChild(f);
        root.AddChild(internalButton, false, Node.InternalMode.Front);
        a.Owner = root;
        b.Owner = root;
        c.Owner = root;
        d.Owner = root;
        return root;
    }

    private static string Names<T>(List<T> nodes) where T : class
    {
        IEnumerable<string> names = nodes.Select(n => ((Node)(object)n).Name.ToString());
        return string.Join(",", names);
    }

    private void TestGetChildren()
    {
        ExpectEqual(Names(_root.GetChildren<Button>()), DirectButtons, "GetChildren<Button>: direct children only");
    }

    private void TestFindChildren()
    {
        ExpectEqual(Names(_root.FindChildren<Button>()), AllButtons, "FindChildren<Button>: all descendants in tree order, instanced ones included");
    }

    private void TestOwned()
    {
        ExpectEqual(Names(_root.FindChildren<Button>(owned: true)), OwnedButtons, "owned: true skips nodes without owner");
    }

    private void TestRecursive()
    {
        ExpectEqual(Names(_root.FindChildren<Button>(recursive: false)), DirectButtons, "recursive: false checks only direct children");
    }

    private void TestPlainClass()
    {
        ExpectEqual(Names(_root.FindChildren<PlainNode>()), "B", "a C# class without [GlobalClass]");
    }

    private void TestInterface()
    {
        ExpectEqual(Names(_root.FindChildren<IDamageable>()), "B", "an interface");
    }

    private void TestInheritance()
    {
        ExpectEqual(Names(_root.FindChildren<Node2D>()), AllNode2D, "inheritance: PlainNode is a Node2D");
        ExpectEqual(Names(_root.FindChildren<CanvasItem>()), AllCanvasItems, "every CanvasItem");
    }

    private void TestNoMatch()
    {
        List<Camera3D> cameras = _root.FindChildren<Camera3D>();
        ExpectEqual(cameras.Count, 0, "no match is an empty list");
    }

    private void TestInternal()
    {
        ExpectEqual(Names(_root.GetChildren<Button>(includeInternal: true)), "I,A", "GetChildren<Button>(includeInternal: true) adds internal children");
        ExpectEqual(Names(_root.FindChildren<Button>(includeInternal: true)), "I,A,C,F", "FindChildren<Button>(includeInternal: true) adds internal children");
    }

    private void TestSameAsFindChildren()
    {
        IEnumerable<string> names = _root.FindChildren("*", "Button", true, false).Select(n => n.Name.ToString());
        string typed = Names(_root.FindChildren<Button>(includeInternal: true));
        ExpectEqual(string.Join(",", names), typed, "find_children(\"*\", \"Button\", true, false) gives the same nodes as FindChildren<Button>(includeInternal: true)");
    }
}
