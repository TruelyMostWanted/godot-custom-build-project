using Godot;

namespace TruelyInteractive.FlowGraphDebugger;

using FlowGraphPlayback = Godot.FlowGraphPlayback;

/// <summary>
/// Minimal example of Flow Graph Debugger. Same as scripts/gd/Example.gd. The guide's only snippet is an editor plugin
/// (needs the editor): left out. Run this scene from the editor (F6) with resources/debug_dialogue.tres open in the FlowGraph dock.
/// </summary>
public partial class Example : Node
{
    private const string GraphPath = "res://flow-graph-debugger/resources/debug_dialogue.tres";

    public override async void _Ready()
    {
        // Watch a running graph: a graph saved as its own file is shown in the dock.
        var graph = GD.Load<FlowGraph>(GraphPath);
        var playerA = new FlowGraphPlayer { Graph = graph };
        AddChild(playerA);
        playerA.NodeWaiting += (nodeId, node) => PrintWaiting(playerA, nodeId);
        playerA.Play();

        // Several playbacks: the second one starts later, the dock shows a selector.
        await ToSignal(GetTree().CreateTimer(0.7), SceneTreeTimer.SignalName.Timeout);
        var playerB = new FlowGraphPlayer { Graph = graph };
        AddChild(playerB);
        playerB.NodeWaiting += (nodeId, node) => PrintWaiting(playerB, nodeId);
        playerB.Play();

        await ToSignal(playerB, FlowGraphPlayer.SignalName.Finished);
        GetTree().Quit();
    }

    // Follow into subgraphs: the current graph differs from the started graph inside a subgraph.
    private static void PrintWaiting(FlowGraphPlayer player, long nodeId)
    {
        FlowGraphPlayback playback = player.GetPlayback();
        GD.Print($"Playback {playback.GetInstanceId()} waits at node {nodeId} in {playback.GetCurrentGraph().ResourcePath}");
    }
}
