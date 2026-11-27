using Godot;

namespace TruelyInteractive.FlowGraphFormats;

/// <summary>Minimal example of Flow Graph Formats: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Mermaid files (door.mmd is the hand-written flowchart of the guide)
        var graph = GD.Load<FlowGraph>("res://flow-graph-formats/resources/door.mmd");
        ResourceSaver.Save(graph, "user://door_copy_cs.mmd");
        string text = FlowGraphMermaid.Serialize(graph);
        FlowGraph copy = FlowGraphMermaid.Deserialize(text);
        GD.Print(text);
        GD.Print("Copy has nodes: ", string.Join(", ", copy.GetNodeIds()));

        // Options
        var options = new FlowGraphMermaidOptions
        {
            Direction = FlowGraphMermaidOptions.DirectionEnum.LeftRight,
            DrawDataConnections = false,
        };
        string leftRight = FlowGraphMermaid.Serialize(graph, options);
        string header = leftRight.Split('\n')[0];
        GD.Print(header);

        // Errors
        var mermaid = new FlowGraphMermaid();
        if (mermaid.Parse("flowchart TD\n  A -.-> B\n") != Error.Ok)
        {
            GD.Print($"Line {mermaid.GetErrorLine()}: {mermaid.GetErrorMessage()}");
        }

        // JSON, YAML and TOML
        string yaml = ResourceTextSerializer.Serialize(graph, ResourceTextSerializer.Format.Yaml);
        var yamlCopy = ResourceTextSerializer.Deserialize(yaml, ResourceTextSerializer.Format.Yaml) as FlowGraph;
        GD.Print("YAML copy has nodes: ", string.Join(", ", yamlCopy.GetNodeIds()));

        GetTree().Quit();
    }
}
