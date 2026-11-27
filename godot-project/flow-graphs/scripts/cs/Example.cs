using Godot;

namespace TruelyInteractive.FlowGraphs;

/// <summary>Minimal example of Flow Graphs: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Build a graph in code
        var graph = new FlowGraph();
        int start = graph.AddNode(new FlowStartNode(), new Vector2(0, 0));
        var wait = new FlowWaitNode { Seconds = 2.0 };
        int waitId = graph.AddNode(wait, new Vector2(200, 0));
        int end = graph.AddNode(new FlowEndNode(), new Vector2(400, 0));
        graph.ConnectNodes(start, "out", waitId, "in");
        graph.ConnectNodes(waitId, "out", end, "in");
        GD.Print("Valid: ", graph.Validate().IsValid());

        // Ports and connections
        int a = graph.AddNode(new FlowWaitNode());
        int b = graph.AddNode(new FlowWaitNode());
        if (graph.CanConnectNodes(a, "out", b, "in"))
        {
            graph.ConnectNodes(a, "out", b, "in");
        }
        foreach (FlowConnection connection in graph.GetOutputConnections(a))
        {
            GD.Print("Connection: ", connection.ToNodeId, ".", connection.ToPort);
        }

        // Data connections and variables
        var gold = new FlowVariable { Name = "gold", Type = Variant.Type.Int, DefaultValue = 50 };
        graph.Variables = [gold];
        var getGold = new FlowGetVariableNode { VariableName = "gold" };
        int getId = graph.AddNode(getGold);
        var check = new FlowExpressionNode
        {
            InputNames = ["gold"],
            Expression = "gold >= 100",
            ResultType = Variant.Type.Bool,
        };
        int checkId = graph.AddNode(check);
        graph.ConnectNodes(getId, "value", checkId, "gold");
        int branchId = graph.AddNode(new FlowBranchNode());
        graph.ConnectNodes(checkId, "result", branchId, "condition");
        GD.Print("Data inputs of the branch: ", graph.GetInputConnections(branchId).Count);

        // Write your own node type (scripts/cs/HasItemNodeCs.cs)
        var hasItem = new HasItemNodeCs();
        GD.Print("HasItemNodeCs warnings: ", hasItem.GetConfigurationWarnings());

        // Validate a graph
        FlowGraphValidation validation = graph.Validate();
        for (int i = 0; i < validation.GetIssueCount(); i++)
        {
            var kind = validation.GetIssueKind(i);
            GD.Print(kind == FlowGraphValidation.IssueKind.UnreachableNode, " ", validation.GetIssueMessage(i));
        }

        // Save, load and copy (user:// instead of res://graphs/)
        ResourceSaver.Save(graph, "user://intro_cs.tres");
        var loaded = GD.Load<FlowGraph>("user://intro_cs.tres");
        string json = ResourceTextSerializer.Serialize(graph, ResourceTextSerializer.Format.Json);
        var fromJson = ResourceTextSerializer.Deserialize(json, ResourceTextSerializer.Format.Json) as FlowGraph;
        var copy = (FlowGraph)graph.Duplicate(true);
        GD.Print("Nodes loaded: ", loaded.GetNodeIds().Length);
        GD.Print("Nodes from JSON: ", fromJson.GetNodeIds().Length);
        GD.Print("Nodes of the copy: ", copy.GetNodeIds().Length);

        GetTree().Quit();
    }
}
