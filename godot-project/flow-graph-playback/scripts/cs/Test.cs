using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.FlowGraphPlayback;

using FlowGraphPlayback = Godot.FlowGraphPlayback;

/// <summary>
/// Full API test of Flow Graph Playback: FlowGraphPlayback, FlowGraphPlayer, FlowGraphState, FlowExit and the runtime
/// virtuals of FlowNode. Same tests as scripts/gd/Test.gd.
/// </summary>
public partial class Test : FeatureTest
{
    private const string GraphPath = "user://flow_graph_playback_test_cs.tres";
    private const string StatePath = "user://flow_graph_playback_state_cs.tres";

    protected override async Task RunTests()
    {
        TestStartAndStop();
        TestSignals();
        TestChoices();
        TestWaiting();
        TestTravel();
        TestVariables();
        TestDataInputs();
        TestSubgraph();
        TestCustomNodes();
        TestState();
        TestGuards();
        await TestPlayer();
    }

    // Start(0) -> LockedChoiceNodeCs(1) -a-> Emit "took_a" [1, "two"](2) -> End "a"(3) / -b-> End "b"(4).
    private static FlowGraph MakeChoiceGraph()
    {
        var graph = new FlowGraph();
        graph.AddNode(new FlowStartNode());
        graph.AddNode(new LockedChoiceNodeCs());
        graph.AddNode(new FlowEmitEventNode { EventName = "took_a", Arguments = [1, "two"] });
        graph.AddNode(new FlowEndNode { EndName = "a" });
        graph.AddNode(new FlowEndNode { EndName = "b" });
        graph.ConnectNodes(0, "out", 1, "in");
        graph.ConnectNodes(1, "a", 2, "in");
        graph.ConnectNodes(2, "out", 3, "in");
        graph.ConnectNodes(1, "b", 4, "in");
        return graph;
    }

    // Start(0) -> Wait(1) -> End(2).
    private static FlowGraph MakeWaitGraph(double seconds)
    {
        var graph = new FlowGraph();
        graph.AddNode(new FlowStartNode());
        graph.AddNode(new FlowWaitNode { Seconds = seconds });
        graph.AddNode(new FlowEndNode());
        graph.ConnectNodes(0, "out", 1, "in");
        graph.ConnectNodes(1, "out", 2, "in");
        return graph;
    }

    private void TestStartAndStop()
    {
        var playback = new FlowGraphPlayback();
        ExpectEqual(playback.Start(new FlowGraph()), Error.InvalidData, "Start without a start node returns InvalidData");
        FlowGraph graph = MakeChoiceGraph();
        var calls = new List<string>();
        playback.Started += () => calls.Add("started");
        playback.Stopped += () => calls.Add("stopped");
        ExpectEqual(playback.Start(graph), Error.Ok, "Start returns Ok");
        Expect(playback.IsPlaying(), "IsPlaying after Start");
        Expect(playback.GetGraph() == graph, "GetGraph");
        ExpectEqual(playback.GetCurrentNodeId(), 1, "GetCurrentNodeId: the choice node");
        Expect(playback.GetCurrentNode() is LockedChoiceNodeCs, "GetCurrentNode");
        playback.Stop();
        Expect(!playback.IsPlaying(), "Stop ends the playback");
        Expect(calls.SequenceEqual(["started", "stopped"]), "Started and Stopped are emitted");
        playback.Start(graph, 4);
        Expect(playback.GetHistory().SequenceEqual([4]), "Start from a node id");
        ExpectEqual(FlowGraphPlayback.StartNodeDefault, -1, "StartNodeDefault");
    }

    private void TestSignals()
    {
        var playback = new FlowGraphPlayback();
        var entered = new List<long>();
        var exited = new List<long>();
        var waiting = new List<long>();
        var events = new List<string>();
        var finished = new List<long>();
        playback.NodeEntered += (id, node) => entered.Add(id);
        playback.NodeExited += (id, node) => exited.Add(id);
        playback.NodeWaiting += (id, node) => waiting.Add(id);
        playback.EventEmitted += (name, arguments) => events.Add($"{name}:{Json.Stringify(arguments)}");
        playback.Finished += endId => finished.Add(endId);
        playback.Start(MakeChoiceGraph());
        Expect(entered.SequenceEqual([0L, 1L]), "NodeEntered for the start and the choice node");
        Expect(exited.SequenceEqual([0L]), "NodeExited for the start node");
        Expect(waiting.SequenceEqual([1L]), "NodeWaiting for the choice node");
        playback.EmitEvent("manual", [7]);
        Expect(events.SequenceEqual(["manual:[7]"]), "EmitEvent emits EventEmitted");
        playback.Choose(0);
        ExpectEqual(events[1], "took_a:[1,\"two\"]", "a FlowEmitEventNode emits EventEmitted with its arguments");
        Expect(finished.SequenceEqual([3L]), "Finished with the id of the end node");
    }

    private void TestChoices()
    {
        FlowGraph graph = MakeChoiceGraph();
        var playback = new FlowGraphPlayback();
        playback.SetVariable("locked", true);
        playback.Start(graph);
        Expect(playback.IsWaiting(), "IsWaiting at the choice node");
        Godot.Collections.Array<FlowExit> exits = playback.GetAvailableExits();
        ExpectEqual(exits.Count, 2, "GetAvailableExits returns two exits");
        ExpectEqual(exits[0].Label, "Take A", "FlowExit.Label");
        ExpectEqual((string)exits[0].Port, "a", "FlowExit.Port");
        ExpectEqual(exits[0].NodeId, 2, "FlowExit.NodeId is the target node");
        Expect(exits[0].Enabled, "FlowExit.Enabled");
        Expect(!exits[1].Enabled, "_GetAvailableExits disables b while locked");
        ExpectEqual(playback.Choose(0), Error.Ok, "Choose returns Ok");
        Expect(playback.GetHistory().SequenceEqual([0, 1, 2, 3]), "GetHistory after Choose(0)");
        ExpectEqual(playback.GetVariable("left").AsBool(), true, "_Exit runs when the node is left");
        playback.SetVariable("locked", false);
        playback.Start(graph);
        Expect(playback.GetAvailableExits()[1].Enabled, "b is enabled when unlocked");
        ExpectEqual(playback.AdvanceToExit("b"), Error.Ok, "AdvanceToExit returns Ok");
        ExpectEqual(playback.GetHistory()[^1], 4, "AdvanceToExit leaves through the named exit");
        playback.Start(graph);
        ExpectEqual(playback.Advance(), Error.Ok, "Advance returns Ok");
        ExpectEqual(playback.GetHistory()[^1], 3, "Advance takes the first flow output");
        ExpectEqual(playback.Advance(), Error.Unavailable, "Advance without a waiting node returns Unavailable");
    }

    private void TestWaiting()
    {
        var playback = new FlowGraphPlayback();
        playback.Start(MakeWaitGraph(0.5));
        Expect(playback.IsWaiting(), "a FlowWaitNode waits");
        playback.Process(0.3);
        Expect(playback.IsWaiting(), "Process(0.3): still waiting for 0.5 s");
        playback.Process(0.3);
        Expect(!playback.IsPlaying(), "Process: the wait ends after 0.5 s");
    }

    private void TestTravel()
    {
        FlowGraph graph = MakeChoiceGraph();
        var playback = new FlowGraphPlayback();
        var events = new List<string>();
        playback.EventEmitted += (name, arguments) => events.Add(name);
        playback.Start(graph);
        Expect(playback.GetTravelPath(3).SequenceEqual([1, 2, 3]), "GetTravelPath");
        ExpectEqual(playback.Travel(3), Error.Ok, "Travel walks");
        Expect(events.SequenceEqual(["took_a"]), "walking runs the nodes on the way");
        Expect(!playback.IsPlaying(), "Travel to an end node finishes");
        playback.Start(graph);
        playback.Travel(3, FlowGraphPlayback.TravelMode.Teleport);
        ExpectEqual(events.Count, 1, "TravelMode.Teleport skips the nodes on the way");
        playback.Start(graph);
        ExpectEqual(playback.Travel(0), Error.CantResolve, "walking without a path returns CantResolve");
        ExpectEqual((int)FlowGraphPlayback.TravelMode.Max, 2, "TravelMode.Max");
    }

    private void TestVariables()
    {
        FlowGraph graph = MakeWaitGraph(1.0);
        graph.Variables = [new FlowVariable { Name = "gold", DefaultValue = 6 }, new FlowVariable { Name = "level", DefaultValue = 1 }];
        var board = new FlowBlackboard();
        board.SetVariable("gold", 9);
        var playback = new FlowGraphPlayback { Blackboard = board };
        Expect(playback.Blackboard == board, "Blackboard");
        playback.Start(graph);
        ExpectEqual(playback.GetVariable("gold").AsInt32(), 9, "Start keeps an existing value");
        ExpectEqual(playback.GetVariable("level").AsInt32(), 1, "Start adds a declared variable with its default value");
        var changed = new List<string>();
        playback.VariableChanged += (name, value) => changed.Add($"{name}={value}");
        playback.SetVariable("gold", 120);
        Expect(changed.SequenceEqual(["gold=120"]), "VariableChanged is emitted");
        ExpectEqual(board.GetVariable("gold").AsInt32(), 120, "SetVariable writes to the blackboard");
        Expect(playback.HasVariable("gold"), "HasVariable");
        Expect(!playback.HasVariable("missing"), "HasVariable of an unknown name");
    }

    // Start -> Set "total" (value = expression "gold * 2") -> Branch (condition = expression "total > 10") -> End "rich" / End.
    // Both expression inputs are unconnected and read the variables of the same name.
    private void TestDataInputs()
    {
        var graph = new FlowGraph();
        graph.Variables = [new FlowVariable { Name = "gold", DefaultValue = 6 }];
        int start = graph.AddNode(new FlowStartNode());
        int setId = graph.AddNode(new FlowSetVariableNode { VariableName = "total" });
        int twiceId = graph.AddNode(new FlowExpressionNode { InputNames = ["gold"], Expression = "gold * 2" });
        int testId = graph.AddNode(new FlowExpressionNode { InputNames = ["total"], Expression = "total > 10" });
        int branch = graph.AddNode(new FlowBranchNode());
        int richId = graph.AddNode(new FlowEndNode());
        int poorId = graph.AddNode(new FlowEndNode());
        graph.ConnectNodes(start, "out", setId, "in");
        graph.ConnectNodes(twiceId, "result", setId, "value");
        graph.ConnectNodes(setId, "out", branch, "in");
        graph.ConnectNodes(testId, "result", branch, "condition");
        graph.ConnectNodes(branch, "true", richId, "in");
        graph.ConnectNodes(branch, "false", poorId, "in");
        var playback = new FlowGraphPlayback();
        playback.Start(graph);
        ExpectEqual(playback.GetVariable("total").AsInt32(), 12, "an unconnected expression input reads the variable");
        ExpectEqual(playback.GetHistory()[^1], richId, "the branch reads its condition when entered");
        ExpectEqual(playback.GetNodeInputValue(setId, "value").AsInt32(), 12, "GetNodeInputValue");
    }

    // Outer: Start -> Subgraph (inner choice graph, ExitNames ["b"]) -b-> End "after_b" / -out-> End.
    private void TestSubgraph()
    {
        FlowGraph inner = MakeChoiceGraph();
        var outer = new FlowGraph();
        int start = outer.AddNode(new FlowStartNode());
        int subId = outer.AddNode(new FlowSubgraphNode { Graph = inner, ExitNames = ["b"] });
        int afterBId = outer.AddNode(new FlowEndNode());
        int afterOtherId = outer.AddNode(new FlowEndNode());
        outer.ConnectNodes(start, "out", subId, "in");
        outer.ConnectNodes(subId, "b", afterBId, "in");
        outer.ConnectNodes(subId, "out", afterOtherId, "in");
        var playback = new FlowGraphPlayback();
        playback.Start(outer);
        ExpectEqual(playback.GetGraphStackDepth(), 2, "GetGraphStackDepth inside the subgraph");
        Expect(playback.GetCurrentGraph() == inner, "GetCurrentGraph is the inner graph");
        Expect(playback.GetGraph() == outer, "GetGraph stays the outer graph");
        playback.AdvanceToExit("b");
        ExpectEqual(playback.GetHistory()[^1], afterBId, "an inner end named in ExitNames leaves through that output");
        playback.Start(outer);
        playback.Choose(0);
        ExpectEqual(playback.GetHistory()[^1], afterOtherId, "another inner end leaves through out");
    }

    private void TestCustomNodes()
    {
        var graph = new FlowGraph();
        int start = graph.AddNode(new FlowStartNode());
        int twoId = graph.AddNode(new FlowExpressionNode { Expression = "2" });
        int threeId = graph.AddNode(new FlowExpressionNode { Expression = "3" });
        int addId = graph.AddNode(new AddNodeCs());
        graph.ConnectNodes(twoId, "result", addId, "a");
        graph.ConnectNodes(threeId, "result", addId, "b");
        int setId = graph.AddNode(new FlowSetVariableNode { VariableName = "sum" });
        graph.ConnectNodes(addId, "sum", setId, "value");
        int hasKeyId = graph.AddNode(new ItemCheckNodeCs { Item = "key" });
        int waitId = graph.AddNode(new WaitSecondsNodeCs());
        int noKeyId = graph.AddNode(new FlowEndNode());
        graph.ConnectNodes(start, "out", setId, "in");
        graph.ConnectNodes(setId, "out", hasKeyId, "in");
        graph.ConnectNodes(hasKeyId, "yes", waitId, "in");
        graph.ConnectNodes(hasKeyId, "no", noKeyId, "in");
        var playback = new FlowGraphPlayback();
        playback.SetVariable("items", new Godot.Collections.Array { new StringName("key") });
        playback.Start(graph);
        ExpectEqual(playback.GetVariable("sum").AsInt32(), 5, "_EvaluateOutput with GetInputValue: 2 + 3");
        ExpectEqual(playback.GetCurrentNodeId(), waitId, "_ChooseExit took yes");
        playback.Process(2.0);
        Expect(playback.IsWaiting(), "_ProcessWaiting: still waiting after 2 s");
        playback.Process(1.0);
        Expect(!playback.IsPlaying(), "_ProcessWaiting returned true after 3 s");
        playback.SetVariable("items", new Godot.Collections.Array());
        playback.Start(graph);
        ExpectEqual(playback.GetHistory()[^1], noKeyId, "_ChooseExit took no");
        ExpectEqual((int)FlowGraphPlayback.EnterResult.Max, 3, "EnterResult.Max");
    }

    private void TestState()
    {
        ExpectEqual(ResourceSaver.Save(MakeChoiceGraph(), GraphPath), Error.Ok, "save the graph");
        var graph = GD.Load<FlowGraph>(GraphPath);
        var playback = new FlowGraphPlayback();
        playback.SetVariable("coins", 5);
        playback.Start(graph);
        ExpectEqual(ResourceSaver.Save(playback.GetState(), StatePath), Error.Ok, "save the state");
        var state = ResourceLoader.Load(StatePath, "", ResourceLoader.CacheMode.Ignore) as FlowGraphState;
        ExpectEqual(state.GraphPath, graph.ResourcePath, "FlowGraphState.GraphPath");
        Expect(state.NodeStack.SequenceEqual([1]), "FlowGraphState.NodeStack");
        Expect(state.History.SequenceEqual([0, 1]), "FlowGraphState.History");
        Expect(state.Waiting, "FlowGraphState.Waiting");
        Godot.Collections.Dictionary memory = state.NodeMemory[0].AsGodotDictionary();
        ExpectEqual(memory[1].AsInt32(), 1, "FlowGraphState.NodeMemory of the choice node");
        ExpectEqual(state.Variables["coins"].AsInt32(), 5, "FlowGraphState.Variables");
        ExpectEqual(state.LayoutVersion, FlowGraphState.StateLayoutVersion, "FlowGraphState.LayoutVersion");

        var restored = new FlowGraphPlayback();
        var waiting = new List<long>();
        restored.NodeWaiting += (id, node) => waiting.Add(id);
        ExpectEqual(restored.SetState(state), Error.Ok, "SetState loads the graph");
        Expect(restored.GetGraph() == graph, "SetState uses the cached graph");
        Expect(waiting.SequenceEqual([1L]), "SetState waits again and emits NodeWaiting");
        ExpectEqual(restored.GetVariable("coins").AsInt32(), 5, "SetState restores the variables");
        ExpectEqual(restored.Choose(0), Error.Ok, "the playback continues after SetState");

        var newer = (FlowGraphState)state.Duplicate();
        newer.LayoutVersion = (int)FlowGraphState.StateLayoutVersion + 1;
        ExpectEqual(new FlowGraphPlayback().SetState(newer), Error.FileUnrecognized, "a newer LayoutVersion: FileUnrecognized");
        var unknownNode = (FlowGraphState)state.Duplicate();
        unknownNode.NodeStack = [99];
        ExpectEqual(new FlowGraphPlayback().SetState(unknownNode), Error.InvalidData, "an unknown node: InvalidData");
        var missingGraph = (FlowGraphState)state.Duplicate();
        missingGraph.GraphPath = "user://missing_graph.tres";
        ExpectEqual(new FlowGraphPlayback().SetState(missingGraph), Error.CantOpen, "a missing graph: CantOpen");
    }

    private void TestGuards()
    {
        var playback = new FlowGraphPlayback();
        ExpectEqual(playback.MaxStepsPerAdvance, 1024, "MaxStepsPerAdvance default");
        ExpectEqual(playback.MaxDataDepth, 256, "MaxDataDepth default");
        ExpectEqual(playback.MaxSubgraphDepth, 64, "MaxSubgraphDepth default");
        int stops = 0;
        playback.Stopped += () => stops++;

        // Start -> Emit "ping"(1) -> Emit "pong"(2) -> back to 1.
        var flowLoop = new FlowGraph();
        flowLoop.AddNode(new FlowStartNode());
        flowLoop.AddNode(new FlowEmitEventNode { EventName = "ping" });
        flowLoop.AddNode(new FlowEmitEventNode { EventName = "pong" });
        flowLoop.ConnectNodes(0, "out", 1, "in");
        flowLoop.ConnectNodes(1, "out", 2, "in");
        flowLoop.ConnectNodes(2, "out", 1, "in");
        playback.MaxStepsPerAdvance = 10;
        ExpectEqual(playback.Start(flowLoop), Error.CyclicLink, "MaxStepsPerAdvance stops a flow loop");
        ExpectEqual(stops, 1, "a guard emits Stopped");

        // Start -> Set "x" whose value comes from two expressions that feed each other.
        var dataLoop = new FlowGraph();
        dataLoop.AddNode(new FlowStartNode());
        dataLoop.AddNode(new FlowSetVariableNode { VariableName = "x" });
        dataLoop.AddNode(new FlowExpressionNode { InputNames = ["a"], Expression = "a" });
        dataLoop.AddNode(new FlowExpressionNode { InputNames = ["b"], Expression = "b" });
        dataLoop.ConnectNodes(0, "out", 1, "in");
        dataLoop.ConnectNodes(2, "result", 1, "value");
        dataLoop.ConnectNodes(3, "result", 2, "a");
        dataLoop.ConnectNodes(2, "result", 3, "b");
        playback.MaxDataDepth = 8;
        ExpectEqual(playback.Start(dataLoop), Error.CyclicLink, "MaxDataDepth stops a data loop");

        // Start -> Subgraph that contains its own graph.
        var selfGraph = new FlowGraph();
        selfGraph.AddNode(new FlowStartNode());
        var inner = new FlowSubgraphNode { Graph = selfGraph };
        selfGraph.AddNode(inner);
        selfGraph.ConnectNodes(0, "out", 1, "in");
        playback.MaxSubgraphDepth = 4;
        ExpectEqual(playback.Start(selfGraph), Error.CyclicLink, "MaxSubgraphDepth stops a subgraph that contains itself");
        inner.Graph = null;
    }

    // A FlowGraphPlayer wired to a Label through its signals: Start -> Wait 0.1 s -> LockedChoiceNodeCs -a-> End.
    private async Task TestPlayer()
    {
        var graph = new FlowGraph();
        int start = graph.AddNode(new FlowStartNode());
        int waitId = graph.AddNode(new FlowWaitNode { Seconds = 0.1, TitleOverride = "Wait a moment" });
        int choiceId = graph.AddNode(new LockedChoiceNodeCs());
        int endId = graph.AddNode(new FlowEndNode());
        graph.ConnectNodes(start, "out", waitId, "in");
        graph.ConnectNodes(waitId, "out", choiceId, "in");
        graph.ConnectNodes(choiceId, "a", endId, "in");

        var player = new FlowGraphPlayer();
        Expect(!player.Autostart, "Autostart is off by default");
        ExpectEqual(player.CallbackModeProcess, FlowGraphPlayer.CallbackModeProcessEnum.Idle, "CallbackModeProcess default");
        ExpectEqual((int)FlowGraphPlayer.CallbackModeProcessEnum.Max, 3, "CallbackModeProcessEnum.Max");
        player.Graph = graph;
        var board = new FlowBlackboard();
        player.Blackboard = board;
        var label = new Label();
        AddChild(label);
        AddChild(player);
        var calls = new List<string>();
        player.Started += () => calls.Add("started");
        player.Stopped += () => calls.Add("stopped");
        player.NodeEntered += (id, node) => label.Text = node.GetTitle();
        player.NodeExited += (id, node) => calls.Add("exited");
        player.NodeWaiting += (id, node) => calls.Add("waiting");
        player.VariableChanged += (name, value) => calls.Add(name);
        player.EventEmitted += (name, arguments) => calls.Add(name);
        player.Finished += endId => label.Text = "Done";

        Expect(!player.IsProcessingInternal(), "an idle player does not process");
        ExpectEqual(player.Play(), Error.Ok, "Play returns Ok");
        Expect(player.IsPlaying(), "IsPlaying after Play");
        ExpectEqual(label.Text, "Wait a moment", "NodeEntered: the label shows the waiting node");
        Expect(player.IsProcessingInternal(), "waiting on a FlowWaitNode: processing on");
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        ExpectEqual(player.GetPlayback().GetCurrentNodeId(), choiceId, "the wait ended in the scene tree");
        Expect(!player.IsProcessingInternal(), "waiting for a choice: processing off");
        player.GetPlayback().EmitEvent("hint");
        player.GetPlayback().Choose(0);
        ExpectEqual(label.Text, "Done", "Finished: the label shows the end");
        Expect(!player.IsPlaying(), "the player finished");
        ExpectEqual(board.GetVariable("left").AsBool(), true, "the player plays on its blackboard");
        Expect(calls.SequenceEqual(["started", "exited", "waiting", "exited", "waiting", "hint", "left", "exited", "exited"]), "the player forwards the signals of its playback");

        player.CallbackModeProcess = FlowGraphPlayer.CallbackModeProcessEnum.Manual;
        player.Play();
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        ExpectEqual(player.GetPlayback().GetCurrentNodeId(), waitId, "CallbackModeProcess Manual: no processing by itself");
        player.GetPlayback().Process(0.2);
        ExpectEqual(player.GetPlayback().GetCurrentNodeId(), choiceId, "CallbackModeProcess Manual: Process(delta) advances");
        player.Stop();
        Expect(!player.IsPlaying(), "Stop");
        ExpectEqual(calls[^1], "stopped", "Stopped is forwarded");

        var autostart = new FlowGraphPlayer { Graph = graph, Autostart = true };
        AddChild(autostart);
        Expect(autostart.IsPlaying(), "Autostart plays when the node is ready");
        autostart.QueueFree();
        player.QueueFree();
        label.QueueFree();
    }
}
