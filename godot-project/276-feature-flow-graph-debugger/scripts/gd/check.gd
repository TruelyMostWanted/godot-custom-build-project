extends FeatureCheck
## GDScript checks of 276-feature-flow-graph-debugger. Check the same things as scripts/cs/Issue276FeatureFlowGraphDebuggerCheck.cs.
## The highlight itself is an editor check by hand: see scripts/gd/debugger_demo.gd and scenes/debugger_demo.tscn.

const GRAPH_PATH := "res://276-feature-flow-graph-debugger/resources/debug_dialogue.tres"


func _get_check_name() -> String:
	return "276-feature-flow-graph-debugger"


func _run_checks() -> void:
	# The checks run without the editor's debugger: no messages, playbacks run as usual.
	expect(not EngineDebugger.is_active(), "no debugger attached during checks")
	_check_graph_paths()


# The editor matches graphs by resource path: the file path, and "<file>::<id>" for built-in subgraphs.
func _check_graph_paths() -> void:
	var graph: FlowGraph = load(GRAPH_PATH)
	expect(graph != null, "debug_dialogue.tres loads")
	if graph == null:
		return
	expect_equal(graph.resource_path, GRAPH_PATH, "graph saved as a file has its file path")

	var playback := FlowGraphPlayback.new()
	expect_equal(playback.start(graph), OK, "start")
	expect(playback.is_waiting(), "waits at the first wait node")
	expect_equal(playback.get_current_node_id(), 1, "current node is 1")
	expect_equal(playback.get_current_graph().resource_path, GRAPH_PATH, "current graph is the started graph")

	playback.process(1.6)
	expect_equal(playback.get_graph_stack_depth(), 2, "inside the subgraph")
	expect_equal(playback.get_current_node_id(), 1, "waits at the wait node of the subgraph")
	var inner_path := playback.get_current_graph().resource_path
	expect(inner_path.begins_with(GRAPH_PATH + "::"), "built-in subgraph has a sub-resource path (%s)" % inner_path)

	playback.process(1.6)
	expect_equal(playback.get_graph_stack_depth(), 1, "back in the outer graph")
	expect_equal(playback.get_current_node_id(), 3, "waits at the last wait node")
	playback.process(1.6)
	expect(not playback.is_playing(), "finished")
