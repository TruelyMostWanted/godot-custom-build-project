using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue256FeatureFlowGraphPlayback;

/// <summary>C# checks of 256-feature-flow-graph-playback. Check the same things as scripts/gd/check.gd.</summary>
public partial class Issue256FeatureFlowGraphPlaybackCheck : FeatureCheckCs
{
    private const string ChoiceNodeScript = "res://256-feature-flow-graph-playback/scripts/gd/issue256_choice_node.gd";
    private const string GraphPath = "user://256_flow_graph_cs.tres";
    private const string StatePath = "user://256_flow_graph_state_cs.tres";

    protected override string CheckName => "256-feature-flow-graph-playback";

    protected override async Task RunChecks()
    {
        foreach (string cls in new[] { "FlowGraphPlayback", "FlowGraphPlayer", "FlowGraphState", "FlowExit" })
        {
            Expect(ClassDB.ClassExists(cls), $"ClassDB knows {cls}");
        }
        CheckChoices();
        CheckCsNode();
        CheckData();
        CheckSubgraphAndTravel();
        CheckState();
        await CheckPlayerLabel();
    }

    private static FlowNode NewChoiceNode()
    {
        return (FlowNode)GD.Load<GDScript>(ChoiceNodeScript).New();
    }

    // Start(0) -> Issue256ChoiceNode(1) -> a: EmitEvent "took_a"(2) -> End "a"(3) / b: End "b"(4).
    private static FlowGraph MakeChoiceGraph()
    {
        var graph = new FlowGraph();
        int start = graph.AddNode(new FlowStartNode());
        int choice = graph.AddNode(NewChoiceNode());
        int eventId = graph.AddNode(new FlowEmitEventNode { EventName = "took_a", Arguments = new Godot.Collections.Array { 1, "two" } });
        int endA = graph.AddNode(new FlowEndNode { EndName = "a" });
        int endB = graph.AddNode(new FlowEndNode { EndName = "b" });
        graph.ConnectNodes(start, "out", choice, "in");
        graph.ConnectNodes(choice, "a", eventId, "in");
        graph.ConnectNodes(eventId, "out", endA, "in");
        graph.ConnectNodes(choice, "b", endB, "in");
        return graph;
    }

    private void CheckChoices()
    {
        FlowGraph graph = MakeChoiceGraph();
        var playback = new FlowGraphPlayback();
        var events = new List<string>();
        var finished = new List<long>();
        playback.EventEmitted += (name, arguments) => events.Add($"{name}:{arguments.Count}");
        playback.Finished += endId => finished.Add(endId);
        playback.SetVariable("locked", true);
        ExpectEqual(playback.Start(graph), Error.Ok, "start");
        Expect(playback.IsWaiting(), "the script node waits");
        ExpectEqual(playback.GetCurrentNodeId(), 1, "current node is the choice node");
        var exits = playback.GetAvailableExits();
        ExpectEqual(exits.Count, 2, "two exits");
        if (exits.Count == 2)
        {
            ExpectEqual(exits[0].Label, "Take A", "exit label");
            ExpectEqual(exits[0].NodeId, 2, "exit a leads to node 2");
            Expect(!exits[1].Enabled, "exit b is disabled while locked");
        }
        ExpectEqual(playback.Choose(0), Error.Ok, "choose a");
        ExpectEqual(string.Join(",", events), "took_a:2", "event_emitted with arguments");
        ExpectEqual(string.Join(",", finished), "3", "finished at end a");
        Expect(playback.GetHistory().SequenceEqual(new[] { 0, 1, 2, 3 }), "history");

        playback.SetVariable("locked", false);
        playback.Start(graph);
        Expect(playback.GetAvailableExits()[1].Enabled, "exit b is enabled when unlocked");
        ExpectEqual(playback.AdvanceToExit("b"), Error.Ok, "advance_to_exit b");
        ExpectEqual(string.Join(",", finished), "3,4", "finished at end b");
    }

    // Start -> Issue256CsCounterNode -> Set "copy" (value = counter.tens) -> end of flow.
    private void CheckCsNode()
    {
        var graph = new FlowGraph
        {
            Variables = new Godot.Collections.Array<FlowVariable> { new FlowVariable { Name = "count", Type = Variant.Type.Int, DefaultValue = 0 } },
        };
        int start = graph.AddNode(new FlowStartNode());
        int counter = graph.AddNode(new Issue256CsCounterNode());
        int copy = graph.AddNode(new FlowSetVariableNode { VariableName = "copy" });
        graph.ConnectNodes(start, "out", counter, "in");
        graph.ConnectNodes(counter, "out", copy, "in");
        graph.ConnectNodes(counter, "tens", copy, "value");

        var playback = new FlowGraphPlayback();
        ExpectEqual(playback.Start(graph), Error.Ok, "C# node: start");
        ExpectEqual((int)playback.GetVariable("count"), 1, "C# node: _Enter ran once");
        ExpectEqual((int)playback.GetVariable("copy"), 10, "C# node: _EvaluateOutput fed the data input");
    }

    // Start -> Set "total" (value = expression "gold * 2") -> Branch (condition = expression "total > 10") -> End "rich" / End.
    private void CheckData()
    {
        var graph = new FlowGraph
        {
            Variables = new Godot.Collections.Array<FlowVariable> { new FlowVariable { Name = "gold", Type = Variant.Type.Int, DefaultValue = 6 } },
        };
        int start = graph.AddNode(new FlowStartNode());
        int setId = graph.AddNode(new FlowSetVariableNode { VariableName = "total" });
        int twice = graph.AddNode(new FlowExpressionNode { InputNames = new[] { "gold" }, Expression = "gold * 2" });
        int test = graph.AddNode(new FlowExpressionNode { InputNames = new[] { "total" }, Expression = "total > 10" });
        int branch = graph.AddNode(new FlowBranchNode());
        int rich = graph.AddNode(new FlowEndNode { EndName = "rich" });
        int poor = graph.AddNode(new FlowEndNode());
        graph.ConnectNodes(start, "out", setId, "in");
        graph.ConnectNodes(twice, "result", setId, "value");
        graph.ConnectNodes(setId, "out", branch, "in");
        graph.ConnectNodes(test, "result", branch, "condition");
        graph.ConnectNodes(branch, "true", rich, "in");
        graph.ConnectNodes(branch, "false", poor, "in");

        var playback = new FlowGraphPlayback();
        var changed = new List<string>();
        playback.VariableChanged += (name, value) => changed.Add(name);
        ExpectEqual(playback.Start(graph), Error.Ok, "data graph: start");
        ExpectEqual((int)playback.GetVariable("total"), 12, "expression read the variable gold");
        ExpectEqual(playback.GetHistory().Last(), rich, "branch read total lazily and took true");
        Expect(changed.Contains("total"), "variable_changed for total");
        ExpectEqual((int)playback.GetNodeInputValue(setId, "value"), 12, "get_node_input_value");
    }

    private void CheckSubgraphAndTravel()
    {
        FlowGraph inner = MakeChoiceGraph();
        var outer = new FlowGraph();
        int start = outer.AddNode(new FlowStartNode());
        int sub = outer.AddNode(new FlowSubgraphNode { Graph = inner, ExitNames = new[] { "b" } });
        int afterB = outer.AddNode(new FlowEndNode { EndName = "after_b" });
        int afterOther = outer.AddNode(new FlowEndNode());
        outer.ConnectNodes(start, "out", sub, "in");
        outer.ConnectNodes(sub, "b", afterB, "in");
        outer.ConnectNodes(sub, "out", afterOther, "in");

        var playback = new FlowGraphPlayback();
        playback.Start(outer);
        ExpectEqual(playback.GetGraphStackDepth(), 2, "inside the subgraph");
        Expect(playback.GetCurrentGraph() == inner, "current graph is the inner graph");
        playback.AdvanceToExit("b");
        ExpectEqual(playback.GetHistory().Last(), afterB, "the subgraph returned through its exit b");

        var events = new List<string>();
        playback.EventEmitted += (name, arguments) => events.Add(name);
        playback.Start(inner);
        Expect(playback.GetTravelPath(3).SequenceEqual(new[] { 1, 2, 3 }), "travel path");
        ExpectEqual(playback.Travel(3), Error.Ok, "travel (walk)");
        ExpectEqual(string.Join(",", events), "took_a", "walking runs the nodes on the way");
        Expect(!playback.IsPlaying(), "travel to an end node finishes");
    }

    private void CheckState()
    {
        ExpectEqual(ResourceSaver.Save(MakeChoiceGraph(), GraphPath), Error.Ok, "save the graph");
        var graph = GD.Load<FlowGraph>(GraphPath);
        var playback = new FlowGraphPlayback();
        playback.SetVariable("coins", 5);
        playback.Start(graph);
        ExpectEqual(ResourceSaver.Save(playback.GetState(), StatePath), Error.Ok, "save the state");
        var state = ResourceLoader.Load(StatePath, "", ResourceLoader.CacheMode.Ignore) as FlowGraphState;
        if (!Expect(state != null, "load the state"))
        {
            return;
        }
        Expect(state.GraphPath != "", "the state names the graph");
        Expect(state.NodeStack.SequenceEqual(new[] { 1 }), "node stack");
        Expect(state.Waiting, "the state was waiting");
        ExpectEqual((int)state.NodeMemory[0].AsGodotDictionary()[1], 1, "node memory of the choice node");

        var restored = new FlowGraphPlayback();
        var waiting = new List<long>();
        restored.NodeWaiting += (id, node) => waiting.Add(id);
        ExpectEqual(restored.SetState(state), Error.Ok, "set_state loads the graph");
        Expect(restored.GetGraph() == graph, "the same graph from the cache");
        ExpectEqual(string.Join(",", waiting), "1", "node_waiting is emitted again");
        ExpectEqual((int)restored.GetVariable("coins"), 5, "variables restored");
        ExpectEqual(restored.Choose(0), Error.Ok, "continues after the restore");
        Expect(!restored.IsPlaying(), "finished after the restore");
    }

    // A FlowGraphPlayer wired to a Label through its signals.
    private async Task CheckPlayerLabel()
    {
        var graph = new FlowGraph();
        int start = graph.AddNode(new FlowStartNode());
        int wait = graph.AddNode(new FlowWaitNode { Seconds = 0.1, TitleOverride = "Wait a moment" });
        int choice = graph.AddNode(NewChoiceNode());
        int end = graph.AddNode(new FlowEndNode());
        graph.ConnectNodes(start, "out", wait, "in");
        graph.ConnectNodes(wait, "out", choice, "in");
        graph.ConnectNodes(choice, "a", end, "in");

        var label = new Label();
        AddChild(label);
        var player = new FlowGraphPlayer { Graph = graph };
        AddChild(player);
        player.NodeEntered += (id, node) => label.Text = node.GetTitle();
        player.Finished += endId => label.Text = "Done";

        Expect(!player.IsProcessingInternal(), "an idle player does not process");
        ExpectEqual(player.Play(), Error.Ok, "play");
        ExpectEqual(label.Text, "Wait a moment", "the label shows the waiting node");
        Expect(player.IsProcessingInternal(), "waiting on a FlowWaitNode: processing on");
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        ExpectEqual(player.GetPlayback().GetCurrentNodeId(), choice, "the wait ended in the scene tree");
        Expect(!player.IsProcessingInternal(), "waiting for a choice: processing off");
        player.GetPlayback().Choose(0);
        ExpectEqual(label.Text, "Done", "the label shows the end");
        Expect(!player.IsPlaying(), "the player finished");
        player.QueueFree();
        label.QueueFree();
    }
}
