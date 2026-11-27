using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;

namespace TruelyInteractive.FlowGraphDebugger;

using FlowGraphPlayback = Godot.FlowGraphPlayback;

/// <summary>
/// Full test of Flow Graph Debugger: what the game side reports (graph paths, current graph, stack depth).
/// The dock highlight itself is checked by hand with scenes/example_cs.tscn. Same tests as scripts/gd/Test.gd.
/// </summary>
public partial class Test : FeatureTest
{
    private const string GraphPath = "res://flow-graph-debugger/resources/debug_dialogue.tres";
    private const double WaitStep = 1.6;

    protected override Task RunTests()
    {
        TestWithoutDebugger();
        TestGraphPaths();
        TestFollowSubgraph();
        return Task.CompletedTask;
    }

    private void TestWithoutDebugger()
    {
        Expect(!EngineDebugger.IsActive(), "no debugger attached: playbacks run without messages");
    }

    // The editor matches graphs by resource path.
    private void TestGraphPaths()
    {
        var graph = GD.Load<FlowGraph>(GraphPath);
        ExpectEqual(graph.ResourcePath, GraphPath, "a graph saved as a file has its file path");
        var copy = (FlowGraph)graph.Duplicate();
        Expect(copy.ResourcePath.Length == 0, "a duplicated graph has no path (not shown)");
        var codeGraph = new FlowGraph();
        Expect(codeGraph.ResourcePath.Length == 0, "a graph created in code has no path (not shown)");
    }

    private void TestFollowSubgraph()
    {
        var playback = new FlowGraphPlayback();
        ExpectEqual(playback.Start(GD.Load<FlowGraph>(GraphPath)), Error.Ok, "start");
        Expect(playback.IsWaiting(), "waits at the first wait node");
        ExpectEqual(playback.GetCurrentNodeId(), 1, "the current node is 1");
        ExpectEqual(playback.GetCurrentGraph().ResourcePath, GraphPath, "the current graph is the started graph");
        playback.Process(WaitStep);
        ExpectEqual(playback.GetGraphStackDepth(), 2, "inside the subgraph");
        ExpectEqual(playback.GetCurrentNodeId(), 1, "waits at the wait node of the subgraph");
        string innerPath = playback.GetCurrentGraph().ResourcePath;
        Expect(innerPath.StartsWith(GraphPath + "::"), $"a built-in subgraph has a sub-resource path ({innerPath})");
        playback.Process(WaitStep);
        ExpectEqual(playback.GetGraphStackDepth(), 1, "back in the outer graph");
        ExpectEqual(playback.GetCurrentNodeId(), 3, "waits at the last wait node");
        playback.Process(WaitStep);
        Expect(!playback.IsPlaying(), "finished");
    }
}
