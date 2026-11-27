using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.FlowGraphs;

/// <summary>
/// Full API test of Flow Graphs: FlowGraph, FlowNode, FlowPort, FlowConnection, FlowFrame, FlowVariable, FlowBlackboard,
/// FlowGraphValidation and the built-in node types. Same tests as scripts/gd/Test.gd.
/// </summary>
public partial class Test : FeatureTest
{
    private const string Dir = "res://flow-graphs/resources/";
    private const int ChainLength = 1000;
    private static readonly string[] Storage = ["node_ids", "nodes", "connections", "frames", "next_node_id"];

    // Input and output port names of the built-in node types (FlowExpressionNode without input_names).
    private static readonly Dictionary<string, (string[] Inputs, string[] Outputs)> Ports = new()
    {
        ["FlowStartNode"] = ([], ["out"]),
        ["FlowEndNode"] = (["in"], []),
        ["FlowBranchNode"] = (["in", "condition"], ["true", "false"]),
        ["FlowExpressionNode"] = ([], ["result"]),
        ["FlowGetVariableNode"] = ([], ["value"]),
        ["FlowSetVariableNode"] = (["in", "value"], ["out"]),
        ["FlowEmitEventNode"] = (["in"], ["out"]),
        ["FlowSubgraphNode"] = (["in"], ["out"]),
        ["FlowWaitNode"] = (["in"], ["out"]),
    };

    protected override Task RunTests()
    {
        TestAddAndRemove();
        TestBuiltInPorts();
        TestBuiltInProperties();
        TestConnect();
        TestReadConnections();
        TestPort();
        TestNode();
        TestCustomNode();
        TestVariables();
        TestBlackboard();
        TestFrames();
        TestStorageHidden();
        TestValidation();
        TestSaveLoad();
        TestSampleResources();
        return Task.CompletedTask;
    }

    // Start(0) -> Wait(1) -> Branch(5) -true-> End(2); GetVariable "gold"(3) -> Expression "gold >= 100"(4) -> condition of 5.
    private static FlowGraph MakeGraph()
    {
        var graph = new FlowGraph();
        graph.Variables = [new FlowVariable { Name = "gold", Type = Variant.Type.Int }];
        graph.AddNode(new FlowStartNode());
        graph.AddNode(new FlowWaitNode(), new Vector2(200, 0));
        graph.AddNode(new FlowEndNode(), new Vector2(400, 0));
        graph.AddNode(new FlowGetVariableNode { VariableName = "gold" });
        graph.AddNode(new FlowExpressionNode { InputNames = ["gold"], Expression = "gold >= 100", ResultType = Variant.Type.Bool });
        graph.AddNode(new FlowBranchNode());
        graph.ConnectNodes(0, "out", 1, "in");
        graph.ConnectNodes(1, "out", 5, "in");
        graph.ConnectNodes(5, "true", 2, "in");
        graph.ConnectNodes(3, "value", 4, "gold");
        graph.ConnectNodes(4, "result", 5, "condition");
        return graph;
    }

    private static string[] PortNames(Godot.Collections.Array<FlowPort> ports)
    {
        return ports.Select(port => (string)port.Name).ToArray();
    }

    private static bool HasIssue(FlowGraphValidation validation, FlowGraphValidation.IssueKind kind, long nodeId)
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

    private void TestAddAndRemove()
    {
        var graph = new FlowGraph();
        var added = new List<long>();
        var removed = new List<long>();
        graph.NodeAdded += id => added.Add(id);
        graph.NodeRemoved += id => removed.Add(id);
        var end = new FlowEndNode();
        int endId = graph.AddNode(end, new Vector2(400, 0));
        int startId = graph.AddNode(new FlowStartNode());
        Expect(graph.GetNodeIds().SequenceEqual([0, 1]), "AddNode returns the ids 0 and 1");
        Expect(added.SequenceEqual([0L, 1L]), "NodeAdded is emitted with the id");
        ExpectEqual(graph.StartNodeId, startId, "the first FlowStartNode becomes the start");
        Expect(graph.HasNode(endId), "HasNode finds an added node");
        Expect(graph.GetNode(endId) == end, "GetNode returns the node");
        ExpectEqual(graph.FindNodeId(end), endId, "FindNodeId returns the id");
        ExpectEqual(end.EditorPosition, new Vector2(400, 0), "AddNode stores the position in EditorPosition");
        var other = new FlowGraph();
        ExpectEqual(other.AddNode(end), FlowGraph.NodeIdNone, "a node can't join a second graph");
        graph.RemoveNode(endId);
        Expect(removed.SequenceEqual([(long)endId]), "NodeRemoved is emitted with the id");
        Expect(!graph.HasNode(endId), "RemoveNode removes the node");
        ExpectEqual(graph.FindNodeId(end), FlowGraph.NodeIdNone, "FindNodeId of a removed node is NodeIdNone");
        Expect(other.AddNode(end) != FlowGraph.NodeIdNone, "a removed node can join another graph");
        ExpectEqual(graph.AddNode(new FlowEndNode()), 2, "ids are never reused");
    }

    private void TestBuiltInPorts()
    {
        foreach ((string type, (string[] inputs, string[] outputs)) in Ports)
        {
            var node = ClassDB.Instantiate(type).As<FlowNode>();
            Expect(PortNames(node.GetInputPorts()).SequenceEqual(inputs), $"{type}: input ports");
            Expect(PortNames(node.GetOutputPorts()).SequenceEqual(outputs), $"{type}: output ports");
        }
        FlowPort condition = new FlowBranchNode().FindInputPort("condition");
        ExpectEqual(condition.Type, Variant.Type.Bool, "FlowBranchNode: condition is a bool");
    }

    private void TestBuiltInProperties()
    {
        var expression = new FlowExpressionNode { InputNames = ["a", "b"], ResultType = Variant.Type.Int, Expression = "a + b" };
        Expect(PortNames(expression.GetInputPorts()).SequenceEqual(["a", "b"]), "FlowExpressionNode: one input per InputNames entry");
        ExpectEqual(expression.FindOutputPort("result").Type, Variant.Type.Int, "FlowExpressionNode: ResultType types the result");
        ExpectEqual(expression.Expression, "a + b", "FlowExpressionNode: Expression");
        var subgraph = new FlowSubgraphNode { Graph = new FlowGraph(), ExitNames = ["bought"] };
        Expect(PortNames(subgraph.GetOutputPorts()).SequenceEqual(["out", "bought"]), "FlowSubgraphNode: one output per ExitNames entry");
        Expect(subgraph.Graph != null, "FlowSubgraphNode: Graph");
        var end = new FlowEndNode { EndName = "done" };
        ExpectEqual((string)end.EndName, "done", "FlowEndNode: EndName");
        var eventNode = new FlowEmitEventNode { EventName = "say", Arguments = ["Hello"] };
        ExpectEqual((string)eventNode.Arguments[0], "Hello", "FlowEmitEventNode: Arguments");
        var wait = new FlowWaitNode { Seconds = 2.0 };
        ExpectEqual(wait.Seconds, 2.0, "FlowWaitNode: Seconds");
        var setGold = new FlowSetVariableNode { VariableName = "gold" };
        ExpectEqual((string)setGold.VariableName, "gold", "FlowSetVariableNode: VariableName");
    }

    private void TestConnect()
    {
        FlowGraph graph = MakeGraph();
        int changes = 0;
        graph.ConnectionsChanged += () => changes++;
        int lonely = graph.AddNode(new FlowWaitNode());
        Expect(graph.CanConnectNodes(lonely, "out", 2, "in"), "CanConnectNodes: a free flow output into a flow input");
        ExpectEqual(graph.ConnectNodes(lonely, "out", 2, "in"), Error.Ok, "ConnectNodes returns Ok");
        ExpectEqual(changes, 1, "ConnectionsChanged is emitted");
        Expect(graph.IsNodesConnected(lonely, "out", 2, "in"), "IsNodesConnected after ConnectNodes");
        graph.DisconnectNodes(lonely, "out", 2, "in");
        Expect(!graph.IsNodesConnected(lonely, "out", 2, "in"), "DisconnectNodes removes the connection");
        ExpectEqual(changes, 2, "ConnectionsChanged is emitted on disconnect");
        ExpectEqual(graph.ConnectNodes(lonely, "out", 99, "in"), Error.DoesNotExist, "unknown node: DoesNotExist");
        ExpectEqual(graph.ConnectNodes(lonely, "nope", 2, "in"), Error.DoesNotExist, "unknown port: DoesNotExist");
        ExpectEqual(graph.ConnectNodes(lonely, "out", lonely, "in"), Error.InvalidParameter, "same node: InvalidParameter");
        ExpectEqual(graph.ConnectNodes(3, "value", 2, "in"), Error.InvalidParameter, "data into flow: InvalidParameter");
        int textId = graph.AddNode(new FlowExpressionNode { ResultType = Variant.Type.String });
        int branch = graph.AddNode(new FlowBranchNode());
        ExpectEqual(graph.ConnectNodes(textId, "result", branch, "condition"), Error.InvalidParameter, "String into bool: InvalidParameter");
        ExpectEqual(graph.ConnectNodes(0, "out", 2, "in"), Error.AlreadyInUse, "flow output used twice: AlreadyInUse");
        ExpectEqual(graph.ConnectNodes(3, "value", 5, "condition"), Error.AlreadyInUse, "data input used twice: AlreadyInUse");
        Expect(!graph.CanConnectNodes(0, "out", 2, "in"), "CanConnectNodes answers false for a used flow output");
        int anyId = graph.AddNode(new FlowExpressionNode());
        ExpectEqual(graph.ConnectNodes(anyId, "result", branch, "condition"), Error.Ok, "a Variant output fits a bool input");
    }

    private void TestReadConnections()
    {
        FlowGraph graph = MakeGraph();
        ExpectEqual(graph.GetConnections().Count, 5, "GetConnections returns all connections");
        Godot.Collections.Array<FlowConnection> outputs = graph.GetOutputConnections(0);
        ExpectEqual(outputs.Count, 1, "GetOutputConnections of the start node");
        FlowConnection flow = outputs[0];
        ExpectEqual(flow.FromNodeId, 0, "FlowConnection.FromNodeId");
        ExpectEqual((string)flow.FromPort, "out", "FlowConnection.FromPort");
        ExpectEqual(flow.ToNodeId, 1, "FlowConnection.ToNodeId");
        ExpectEqual((string)flow.ToPort, "in", "FlowConnection.ToPort");
        ExpectEqual(flow.Kind, FlowPort.PortKind.Flow, "FlowConnection.Kind of a flow connection");
        Godot.Collections.Array<FlowConnection> inputs = graph.GetInputConnections(4);
        ExpectEqual(inputs.Count, 1, "GetInputConnections of the expression");
        ExpectEqual(inputs[0].Kind, FlowPort.PortKind.Data, "FlowConnection.Kind of a data connection");
        Expect(graph.FindFlowPath(0, 2).SequenceEqual([0, 1, 5, 2]), "FindFlowPath returns the ids along the path");
        Expect(graph.FindFlowPath(2, 0).Length == 0, "FindFlowPath without a path is empty");
        graph.RemoveNode(1);
        ExpectEqual(graph.GetConnections().Count, 3, "RemoveNode removes the connections of the node");
    }

    private void TestPort()
    {
        FlowPort flow = FlowPort.CreateFlow("yes", "Yes");
        ExpectEqual(flow.Kind, FlowPort.PortKind.Flow, "CreateFlow: Kind");
        ExpectEqual((string)flow.Name, "yes", "CreateFlow: Name");
        ExpectEqual(flow.Label, "Yes", "CreateFlow: Label");
        FlowPort data = FlowPort.CreateData("amount", Variant.Type.Int, 1, "Amount");
        ExpectEqual(data.Kind, FlowPort.PortKind.Data, "CreateData: Kind");
        ExpectEqual(data.Type, Variant.Type.Int, "CreateData: Type");
        ExpectEqual(data.DefaultValue.AsInt32(), 1, "CreateData: DefaultValue");
        ExpectEqual(data.Label, "Amount", "CreateData: Label");
        data.ClassHint = "Texture2D";
        ExpectEqual((string)data.ClassHint, "Texture2D", "ClassHint");
        ExpectEqual((int)FlowPort.PortKind.Max, 2, "PortKind.Max");
    }

    private void TestNode()
    {
        var wait = new FlowWaitNode();
        ExpectEqual(wait.GetTitle(), "Wait", "GetTitle of a built-in node: class name without Flow and Node");
        wait.TitleOverride = "Pause";
        ExpectEqual(wait.GetTitle(), "Pause", "TitleOverride replaces the title");
        ExpectEqual(wait.GetCategory(), "Flow", "GetCategory of a built-in node");
        Expect(wait.GetInlineProperties().SequenceEqual(["seconds"]), "GetInlineProperties of a built-in node");
        Expect(wait.GetConfigurationWarnings().Length == 0, "FlowWaitNode has no warnings");
        ExpectEqual(new FlowEmitEventNode().GetConfigurationWarnings().Length, 1, "FlowEmitEventNode without EventName warns");
        Expect(wait.GetTranslatableStrings().Count == 0, "FlowWaitNode has no translatable strings");
        wait.EditorComment = "Breathe";
        ExpectEqual(wait.EditorComment, "Breathe", "EditorComment");
        wait.EditorSize = new Vector2(120, 80);
        ExpectEqual(wait.EditorSize, new Vector2(120, 80), "EditorSize");
    }

    private void TestCustomNode()
    {
        var node = new HasItemNodeCs();
        Expect(PortNames(node.GetInputPorts()).SequenceEqual(["in"]), "_GetInputPorts");
        Expect(PortNames(node.GetOutputPorts()).SequenceEqual(["yes", "no"]), "_GetOutputPorts");
        ExpectEqual(node.FindOutputPort("no").Kind, FlowPort.PortKind.Flow, "FindOutputPort");
        Expect(node.FindInputPort("nope") == null, "FindInputPort of an unknown port is null");
        ExpectEqual(node.GetTitle(), "Has Item Node Cs", "GetTitle from the class name");
        ExpectEqual(node.GetCategory(), "Inventory", "_GetCategory");
        Expect(node.GetConfigurationWarnings().SequenceEqual(["No item is set."]), "_GetConfigurationWarnings");
        int changes = 0;
        node.Changed += () => changes++;
        node.Item = "key";
        ExpectEqual(changes, 1, "NotifyPortsChanged emits Changed");
        Expect(node.GetConfigurationWarnings().Length == 0, "no warning once the item is set");
        Expect(node.GetInlineProperties().SequenceEqual(["Item"]), "_GetInlineProperties");
        Godot.Collections.Array<string[]> strings = node.GetTranslatableStrings();
        Expect(strings.Count == 1 && strings[0].SequenceEqual(["Do you have the key?"]), "_GetTranslatableStrings");
        var graph = new FlowGraph();
        graph.AddNode(node);
        Godot.Collections.Array<string[]> collected = graph.GetTranslatableStrings();
        Expect(collected.Count == 1 && collected[0].SequenceEqual(["Do you have the key?"]), "FlowGraph.GetTranslatableStrings collects the nodes");
    }

    private void TestVariables()
    {
        var gold = new FlowVariable { Name = "gold", Type = Variant.Type.Int, DefaultValue = 50, Description = "Coins of the player." };
        ExpectEqual((string)gold.Name, "gold", "FlowVariable.Name");
        ExpectEqual(gold.Type, Variant.Type.Int, "FlowVariable.Type");
        ExpectEqual(gold.DefaultValue.AsInt32(), 50, "FlowVariable.DefaultValue");
        ExpectEqual(gold.Description, "Coins of the player.", "FlowVariable.Description");
        var graph = new FlowGraph { Variables = [gold] };
        Expect(graph.Variables[0] == gold, "FlowGraph.Variables");
    }

    private void TestBlackboard()
    {
        var board = new FlowBlackboard();
        var changed = new List<string>();
        board.VariableChanged += (name, value) => changed.Add($"{name}={value}");
        board.SetVariable("gold", 10);
        Expect(changed.SequenceEqual(["gold=10"]), "VariableChanged is emitted with name and value");
        ExpectEqual(board.GetVariable("gold").AsInt32(), 10, "GetVariable");
        ExpectEqual(board.GetVariable("missing", 7).AsInt32(), 7, "GetVariable returns the default of an unknown name");
        Expect(board.HasVariable("gold"), "HasVariable");
        Expect(board.GetVariableNames().Select(name => (string)name).SequenceEqual(["gold"]), "GetVariableNames");
        ExpectEqual(board.Variables["gold"].AsInt32(), 10, "Variables");
        board.RemoveVariable("gold");
        Expect(!board.HasVariable("gold"), "RemoveVariable");
    }

    private void TestFrames()
    {
        var frame = new FlowFrame { Title = "Shop", Color = Colors.Orange, Rect = new Rect2(0, 0, 300, 200), NodeIds = [1, 2] };
        var graph = new FlowGraph { Frames = [frame] };
        FlowFrame stored = graph.Frames[0];
        ExpectEqual(stored.Title, "Shop", "FlowFrame.Title");
        ExpectEqual(stored.Color, Colors.Orange, "FlowFrame.Color");
        ExpectEqual(stored.Rect, new Rect2(0, 0, 300, 200), "FlowFrame.Rect");
        Expect(stored.NodeIds.SequenceEqual([1, 2]), "FlowFrame.NodeIds");
    }

    private void TestStorageHidden()
    {
        foreach (Godot.Collections.Dictionary property in new FlowGraph().GetPropertyList())
        {
            string name = property["name"].AsString();
            long usage = property["usage"].AsInt64();
            if (Storage.Contains(name))
            {
                Expect((usage & (long)PropertyUsageFlags.Storage) != 0, $"{name} is saved");
                Expect((usage & (long)PropertyUsageFlags.Editor) == 0, $"{name} is hidden in the inspector");
            }
            else if (name == "variables" || name == "start_node_id")
            {
                Expect((usage & (long)PropertyUsageFlags.Editor) != 0, $"{name} is shown in the inspector");
            }
        }
        FlowGraph graph = MakeGraph();
        ExpectEqual(graph.Get("next_node_id").AsInt32(), 6, "next_node_id is the next free id");
    }

    private void TestValidation()
    {
        FlowGraph graph = MakeGraph();
        FlowGraphValidation valid = graph.Validate();
        Expect(valid.IsValid(), "IsValid of a correct graph");
        ExpectEqual(valid.GetIssueCount(), 0, "a valid graph has no issues");

        var noStart = new FlowGraph();
        noStart.AddNode(new FlowEndNode());
        FlowGraphValidation missing = noStart.Validate();
        Expect(HasIssue(missing, FlowGraphValidation.IssueKind.MissingStart, FlowGraph.NodeIdNone), "IssueKind.MissingStart");

        int lonely = graph.AddNode(new FlowWaitNode());
        int ghostId = graph.AddNode(new FlowGetVariableNode { VariableName = "ghost" });
        int silent = graph.AddNode(new FlowEmitEventNode());
        int firstId = graph.AddNode(new FlowExpressionNode { InputNames = ["x"] });
        int secondId = graph.AddNode(new FlowExpressionNode { InputNames = ["y"] });
        graph.ConnectNodes(firstId, "result", secondId, "y");
        graph.ConnectNodes(secondId, "result", firstId, "x");
        int textId = graph.AddNode(new FlowExpressionNode { Expression = "\"text\"", ResultType = Variant.Type.String });
        int branch = graph.AddNode(new FlowBranchNode());
        // ConnectNodes refuses these, so write the storage directly, as a hand-edited file would.
        Godot.Collections.Array storage = graph.Get("connections").AsGodotArray();
        storage.AddRange(new Godot.Collections.Array { textId, new StringName("result"), branch, new StringName("condition") });
        storage.AddRange(new Godot.Collections.Array { 0, new StringName("out"), 2, new StringName("in") });
        storage.AddRange(new Godot.Collections.Array { lonely, new StringName("gone"), 2, new StringName("in") });
        graph.Set("connections", storage);
        FlowGraphValidation validation = graph.Validate();
        Expect(HasIssue(validation, FlowGraphValidation.IssueKind.UnreachableNode, lonely), "IssueKind.UnreachableNode");
        Expect(HasIssue(validation, FlowGraphValidation.IssueKind.TypeMismatch, branch), "IssueKind.TypeMismatch");
        Expect(HasIssue(validation, FlowGraphValidation.IssueKind.UnknownPort, lonely), "IssueKind.UnknownPort");
        Expect(HasIssue(validation, FlowGraphValidation.IssueKind.MultipleFlowOutputs, 0), "IssueKind.MultipleFlowOutputs");
        Expect(HasIssue(validation, FlowGraphValidation.IssueKind.UndeclaredVariable, ghostId), "IssueKind.UndeclaredVariable");
        Expect(HasIssue(validation, FlowGraphValidation.IssueKind.DataCycle, firstId), "IssueKind.DataCycle");
        Expect(HasIssue(validation, FlowGraphValidation.IssueKind.NodeWarning, silent), "IssueKind.NodeWarning");
        ExpectEqual((int)FlowGraphValidation.IssueKind.Max, 8, "IssueKind.Max");
        ExpectEqual(validation.GetIssuesForNode(ghostId).Length, 2, "GetIssuesForNode: undeclared and unreachable");
        Expect(!string.IsNullOrEmpty(validation.GetIssueMessage(0)), "GetIssueMessage");
    }

    private void ExpectSame(FlowGraph graph, FlowGraph loaded, string label)
    {
        Expect(loaded != null, $"{label}: loaded");
        Expect(loaded.GetNodeIds().SequenceEqual(graph.GetNodeIds()), $"{label}: node ids");
        ExpectEqual(loaded.GetConnections().Count, graph.GetConnections().Count, $"{label}: connections");
        ExpectEqual(loaded.Get("next_node_id").AsInt32(), graph.Get("next_node_id").AsInt32(), $"{label}: next_node_id");
    }

    private void TestSaveLoad()
    {
        FlowGraph graph = MakeGraph();
        graph.AddNode(new HasItemNodeCs(), new Vector2(10, 20));
        ExpectEqual(ResourceSaver.Save(graph, "user://flow_graphs_test_cs.tres"), Error.Ok, "save .tres");
        var fromTres = ResourceLoader.Load("user://flow_graphs_test_cs.tres", "", ResourceLoader.CacheMode.Ignore) as FlowGraph;
        ExpectSame(graph, fromTres, ".tres");
        Expect(fromTres.GetNode(6) is HasItemNodeCs, ".tres: script node type kept");
        ExpectEqual(fromTres.GetNode(6).EditorPosition, new Vector2(10, 20), ".tres: EditorPosition kept");
        string json = ResourceTextSerializer.Serialize(graph, ResourceTextSerializer.Format.Json);
        var fromJson = ResourceTextSerializer.Deserialize(json, ResourceTextSerializer.Format.Json) as FlowGraph;
        ExpectSame(graph, fromJson, "JSON");
        var copy = (FlowGraph)graph.Duplicate(true);
        ExpectSame(graph, copy, "Duplicate(true)");
        Expect(copy.GetNode(0) != graph.GetNode(0), "Duplicate(true) copies the nodes");
        var shallow = (FlowGraph)graph.Duplicate();
        Expect(shallow.GetNode(0) == graph.GetNode(0), "a shallow duplicate shares the node objects");

        var chain = new FlowGraph();
        int previous = chain.AddNode(new FlowStartNode());
        for (int i = 0; i < ChainLength; i++)
        {
            int id = chain.AddNode(new FlowWaitNode());
            chain.ConnectNodes(previous, "out", id, "in");
            previous = id;
        }
        ExpectEqual(ResourceSaver.Save(chain, "user://flow_graphs_chain_cs.res"), Error.Ok, "save .res");
        ulong begin = Time.GetTicksUsec();
        var fromRes = ResourceLoader.Load("user://flow_graphs_chain_cs.res", "", ResourceLoader.CacheMode.Ignore) as FlowGraph;
        double elapsed = (Time.GetTicksUsec() - begin) / 1000.0;
        ExpectSame(chain, fromRes, $".res with {ChainLength} connections");
        GD.Print($"  {ChainLength} connections load from .res in {elapsed:F2} ms");
    }

    private void TestSampleResources()
    {
        var graph = GD.Load<FlowGraph>(Dir + "flow_graph.tres");
        Expect(graph.Validate().IsValid(), "flow_graph.tres is valid");
        Expect(graph.GetNode(2) is HasItemNodeCs, "flow_graph.tres: C# node type");
        ExpectEqual(graph.GetNode(1).GetCategory(), "Inventory", "flow_graph.tres: GDScript node type");
        ExpectEqual(graph.Frames.Count, 1, "flow_graph.tres: one frame");
        var board = GD.Load<FlowBlackboard>(Dir + "flow_blackboard.tres");
        ExpectEqual(board.GetVariable("score").AsInt32(), 10, "flow_blackboard.tres: score");
        var port = GD.Load<FlowPort>(Dir + "flow_port.tres");
        ExpectEqual(port.Type, Variant.Type.Int, "flow_port.tres: type");
    }
}
