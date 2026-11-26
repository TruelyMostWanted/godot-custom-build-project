using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue255FeatureFlowGraph;

/// <summary>C# checks of 255-feature-flow-graph. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue255FeatureFlowGraphCheck : FeatureCheckCs
{
    private const string Dir = "res://255-feature-flow-graph/";
    private static readonly string[] Storage = { "node_ids", "nodes", "connections", "frames", "next_node_id" };

    protected override string CheckName => "255-feature-flow-graph";

    protected override Task RunChecks()
    {
        foreach (string cls in new[] { "FlowGraph", "FlowNode", "FlowPort", "FlowConnection", "FlowFrame", "FlowVariable", "FlowBlackboard", "FlowGraphValidation",
                     "FlowStartNode", "FlowEndNode", "FlowBranchNode", "FlowExpressionNode", "FlowGetVariableNode", "FlowSetVariableNode",
                     "FlowEmitEventNode", "FlowSubgraphNode", "FlowWaitNode" })
        {
            Expect(ClassDB.ClassExists(cls), $"ClassDB knows {cls}");
        }
        CheckCsNode();
        CheckGraphApi();
        CheckTwoGraphs();
        CheckStorageHidden();
        CheckValidation();
        CheckTresRoundTrip();
        CheckJsonRoundTrip();
        CheckThousandConnections();
        CheckExample();
        return Task.CompletedTask;
    }

    // Start(0) -> C# node(1) -> End(2); GetVariable(3) feeds the data input "text" of node 1.
    private static FlowGraph MakeGraph()
    {
        var graph = new FlowGraph();
        int start = graph.AddNode(new FlowStartNode());
        int custom = graph.AddNode(new Issue255CsNode(), new Vector2(200, 0));
        int end = graph.AddNode(new FlowEndNode(), new Vector2(400, 0));
        int text = graph.AddNode(new FlowGetVariableNode { VariableName = "text" }, new Vector2(0, 200));
        graph.Variables = new Godot.Collections.Array<FlowVariable> { new FlowVariable { Name = "text", Type = Variant.Type.String } };
        graph.ConnectNodes(start, "out", custom, "in");
        graph.ConnectNodes(custom, "out", end, "in");
        graph.ConnectNodes(text, "value", custom, "text");
        return graph;
    }

    private void CheckCsNode()
    {
        var node = new Issue255CsNode();
        ExpectEqual(node.GetInputPorts().Count, 2, "C# node: two input ports");
        FlowPort text = node.FindInputPort("text");
        Expect(text != null, "C# node: FindInputPort(text)");
        if (text != null)
        {
            ExpectEqual(text.Kind, FlowPort.PortKind.Data, "C# node: text is a data port");
            ExpectEqual(text.Type, Variant.Type.String, "C# node: text is a String");
            ExpectEqual(text.Label, "Text", "C# node: label");
        }
        ExpectEqual(node.FindOutputPort("out").Kind, FlowPort.PortKind.Flow, "C# node: out is a flow port");
        ExpectEqual(node.GetTitle(), "C# Node", "C# node: title override");
        ExpectEqual(node.GetCategory(), "Checks", "C# node: category override");
    }

    private void CheckGraphApi()
    {
        FlowGraph graph = MakeGraph();
        Expect(graph.GetNodeIds().SequenceEqual(new[] { 0, 1, 2, 3 }), "ids 0..3");
        ExpectEqual(graph.StartNodeId, 0, "the first start node is the start");
        ExpectEqual(graph.FindNodeId(graph.GetNode(1)), 1, "FindNodeId");
        Expect(graph.IsNodesConnected(0, "out", 1, "in"), "IsNodesConnected");
        ExpectEqual(graph.GetConnections().Count, 3, "three connections");
        ExpectEqual(graph.GetInputConnections(1).Count, 2, "two inputs of the C# node");
        ExpectEqual(graph.GetInputConnections(1)[1].Kind, FlowPort.PortKind.Data, "connection kind data");
        ExpectEqual(graph.ConnectNodes(0, "out", 2, "in"), Error.AlreadyInUse, "flow output can't be connected twice");
        ExpectEqual(graph.ConnectNodes(3, "nope", 1, "in"), Error.DoesNotExist, "unknown port");
        Expect(!graph.CanConnectNodes(1, "length", 2, "in"), "data can't go into flow");
        Expect(graph.FindFlowPath(0, 2).SequenceEqual(new[] { 0, 1, 2 }), "FindFlowPath");
        Expect(graph.Validate().IsValid(), "the graph is valid");
        graph.RemoveNode(1);
        ExpectEqual(graph.GetConnections().Count, 0, "RemoveNode removes its connections");
        ExpectEqual(graph.AddNode(new FlowWaitNode()), 4, "ids are never reused");
    }

    private void CheckTwoGraphs()
    {
        var first = new FlowGraph();
        var second = new FlowGraph();
        var node = new FlowEndNode();
        int id = first.AddNode(node);
        Expect(id != FlowGraph.NodeIdNone, "add to the first graph");
        ExpectEqual(second.AddNode(node), FlowGraph.NodeIdNone, "a node can't join a second graph");
        Expect(second.GetNodeIds().Length == 0, "the second graph stays empty");
        first.RemoveNode(id);
        Expect(second.AddNode(node) != FlowGraph.NodeIdNone, "after RemoveNode the node is free again");
    }

    private void CheckStorageHidden()
    {
        int found = 0;
        foreach (Godot.Collections.Dictionary property in new FlowGraph().GetPropertyList())
        {
            string name = property["name"].AsString();
            long usage = property["usage"].AsInt64();
            if (Storage.Contains(name))
            {
                found++;
                Expect((usage & (long)PropertyUsageFlags.Storage) != 0, $"{name} is stored");
                Expect((usage & (long)PropertyUsageFlags.Editor) == 0, $"{name} is hidden in the inspector");
            }
            else if (name == "variables" || name == "start_node_id")
            {
                Expect((usage & (long)PropertyUsageFlags.Editor) != 0, $"{name} is shown in the inspector");
            }
        }
        ExpectEqual(found, Storage.Length, "all storage properties found");
    }

    private static bool HasIssue(FlowGraphValidation validation, FlowGraphValidation.IssueKind kind, int nodeId)
    {
        for (int i = 0; i < validation.GetIssueCount(); i++)
        {
            if (validation.GetIssueKind(i) == kind && validation.GetIssueNodeId(i) == nodeId)
            {
                return true;
            }
        }
        return false;
    }

    private void CheckValidation()
    {
        var graph = new FlowGraph();
        graph.AddNode(new FlowEndNode());
        FlowGraphValidation validation = graph.Validate();
        Expect(!validation.IsValid(), "a graph without start is invalid");
        Expect(HasIssue(validation, FlowGraphValidation.IssueKind.MissingStart, FlowGraph.NodeIdNone), "missing start reported");

        graph = MakeGraph();
        int lonely = graph.AddNode(new FlowWaitNode());
        int expressionId = graph.AddNode(new FlowExpressionNode { Expression = "42", ResultType = Variant.Type.Int });
        // ConnectNodes refuses int -> String, so write the storage directly, as a hand-edited file would.
        Godot.Collections.Array storage = graph.Get("connections").AsGodotArray();
        storage.Add(expressionId);
        storage.Add(new StringName("result"));
        storage.Add(1);
        storage.Add(new StringName("text"));
        graph.Set("connections", storage);
        validation = graph.Validate();
        Expect(HasIssue(validation, FlowGraphValidation.IssueKind.UnreachableNode, lonely), "unreachable node reported");
        Expect(HasIssue(validation, FlowGraphValidation.IssueKind.TypeMismatch, 1), "type mismatch int -> String reported");
        ExpectEqual(validation.GetIssuesForNode(lonely).Length, 1, "GetIssuesForNode");
        Expect(!string.IsNullOrEmpty(validation.GetIssueMessage(0)), "issue message");
    }

    private void CheckTresRoundTrip()
    {
        FlowGraph graph = MakeGraph();
        const string path = "user://check_255_graph_cs.tres";
        ExpectEqual(ResourceSaver.Save(graph, path), Error.Ok, "save .tres");
        var loaded = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore) as FlowGraph;
        Expect(loaded != null, "load .tres");
        if (loaded != null)
        {
            ExpectSame(graph, loaded, ".tres");
        }
    }

    private void CheckJsonRoundTrip()
    {
        FlowGraph graph = MakeGraph();
        string text = ResourceTextSerializer.Serialize(graph, ResourceTextSerializer.Format.Json);
        Expect(!string.IsNullOrEmpty(text), "serialize JSON");
        var loaded = ResourceTextSerializer.Deserialize(text, ResourceTextSerializer.Format.Json) as FlowGraph;
        Expect(loaded != null, "deserialize JSON");
        if (loaded != null)
        {
            ExpectSame(graph, loaded, "JSON");
        }
    }

    private void ExpectSame(FlowGraph graph, FlowGraph loaded, string label)
    {
        Expect(loaded.GetNodeIds().SequenceEqual(graph.GetNodeIds()), $"{label}: node ids");
        ExpectEqual(loaded.GetConnections().Count, graph.GetConnections().Count, $"{label}: connection count");
        Expect(loaded.IsNodesConnected(3, "value", 1, "text"), $"{label}: data connection");
        FlowNode custom = loaded.GetNode(1);
        Expect(custom is Issue255CsNode, $"{label}: C# node type kept");
        FlowPort text = custom.FindInputPort("text");
        Expect(text != null && text.Type == Variant.Type.String, $"{label}: C# ports kept");
        ExpectEqual(custom.EditorPosition, new Vector2(200, 0), $"{label}: editor position kept");
        Expect(loaded.Validate().IsValid(), $"{label}: loaded graph is valid");
    }

    private void CheckThousandConnections()
    {
        var graph = new FlowGraph();
        int previous = graph.AddNode(new FlowStartNode());
        for (int i = 0; i < 1000; i++)
        {
            int id = graph.AddNode(new FlowWaitNode(), new Vector2(i * 10, 0));
            graph.ConnectNodes(previous, "out", id, "in");
            previous = id;
        }
        foreach (string ext in new[] { "tres", "res" })
        {
            string path = $"user://check_255_thousand_cs.{ext}";
            ExpectEqual(ResourceSaver.Save(graph, path), Error.Ok, $"save 1000 connections (.{ext})");
            var watch = Stopwatch.StartNew();
            var loaded = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore) as FlowGraph;
            watch.Stop();
            Expect(loaded != null && loaded.GetConnections().Count == 1000, $"load 1000 connections (.{ext})");
            GD.Print($"255 (C#): graph with 1001 nodes and 1000 connections loads from .{ext} in {watch.Elapsed.TotalMilliseconds:F2} ms");
        }
    }

    private void CheckExample()
    {
        var graph = GD.Load<FlowGraph>(Dir + "resources/example_flow_graph.tres");
        Expect(graph != null, "example graph loads");
        if (graph == null)
        {
            return;
        }
        Expect(graph.Validate().IsValid(), "example graph is valid");
        Expect(graph.GetNode(1).GetScript().As<Script>()?.ResourcePath.EndsWith(".gd") == true, "example: GDScript node type");
        Expect(graph.GetNode(2) is Issue255CsNode, "example: C# node type");
        Expect(graph.GetNode(2).FindInputPort("text") != null, "example: C# node ports");
        ExpectEqual(graph.GetFrames().Count, 1, "example: one frame");
        var board = GD.Load<FlowBlackboard>(Dir + "resources/example_flow_blackboard.tres");
        Expect(board != null && board.GetVariable("score").AsInt32() == 10, "example blackboard");
        var port = GD.Load<FlowPort>(Dir + "resources/example_flow_port.tres");
        Expect(port != null && port.Type == Variant.Type.Int, "example port");
    }
}
