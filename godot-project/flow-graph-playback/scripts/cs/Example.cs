using Godot;

namespace TruelyInteractive.FlowGraphPlayback;

using FlowGraphPlayback = Godot.FlowGraphPlayback;

/// <summary>
/// Minimal example of Flow Graph Playback: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.
/// The scene root is called "Main" because WaitForSignalNodeCs looks for Main/ContinueButton.
/// </summary>
public partial class Example : Node
{
    private Label _label;
    private VBoxContainer _menu;
    private FlowGraphPlayer _player;

    public override async void _Ready()
    {
        // Start and play: Start(0) -> Emit say "Hello"(1) -> Wait 2 s(2) -> Emit say "Bye"(3) -> End(4)
        var graph = new FlowGraph();
        int start = graph.AddNode(new FlowStartNode());
        int helloId = graph.AddNode(new FlowEmitEventNode { EventName = "say", Arguments = ["Hello"] });
        int waitId = graph.AddNode(new FlowWaitNode { Seconds = 2.0 });
        int byeId = graph.AddNode(new FlowEmitEventNode { EventName = "say", Arguments = ["Bye"] });
        int end = graph.AddNode(new FlowEndNode());
        graph.ConnectNodes(start, "out", helloId, "in");
        graph.ConnectNodes(helloId, "out", waitId, "in");
        graph.ConnectNodes(waitId, "out", byeId, "in");
        graph.ConnectNodes(byeId, "out", end, "in");
        _player = new FlowGraphPlayer { Graph = graph };
        AddChild(_player);
        _player.Play();
        _player.Play(waitId);
        var playback = new FlowGraphPlayback();
        Error err = playback.Start(graph);
        GD.Print("Start: ", err);
        playback.Stop();

        // Signals: a player wired to a Label
        _label = new Label();
        AddChild(_label);
        _player.EventEmitted += (name, arguments) =>
        {
            if (name == "say")
            {
                _label.Text = (string)arguments[0];
            }
        };
        _player.Finished += endId => _label.Text = "";
        _player.Play();
        GD.Print("Label: ", _label.Text);
        await ToSignal(_player, FlowGraphPlayer.SignalName.Finished);
        GD.Print("Label after the end: '", _label.Text, "'");

        // Choices and exits: Start -> LockedChoiceNodeCs -a-> Emit "took_a" -> End / -b-> End
        var choiceGraph = new FlowGraph();
        int choiceStart = choiceGraph.AddNode(new FlowStartNode());
        int choiceId = choiceGraph.AddNode(new LockedChoiceNodeCs());
        int tookAId = choiceGraph.AddNode(new FlowEmitEventNode { EventName = "took_a" });
        int endA = choiceGraph.AddNode(new FlowEndNode());
        int endB = choiceGraph.AddNode(new FlowEndNode());
        choiceGraph.ConnectNodes(choiceStart, "out", choiceId, "in");
        choiceGraph.ConnectNodes(choiceId, "a", tookAId, "in");
        choiceGraph.ConnectNodes(tookAId, "out", endA, "in");
        choiceGraph.ConnectNodes(choiceId, "b", endB, "in");
        _menu = new VBoxContainer();
        AddChild(_menu);
        _player.Graph = choiceGraph;
        _player.NodeWaiting += OnNodeWaiting;
        _player.Play();
        var firstButton = _menu.GetChild<Button>(0);
        GD.Print("Menu: ", firstButton.Text, ", ", _menu.GetChild<Button>(1).Text);
        firstButton.EmitSignal(BaseButton.SignalName.Pressed);
        GD.Print("Chosen, playing: ", _player.IsPlaying());
        _player.NodeWaiting -= OnNodeWaiting;

        // Waiting: WaitForSignalNodeCs continues when Main/ContinueButton is pressed
        var signalGraph = new FlowGraph();
        int signalStart = signalGraph.AddNode(new FlowStartNode());
        int signalWait = signalGraph.AddNode(new WaitForSignalNodeCs());
        signalGraph.ConnectNodes(signalStart, "out", signalWait, "in");
        var button = new Button { Name = "ContinueButton" };
        AddChild(button);
        playback.Start(signalGraph);
        GD.Print("Waiting for the button: ", playback.IsWaiting());
        button.EmitSignal(BaseButton.SignalName.Pressed);
        GD.Print("After the button, playing: ", playback.IsPlaying());

        // Travel
        playback.Start(choiceGraph);
        GD.Print(string.Join(", ", playback.GetTravelPath(endA)));
        playback.Travel(endA);
        playback.Start(choiceGraph);
        playback.Travel(endA, FlowGraphPlayback.TravelMode.Teleport);
        GD.Print("After teleport, history: ", string.Join(", ", playback.GetHistory()));

        // Variables and the blackboard
        playback.VariableChanged += (name, value) => GD.Print(name, " = ", value);
        playback.Start(graph);
        playback.SetVariable("gold", 120);
        GD.Print(playback.GetVariable("gold"), " ", playback.HasVariable("gold"));
        playback.Stop();

        // Data inputs: the input "gold" of the expression stays unconnected and reads the variable
        var dataGraph = new FlowGraph();
        int dataStart = dataGraph.AddNode(new FlowStartNode());
        var check = new FlowExpressionNode { InputNames = ["gold"], Expression = "gold >= 10" };
        int checkId = dataGraph.AddNode(check);
        int branchId = dataGraph.AddNode(new FlowBranchNode());
        dataGraph.ConnectNodes(checkId, "result", branchId, "condition");
        int rich = dataGraph.AddNode(new FlowEndNode());
        dataGraph.ConnectNodes(dataStart, "out", branchId, "in");
        dataGraph.ConnectNodes(branchId, "true", rich, "in");
        playback.Start(dataGraph);
        int[] dataHistory = playback.GetHistory();
        GD.Print("gold >= 10 took true: ", dataHistory[^1] == rich);

        // Subgraphs: resources/shop.tres ends at an End node named "bought"
        var shopGraph = new FlowGraph();
        int shopStart = shopGraph.AddNode(new FlowStartNode());
        int thanksId = shopGraph.AddNode(new FlowEndNode());
        int farewellId = shopGraph.AddNode(new FlowEndNode());
        var shop = new FlowSubgraphNode
        {
            Graph = GD.Load<FlowGraph>("res://flow-graph-playback/resources/shop.tres"),
            ExitNames = ["bought", "left"],
        };
        int shopId = shopGraph.AddNode(shop);
        shopGraph.ConnectNodes(shopStart, "out", shopId, "in");
        shopGraph.ConnectNodes(shopId, "bought", thanksId, "in");
        shopGraph.ConnectNodes(shopId, "left", farewellId, "in");
        playback.Start(shopGraph);
        int[] shopHistory = playback.GetHistory();
        GD.Print("The shop ended at thanks: ", shopHistory[^1] == thanksId);

        // Custom node types: AddNodeCs, ItemCheckNodeCs and WaitSecondsNodeCs (scripts/cs/)
        var customGraph = new FlowGraph { Variables = [new FlowVariable { Name = "sum" }] };
        int customStart = customGraph.AddNode(new FlowStartNode());
        int twoId = customGraph.AddNode(new FlowExpressionNode { Expression = "2" });
        int threeId = customGraph.AddNode(new FlowExpressionNode { Expression = "3" });
        int addId = customGraph.AddNode(new AddNodeCs());
        customGraph.ConnectNodes(twoId, "result", addId, "a");
        customGraph.ConnectNodes(threeId, "result", addId, "b");
        int setId = customGraph.AddNode(new FlowSetVariableNode { VariableName = "sum" });
        customGraph.ConnectNodes(addId, "sum", setId, "value");
        int hasKeyId = customGraph.AddNode(new ItemCheckNodeCs { Item = "key" });
        int waitThreeId = customGraph.AddNode(new WaitSecondsNodeCs());
        customGraph.ConnectNodes(customStart, "out", setId, "in");
        customGraph.ConnectNodes(setId, "out", hasKeyId, "in");
        customGraph.ConnectNodes(hasKeyId, "yes", waitThreeId, "in");
        playback.SetVariable("items", new Godot.Collections.Array { new StringName("key") });
        playback.Start(customGraph);
        GD.Print("Waiting at WaitSecondsNodeCs: ", playback.GetCurrentNodeId() == waitThreeId);
        playback.Process(3.0);
        GD.Print("After 3 s, playing: ", playback.IsPlaying());

        // Save and restore
        playback.Start(choiceGraph);
        ResourceSaver.Save(playback.GetState(), "user://flow_state_cs.tres");
        playback.SetState(GD.Load<FlowGraphState>("user://flow_state_cs.tres"));
        GD.Print("Restored at the choice: ", playback.GetCurrentNodeId() == choiceId);

        // Player processing modes
        _player.CallbackModeProcess = FlowGraphPlayer.CallbackModeProcessEnum.Manual;
        _player.Graph = graph;
        _player.Play(waitId);
        _player.GetPlayback().Process(2.0);
        GD.Print("Manual processing ended the wait, playing: ", _player.IsPlaying());

        GetTree().Quit();
    }

    private void OnNodeWaiting(long nodeId, FlowNode node)
    {
        var playback = _player.GetPlayback();
        var exits = playback.GetAvailableExits();
        for (int i = 0; i < exits.Count; i++)
        {
            int index = i;
            var button = new Button { Text = exits[i].Label, Disabled = !exits[i].Enabled };
            button.Pressed += () => playback.Choose(index);
            _menu.AddChild(button);
        }
    }
}
