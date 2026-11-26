using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.Issue276FeatureFlowGraphDebugger;

/// <summary>
/// C# checks of 276-feature-flow-graph-debugger. Check the same things as scripts/gd/check.gd.
/// The highlight itself is an editor check by hand: see scripts/gd/debugger_demo.gd and scenes/debugger_demo.tscn.
/// </summary>
public partial class Issue276FeatureFlowGraphDebuggerCheck : FeatureCheckCs
{
    private const string GraphPath = "res://276-feature-flow-graph-debugger/resources/debug_dialogue.tres";

    protected override string CheckName => "276-feature-flow-graph-debugger";

    protected override Task RunChecks()
    {
        // The checks run without the editor's debugger: no messages, playbacks run as usual.
        Expect(!EngineDebugger.IsActive(), "no debugger attached during checks");
        CheckGraphPaths();
        return Task.CompletedTask;
    }

    // The editor matches graphs by resource path: the file path, and "<file>::<id>" for built-in subgraphs.
    private void CheckGraphPaths()
    {
        var graph = GD.Load<FlowGraph>(GraphPath);
        Expect(graph != null, "debug_dialogue.tres loads");
        if (graph == null)
            return;
        ExpectEqual(graph.ResourcePath, GraphPath, "graph saved as a file has its file path");

        var playback = new FlowGraphPlayback();
        ExpectEqual(playback.Start(graph), Error.Ok, "start");
        Expect(playback.IsWaiting(), "waits at the first wait node");
        ExpectEqual(playback.GetCurrentNodeId(), 1, "current node is 1");
        ExpectEqual(playback.GetCurrentGraph().ResourcePath, GraphPath, "current graph is the started graph");

        playback.Process(1.6);
        ExpectEqual(playback.GetGraphStackDepth(), 2, "inside the subgraph");
        ExpectEqual(playback.GetCurrentNodeId(), 1, "waits at the wait node of the subgraph");
        string innerPath = playback.GetCurrentGraph().ResourcePath;
        Expect(innerPath.StartsWith(GraphPath + "::"), $"built-in subgraph has a sub-resource path ({innerPath})");

        playback.Process(1.6);
        ExpectEqual(playback.GetGraphStackDepth(), 1, "back in the outer graph");
        ExpectEqual(playback.GetCurrentNodeId(), 3, "waits at the last wait node");
        playback.Process(1.6);
        Expect(!playback.IsPlaying(), "finished");
    }
}
