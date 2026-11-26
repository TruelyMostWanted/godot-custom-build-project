using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;
using TruelyInteractive.Issue255FeatureFlowGraph;

namespace TruelyInteractive.Issue258FeatureFlowGraphFormats;

/// <summary>C# checks of 258-feature-flow-graph-formats. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue258FeatureFlowGraphFormatsCheck : FeatureCheckCs
{
    private const string Dir = "res://258-feature-flow-graph-formats/";

    protected override string CheckName => "258-feature-flow-graph-formats";

    protected override Task RunChecks()
    {
        foreach (string cls in new[] { "FlowGraphMermaid", "FlowGraphMermaidOptions" })
        {
            Expect(ClassDB.ClassExists(cls), $"ClassDB knows {cls}");
        }
        CheckMermaidRoundTrip();
        CheckOptions();
        CheckHandWritten();
        CheckErrors();
        CheckMmdFile();
        CheckTextFormats();
        ExpectEqual((bool)ProjectSettings.GetSetting("flow_graph/export/convert_text_to_binary"), true, "export setting defaults to true");
        return Task.CompletedTask;
    }

    // Start(0) -> C# node(1) -> End(2); GetVariable(3) feeds "text" of node 1; frame around 0 and 1.
    private static FlowGraph MakeGraph()
    {
        var graph = new FlowGraph();
        int start = graph.AddNode(new FlowStartNode());
        int custom = graph.AddNode(new Issue255CsNode { Prefix = "!!" }, new Vector2(200, 0));
        int end = graph.AddNode(new FlowEndNode(), new Vector2(400, 0));
        int text = graph.AddNode(new FlowGetVariableNode { VariableName = "text" }, new Vector2(0, 200));
        graph.Variables = new Godot.Collections.Array<FlowVariable> { new FlowVariable { Name = "text", Type = Variant.Type.String } };
        graph.ConnectNodes(start, "out", custom, "in");
        graph.ConnectNodes(custom, "out", end, "in");
        graph.ConnectNodes(text, "value", custom, "text");
        graph.Frames = new Godot.Collections.Array<FlowFrame> { new FlowFrame { Title = "Intro", NodeIds = new[] { start, custom } } };
        return graph;
    }

    private void ExpectSame(FlowGraph graph, FlowGraph loaded, string label)
    {
        Expect(loaded != null, $"{label}: loaded");
        if (loaded == null)
        {
            return;
        }
        Expect(loaded.GetNodeIds().SequenceEqual(graph.GetNodeIds()), $"{label}: node ids");
        ExpectEqual(loaded.StartNodeId, graph.StartNodeId, $"{label}: start node");
        ExpectEqual(loaded.GetConnections().Count, graph.GetConnections().Count, $"{label}: connection count");
        Expect(loaded.IsNodesConnected(0, "out", 1, "in"), $"{label}: flow connection");
        Expect(loaded.IsNodesConnected(3, "value", 1, "text"), $"{label}: data connection");
        var custom = loaded.GetNode(1) as Issue255CsNode;
        Expect(custom != null, $"{label}: C# node type kept");
        if (custom != null)
        {
            ExpectEqual(custom.Prefix, "!!", $"{label}: property kept");
            ExpectEqual(custom.GetTitle(), "C# Node", $"{label}: title with # kept");
            ExpectEqual(custom.EditorPosition, new Vector2(200, 0), $"{label}: editor position kept");
        }
        ExpectEqual(loaded.Variables.Count, 1, $"{label}: variables kept");
        ExpectEqual(loaded.Frames.Count, 1, $"{label}: frame kept");
        if (loaded.Frames.Count == 1)
        {
            ExpectEqual(loaded.Frames[0].Title, "Intro", $"{label}: frame title");
            Expect(loaded.Frames[0].NodeIds.SequenceEqual(new[] { 0, 1 }), $"{label}: frame nodes");
        }
        Expect(loaded.Validate().IsValid(), $"{label}: loaded graph is valid");
    }

    private void CheckMermaidRoundTrip()
    {
        FlowGraph graph = MakeGraph();
        string text = FlowGraphMermaid.Serialize(graph);
        Expect(text.StartsWith("flowchart TD\n"), "Mermaid header");
        Expect(text.Contains("n0 -->|out| n1"), "flow edge as Mermaid edge");
        Expect(text.Contains("n3 -.->|value:text| n1"), "data edge dotted");
        Expect(text.Contains("subgraph f0"), "frame as subgraph");
        FlowGraph loaded = FlowGraphMermaid.Deserialize(text);
        ExpectSame(graph, loaded, "Mermaid");
        if (loaded != null)
        {
            ExpectEqual(FlowGraphMermaid.Serialize(loaded), text, "Mermaid: second round trip gives the same text");
        }
    }

    private void CheckOptions()
    {
        FlowGraph graph = MakeGraph();
        var options = new FlowGraphMermaidOptions
        {
            Direction = FlowGraphMermaidOptions.DirectionEnum.LeftRight,
            DrawDataConnections = false,
            IncludeEditorData = false,
        };
        string text = FlowGraphMermaid.Serialize(graph, options);
        Expect(text.StartsWith("flowchart LR\n"), "options: direction");
        Expect(!text.Contains("-.->") && text.Contains("%% flow:edge n3 value n1 text"), "options: data edge as comment");
        FlowGraph loaded = FlowGraphMermaid.Deserialize(text);
        Expect(loaded != null && loaded.IsNodesConnected(3, "value", 1, "text"), "options: data edge kept");
        Expect(loaded != null && loaded.GetNode(1).EditorPosition == Vector2.Zero, "options: editor data left out");
        options.IncludeDataComments = false;
        text = FlowGraphMermaid.Serialize(graph, options);
        Expect(!text.Contains("%%"), "options: plain Mermaid without comments");
        Expect(FlowGraphMermaid.Deserialize(text) != null, "options: plain Mermaid loads");
    }

    private void CheckHandWritten()
    {
        var graph = GD.Load<FlowGraph>(Dir + "resources/door.mmd");
        Expect(graph != null, "hand-written .mmd loads as FlowGraph");
        if (graph == null)
        {
            return;
        }
        Expect(graph.GetNodeIds().SequenceEqual(new[] { 0, 1, 2, 3 }), "hand-written: four nodes");
        Expect(graph.GetNode(0) is FlowStartNode, "hand-written: start");
        Expect(graph.GetNode(1) is FlowBranchNode, "hand-written: rhombus is a branch");
        Expect(graph.GetNode(2) is FlowEmitEventNode, "hand-written: step is an event node");
        Expect(graph.GetNode(3) is FlowEndNode, "hand-written: end");
        ExpectEqual(graph.GetNode(1).GetTitle(), "Door open?", "hand-written: label is the title");
        Expect(graph.IsNodesConnected(1, "true", 2, "in"), "hand-written: yes -> true");
        Expect(graph.IsNodesConnected(1, "false", 3, "in"), "hand-written: no -> false");
        ExpectEqual(graph.Frames.Count, 1, "hand-written: subgraph is a frame");
        ExpectEqual(graph.StartNodeId, 0, "hand-written: start node id");
    }

    private void CheckErrors()
    {
        var first = new FlowGraphMermaid();
        var second = new FlowGraphMermaid();
        ExpectEqual(first.Parse("sequenceDiagram\n  A->>B: hi\n"), Error.ParseError, "errors: not a flowchart");
        ExpectEqual(second.Parse("flowchart TD\n  A --> B\n  B -.-> C\n"), Error.ParseError, "errors: data edge without ports");
        ExpectEqual(first.GetErrorLine(), 1, "errors: line of the first instance");
        ExpectEqual(second.GetErrorLine(), 3, "errors: line of the second instance");
        Expect(!string.IsNullOrEmpty(second.GetErrorMessage()), "errors: message");
        Expect(first.GetGraph() == null, "errors: no graph");
        ExpectEqual(first.Parse("flowchart TD\n  A --> B\n"), Error.Ok, "errors: next parse succeeds");
        ExpectEqual(first.GetErrorLine(), 0, "errors: cleared");
        ExpectEqual(second.GetErrorLine(), 3, "errors: other instance unchanged");
    }

    private void CheckMmdFile()
    {
        FlowGraph graph = MakeGraph();
        const string path = "user://258_flow_graph_cs.mmd";
        ExpectEqual(ResourceSaver.Save(graph, path), Error.Ok, ".mmd: save");
        Expect(FileAccess.GetFileAsString(path).StartsWith("flowchart"), ".mmd: file is Mermaid text");
        ExpectSame(graph, ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore) as FlowGraph, ".mmd");
    }

    private void CheckTextFormats()
    {
        FlowGraph graph = MakeGraph();
        var formats = new Dictionary<string, ResourceTextSerializer.Format>
        {
            ["JSON"] = ResourceTextSerializer.Format.Json,
            ["YAML"] = ResourceTextSerializer.Format.Yaml,
            ["TOML"] = ResourceTextSerializer.Format.Toml,
        };
        foreach ((string label, ResourceTextSerializer.Format format) in formats)
        {
            string text = ResourceTextSerializer.Serialize(graph, format);
            Expect(!string.IsNullOrEmpty(text), $"{label}: serialize");
            ExpectSame(graph, ResourceTextSerializer.Deserialize(text, format) as FlowGraph, label);
        }
    }
}
