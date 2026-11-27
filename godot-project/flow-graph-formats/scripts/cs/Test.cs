using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.FlowGraphFormats;

/// <summary>Full API test of Flow Graph Formats (FlowGraphMermaid, FlowGraphMermaidOptions, text formats). Same tests as scripts/gd/Test.gd.</summary>
public partial class Test : FeatureTest
{
    private const string DoorPath = "res://flow-graph-formats/resources/door.mmd";
    private const string OptionsPath = "res://flow-graph-formats/resources/mermaid_options.tres";
    private const string AmountNodePath = "res://flow-graph-formats/scripts/gd/FormatsAmountNode.gd";
    private const string MmdPath = "user://flow_graph_formats_cs.mmd";
    private const string Title = "Say \"hi\" #1";

    private static readonly Dictionary<FlowGraphMermaidOptions.DirectionEnum, string> Headers = new()
    {
        [FlowGraphMermaidOptions.DirectionEnum.TopDown] = "flowchart TD\n",
        [FlowGraphMermaidOptions.DirectionEnum.LeftRight] = "flowchart LR\n",
        [FlowGraphMermaidOptions.DirectionEnum.BottomUp] = "flowchart BT\n",
        [FlowGraphMermaidOptions.DirectionEnum.RightLeft] = "flowchart RL\n",
    };

    private readonly GDScript _amountScript = GD.Load<GDScript>(AmountNodePath);

    protected override Task RunTests()
    {
        TestMermaidRoundTrip();
        TestOptionDefaults();
        TestDirections();
        TestOptions();
        TestHandWritten();
        TestErrors();
        TestMmdFile();
        TestTextFormats();
        TestExportSetting();
        return Task.CompletedTask;
    }

    // Start(0) -> FormatsAmountNode(1) -> End(2); GetVariable(3) feeds "amount" of node 1; frame "Intro" around 0 and 1.
    private FlowGraph MakeGraph()
    {
        var graph = new FlowGraph();
        int start = graph.AddNode(new FlowStartNode());
        var amountNode = _amountScript.New().As<FlowNode>();
        amountNode.Set("factor", 5);
        amountNode.TitleOverride = Title;
        int custom = graph.AddNode(amountNode, new Vector2(200, 0));
        int end = graph.AddNode(new FlowEndNode(), new Vector2(400, 0));
        var getAmount = new FlowGetVariableNode { VariableName = "amount" };
        int amount = graph.AddNode(getAmount, new Vector2(0, 200));
        var variable = new FlowVariable { Name = "amount", Type = Variant.Type.Int };
        graph.Variables = [variable];
        graph.ConnectNodes(start, "out", custom, "in");
        graph.ConnectNodes(custom, "out", end, "in");
        graph.ConnectNodes(amount, "value", custom, "amount");
        var frame = new FlowFrame { Title = "Intro", NodeIds = [start, custom] };
        graph.Frames = [frame];
        return graph;
    }

    private void ExpectSame(FlowGraph loaded, string label)
    {
        Expect(loaded.GetNodeIds().SequenceEqual([0, 1, 2, 3]), $"{label}: node ids kept");
        ExpectEqual(loaded.StartNodeId, 0, $"{label}: start node kept");
        ExpectEqual(loaded.GetConnections().Count, 3, $"{label}: three connections");
        Expect(loaded.IsNodesConnected(0, "out", 1, "in"), $"{label}: flow connection kept");
        Expect(loaded.IsNodesConnected(3, "value", 1, "amount"), $"{label}: data connection kept");
        FlowNode custom = loaded.GetNode(1);
        Expect(custom.GetScript().As<GDScript>() == _amountScript, $"{label}: script node type kept");
        ExpectEqual(custom.Get("factor").AsInt32(), 5, $"{label}: script property kept");
        ExpectEqual(custom.GetTitle(), Title, $"{label}: title with quotes and # kept");
        ExpectEqual(custom.EditorPosition, new Vector2(200, 0), $"{label}: editor position kept");
        ExpectEqual(loaded.Variables.Count, 1, $"{label}: variables kept");
        ExpectEqual(loaded.Frames.Count, 1, $"{label}: frame kept");
        ExpectEqual(loaded.Frames[0].Title, "Intro", $"{label}: frame title kept");
        Expect(loaded.Frames[0].NodeIds.SequenceEqual([0, 1]), $"{label}: frame nodes kept");
        Expect(loaded.Validate().IsValid(), $"{label}: loaded graph is valid");
    }

    private void TestMermaidRoundTrip()
    {
        string text = FlowGraphMermaid.Serialize(MakeGraph());
        Expect(text.StartsWith("flowchart TD\n"), "Serialize writes a top-down flowchart by default");
        Expect(text.Contains("n0 -->|out| n1"), "a flow connection is a Mermaid edge");
        Expect(text.Contains("n3 -.->|value:amount| n1"), "a data connection is a dotted edge");
        Expect(text.Contains("subgraph f0"), "a frame is a subgraph");
        FlowGraph loaded = FlowGraphMermaid.Deserialize(text);
        ExpectSame(loaded, "Mermaid");
        ExpectEqual(FlowGraphMermaid.Serialize(loaded), text, "a second round trip gives the same text");
    }

    private void TestOptionDefaults()
    {
        var options = new FlowGraphMermaidOptions();
        ExpectEqual(options.Direction, FlowGraphMermaidOptions.DirectionEnum.TopDown, "Direction defaults to top-down");
        Expect(options.IncludeDataComments, "IncludeDataComments defaults to true");
        Expect(options.IncludeEditorData, "IncludeEditorData defaults to true");
        Expect(options.DrawDataConnections, "DrawDataConnections defaults to true");
        ExpectEqual((int)FlowGraphMermaidOptions.DirectionEnum.Max, 4, "DirectionEnum.Max is the number of directions");
        var sample = GD.Load<FlowGraphMermaidOptions>(OptionsPath);
        ExpectEqual(sample.Direction, FlowGraphMermaidOptions.DirectionEnum.LeftRight, "the sample mermaid_options.tres loads");
    }

    private void TestDirections()
    {
        FlowGraph graph = MakeGraph();
        var options = new FlowGraphMermaidOptions();
        foreach ((FlowGraphMermaidOptions.DirectionEnum direction, string header) in Headers)
        {
            options.Direction = direction;
            string text = FlowGraphMermaid.Serialize(graph, options);
            Expect(text.StartsWith(header), $"direction {(int)direction} writes {header.Trim()}");
        }
    }

    private void TestOptions()
    {
        FlowGraph graph = MakeGraph();
        var options = new FlowGraphMermaidOptions { DrawDataConnections = false, IncludeEditorData = false };
        string text = FlowGraphMermaid.Serialize(graph, options);
        Expect(!text.Contains("-.->"), "DrawDataConnections off: no dotted edge");
        Expect(text.Contains("%% flow:edge n3 value n1 amount"), "DrawDataConnections off: data edge as a comment");
        FlowGraph loaded = FlowGraphMermaid.Deserialize(text);
        Expect(loaded.IsNodesConnected(3, "value", 1, "amount"), "the data edge comment loads as a connection");
        ExpectEqual(loaded.GetNode(1).EditorPosition, Vector2.Zero, "IncludeEditorData off: no positions");
        options.IncludeDataComments = false;
        text = FlowGraphMermaid.Serialize(graph, options);
        Expect(!text.Contains("%%"), "IncludeDataComments off: plain Mermaid without comments");
        Expect(FlowGraphMermaid.Deserialize(text) != null, "plain Mermaid loads");
    }

    private void TestHandWritten()
    {
        var graph = GD.Load<FlowGraph>(DoorPath);
        Expect(graph.GetNodeIds().SequenceEqual([0, 1, 2, 3]), "hand-written .mmd loads four nodes");
        Expect(graph.GetNode(0) is FlowStartNode, "no incoming edge: start node");
        Expect(graph.GetNode(1) is FlowBranchNode, "rhombus: branch node");
        Expect(graph.GetNode(2) is FlowEmitEventNode, "other node: emit event node");
        Expect(graph.GetNode(3) is FlowEndNode, "no outgoing edge: end node");
        ExpectEqual(graph.GetNode(1).GetTitle(), "Door open?", "the label is the title");
        var walk = (FlowEmitEventNode)graph.GetNode(2);
        ExpectEqual(walk.EventName, (StringName)"Walk in", "the label is the event name");
        Expect(graph.IsNodesConnected(1, "true", 2, "in"), "first branch edge (yes) leaves true");
        Expect(graph.IsNodesConnected(1, "false", 3, "in"), "second branch edge (no) leaves false");
        ExpectEqual(graph.Frames.Count, 1, "a subgraph is a frame");
        ExpectEqual(graph.StartNodeId, 0, "the first start node is the start of the graph");
    }

    private void TestErrors()
    {
        var first = new FlowGraphMermaid();
        var second = new FlowGraphMermaid();
        ExpectEqual(first.Parse("sequenceDiagram\n  A->>B: hi\n"), Error.ParseError, "Parse fails on another diagram type");
        ExpectEqual(second.Parse("flowchart TD\n  A --> B\n  B -.-> C\n"), Error.ParseError, "Parse fails on a data edge without ports");
        ExpectEqual(first.GetErrorLine(), 1, "GetErrorLine of the first instance");
        ExpectEqual(second.GetErrorLine(), 3, "GetErrorLine of the second instance");
        Expect(second.GetErrorMessage().Length > 0, "GetErrorMessage is set");
        Expect(first.GetGraph() == null, "GetGraph is null after an error");
        ExpectEqual(first.Parse("flowchart TD\n  A --> B\n"), Error.Ok, "the next parse succeeds");
        Expect(first.GetGraph() != null, "GetGraph returns the parsed graph");
        ExpectEqual(first.GetErrorLine(), 0, "a successful parse clears the error line");
        Expect(first.GetErrorMessage().Length == 0, "a successful parse clears the error message");
        ExpectEqual(second.GetErrorLine(), 3, "the other instance keeps its error");
    }

    private void TestMmdFile()
    {
        ExpectEqual(ResourceSaver.Save(MakeGraph(), MmdPath), Error.Ok, "ResourceSaver saves .mmd");
        string text = FileAccess.GetFileAsString(MmdPath);
        Expect(text.StartsWith("flowchart"), "the .mmd file is Mermaid text");
        var loaded = ResourceLoader.Load(MmdPath, "", ResourceLoader.CacheMode.Ignore) as FlowGraph;
        ExpectSame(loaded, ".mmd");
    }

    private void TestTextFormats()
    {
        FlowGraph graph = MakeGraph();
        ResourceTextSerializer.Format[] formats = [ResourceTextSerializer.Format.Json, ResourceTextSerializer.Format.Yaml, ResourceTextSerializer.Format.Toml];
        foreach (ResourceTextSerializer.Format format in formats)
        {
            string text = ResourceTextSerializer.Serialize(graph, format);
            var loaded = ResourceTextSerializer.Deserialize(text, format) as FlowGraph;
            ExpectSame(loaded, $"format {(int)format}");
        }
    }

    private void TestExportSetting()
    {
        bool convert = ProjectSettings.GetSetting("flow_graph/export/convert_text_to_binary").AsBool();
        Expect(convert, "flow_graph/export/convert_text_to_binary defaults to true");
    }
}
