extends FeatureTest
## Full test of Flow Graph Debugger: what the game side reports (graph paths, current graph, stack depth).
## The dock highlight itself is checked by hand with scenes/example_gd.tscn. Same tests as scripts/cs/Test.cs.

const GRAPH_PATH := "res://flow-graph-debugger/resources/debug_dialogue.tres"
const WAIT_STEP := 1.6


func _run_tests() -> void:
	_test_without_debugger()
	_test_graph_paths()
	_test_follow_subgraph()


func _test_without_debugger() -> void:
	expect(not EngineDebugger.is_active(), "no debugger attached: playbacks run without messages")


# The editor matches graphs by resource path.
func _test_graph_paths() -> void:
	var graph: FlowGraph = load(GRAPH_PATH)
	expect_equal(graph.resource_path, GRAPH_PATH, "a graph saved as a file has its file path")
	var copy: FlowGraph = graph.duplicate()
	expect(copy.resource_path.is_empty(), "a duplicated graph has no path (not shown)")
	var code_graph := FlowGraph.new()
	expect(code_graph.resource_path.is_empty(), "a graph created in code has no path (not shown)")


func _test_follow_subgraph() -> void:
	var playback := FlowGraphPlayback.new()
	expect_equal(playback.start(load(GRAPH_PATH)), OK, "start")
	expect(playback.is_waiting(), "waits at the first wait node")
	expect_equal(playback.get_current_node_id(), 1, "the current node is 1")
	expect_equal(playback.get_current_graph().resource_path, GRAPH_PATH, "the current graph is the started graph")
	playback.process(WAIT_STEP)
	expect_equal(playback.get_graph_stack_depth(), 2, "inside the subgraph")
	expect_equal(playback.get_current_node_id(), 1, "waits at the wait node of the subgraph")
	var inner_path: String = playback.get_current_graph().resource_path
	expect(inner_path.begins_with(GRAPH_PATH + "::"), "a built-in subgraph has a sub-resource path (%s)" % inner_path)
	playback.process(WAIT_STEP)
	expect_equal(playback.get_graph_stack_depth(), 1, "back in the outer graph")
	expect_equal(playback.get_current_node_id(), 3, "waits at the last wait node")
	playback.process(WAIT_STEP)
	expect(not playback.is_playing(), "finished")
