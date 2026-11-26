extends FeatureCheck
## GDScript checks of 256-feature-flow-graph-playback. Check the same things as scripts/cs/Issue256FeatureFlowGraphPlaybackCheck.cs.

const GRAPH_PATH := "user://256_flow_graph.tres"
const STATE_PATH := "user://256_flow_graph_state.tres"


func _get_check_name() -> String:
	return "256-feature-flow-graph-playback"


func _run_checks() -> void:
	for cls in ["FlowGraphPlayback", "FlowGraphPlayer", "FlowGraphState", "FlowExit"]:
		expect(ClassDB.class_exists(cls), "ClassDB knows %s" % cls)
	_check_choices()
	_check_data()
	_check_subgraph_and_travel()
	_check_state()
	await _check_player_label()


# Start(0) -> Issue256ChoiceNode(1) -> a: EmitEvent "took_a"(2) -> End "a"(3) / b: End "b"(4).
func _make_choice_graph() -> FlowGraph:
	var graph := FlowGraph.new()
	var start := graph.add_node(FlowStartNode.new())
	var choice := graph.add_node(Issue256ChoiceNode.new())
	var event := FlowEmitEventNode.new()
	event.event_name = &"took_a"
	event.arguments = [1, "two"]
	var event_id := graph.add_node(event)
	var end_a := FlowEndNode.new()
	end_a.end_name = &"a"
	var end_a_id := graph.add_node(end_a)
	var end_b := FlowEndNode.new()
	end_b.end_name = &"b"
	var end_b_id := graph.add_node(end_b)
	graph.connect_nodes(start, &"out", choice, &"in")
	graph.connect_nodes(choice, &"a", event_id, &"in")
	graph.connect_nodes(event_id, &"out", end_a_id, &"in")
	graph.connect_nodes(choice, &"b", end_b_id, &"in")
	return graph


func _check_choices() -> void:
	var graph := _make_choice_graph()
	var playback := FlowGraphPlayback.new()
	var events := []
	var finished := []
	playback.event_emitted.connect(func(event_name: StringName, arguments: Array): events.append([event_name, arguments]))
	playback.finished.connect(func(end_id: int): finished.append(end_id))
	playback.set_variable(&"locked", true)
	expect_equal(playback.start(graph), OK, "start")
	expect(playback.is_waiting(), "the script node waits")
	expect_equal(playback.get_current_node_id(), 1, "current node is the choice node")
	var exits := playback.get_available_exits()
	expect_equal(exits.size(), 2, "two exits")
	if exits.size() == 2:
		expect_equal(exits[0].label, "Take A", "exit label")
		expect_equal(exits[0].node_id, 2, "exit a leads to node 2")
		expect(not exits[1].enabled, "exit b is disabled while locked")
	expect_equal(playback.choose(0), OK, "choose a")
	expect_equal(events, [[&"took_a", [1, "two"]]], "event_emitted with arguments")
	expect_equal(finished, [3], "finished at end a")
	expect_equal(playback.get_history(), PackedInt32Array([0, 1, 2, 3]), "history")

	playback.set_variable(&"locked", false)
	playback.start(graph)
	expect(playback.get_available_exits()[1].enabled, "exit b is enabled when unlocked")
	expect_equal(playback.advance_to_exit(&"b"), OK, "advance_to_exit b")
	expect_equal(finished, [3, 4], "finished at end b")


# Start -> Set "total" (value = expression "gold * 2") -> Branch (condition = expression "total > 10") -> End "rich" / End.
# Both expression inputs are unconnected and read the variables of the same name.
func _check_data() -> void:
	var graph := FlowGraph.new()
	var gold := FlowVariable.new()
	gold.name = &"gold"
	gold.type = TYPE_INT
	gold.default_value = 6
	graph.variables = [gold]
	var start := graph.add_node(FlowStartNode.new())
	var set_total := FlowSetVariableNode.new()
	set_total.variable_name = &"total"
	var set_id := graph.add_node(set_total)
	var twice := FlowExpressionNode.new()
	twice.input_names = PackedStringArray(["gold"])
	twice.expression = "gold * 2"
	var twice_id := graph.add_node(twice)
	var test := FlowExpressionNode.new()
	test.input_names = PackedStringArray(["total"])
	test.expression = "total > 10"
	var test_id := graph.add_node(test)
	var branch := graph.add_node(FlowBranchNode.new())
	var rich := FlowEndNode.new()
	rich.end_name = &"rich"
	var rich_id := graph.add_node(rich)
	var poor_id := graph.add_node(FlowEndNode.new())
	graph.connect_nodes(start, &"out", set_id, &"in")
	graph.connect_nodes(twice_id, &"result", set_id, &"value")
	graph.connect_nodes(set_id, &"out", branch, &"in")
	graph.connect_nodes(test_id, &"result", branch, &"condition")
	graph.connect_nodes(branch, &"true", rich_id, &"in")
	graph.connect_nodes(branch, &"false", poor_id, &"in")

	var playback := FlowGraphPlayback.new()
	var changed := []
	playback.variable_changed.connect(func(variable: StringName, _value: Variant): changed.append(variable))
	expect_equal(playback.start(graph), OK, "data graph: start")
	expect_equal(playback.get_variable(&"total"), 12, "expression read the variable gold")
	expect_equal(playback.get_history()[-1], rich_id, "branch read total lazily and took true")
	expect(&"total" in changed, "variable_changed for total")
	expect_equal(playback.get_node_input_value(set_id, &"value"), 12, "get_node_input_value")


func _check_subgraph_and_travel() -> void:
	var inner := _make_choice_graph()
	var outer := FlowGraph.new()
	var start := outer.add_node(FlowStartNode.new())
	var subgraph := FlowSubgraphNode.new()
	subgraph.graph = inner
	subgraph.exit_names = PackedStringArray(["b"])
	var sub_id := outer.add_node(subgraph)
	var after_b := FlowEndNode.new()
	after_b.end_name = &"after_b"
	var after_b_id := outer.add_node(after_b)
	var after_other_id := outer.add_node(FlowEndNode.new())
	outer.connect_nodes(start, &"out", sub_id, &"in")
	outer.connect_nodes(sub_id, &"b", after_b_id, &"in")
	outer.connect_nodes(sub_id, &"out", after_other_id, &"in")

	var playback := FlowGraphPlayback.new()
	playback.start(outer)
	expect_equal(playback.get_graph_stack_depth(), 2, "inside the subgraph")
	expect(playback.get_current_graph() == inner, "current graph is the inner graph")
	playback.advance_to_exit(&"b")
	expect_equal(playback.get_history()[-1], after_b_id, "the subgraph returned through its exit b")

	var events := []
	playback.event_emitted.connect(func(event_name: StringName, _arguments: Array): events.append(event_name))
	playback.start(inner)
	expect_equal(playback.get_travel_path(3), PackedInt32Array([1, 2, 3]), "travel path")
	expect_equal(playback.travel(3), OK, "travel (walk)")
	expect_equal(events, [&"took_a"], "walking runs the nodes on the way")
	expect(not playback.is_playing(), "travel to an end node finishes")


func _check_state() -> void:
	expect_equal(ResourceSaver.save(_make_choice_graph(), GRAPH_PATH), OK, "save the graph")
	var graph := load(GRAPH_PATH) as FlowGraph
	var playback := FlowGraphPlayback.new()
	playback.set_variable(&"coins", 5)
	playback.start(graph)
	expect_equal(ResourceSaver.save(playback.get_state(), STATE_PATH), OK, "save the state")
	var state := ResourceLoader.load(STATE_PATH, "", ResourceLoader.CACHE_MODE_IGNORE) as FlowGraphState
	if not expect(state != null, "load the state"):
		return
	expect(not state.graph_path.is_empty(), "the state names the graph")
	expect_equal(state.node_stack, PackedInt32Array([1]), "node stack")
	expect(state.waiting, "the state was waiting")
	expect_equal(state.node_memory[0].get(1), 1, "node memory of the choice node")

	var restored := FlowGraphPlayback.new()
	var waiting := []
	restored.node_waiting.connect(func(id: int, _node: FlowNode): waiting.append(id))
	expect_equal(restored.set_state(state), OK, "set_state loads the graph")
	expect(restored.get_graph() == graph, "the same graph from the cache")
	expect_equal(waiting, [1], "node_waiting is emitted again")
	expect_equal(restored.get_variable(&"coins"), 5, "variables restored")
	expect_equal(restored.choose(0), OK, "continues after the restore")
	expect(not restored.is_playing(), "finished after the restore")


# A FlowGraphPlayer wired to a Label through its signals.
func _check_player_label() -> void:
	var graph := FlowGraph.new()
	var start := graph.add_node(FlowStartNode.new())
	var wait := FlowWaitNode.new()
	wait.seconds = 0.1
	wait.title_override = "Wait a moment"
	var wait_id := graph.add_node(wait)
	var choice_id := graph.add_node(Issue256ChoiceNode.new())
	var end_id := graph.add_node(FlowEndNode.new())
	graph.connect_nodes(start, &"out", wait_id, &"in")
	graph.connect_nodes(wait_id, &"out", choice_id, &"in")
	graph.connect_nodes(choice_id, &"a", end_id, &"in")

	var label := Label.new()
	add_child(label)
	var player := FlowGraphPlayer.new()
	player.graph = graph
	add_child(player)
	player.node_entered.connect(func(_id: int, node: FlowNode): label.text = node.get_title())
	player.finished.connect(func(_end_id: int): label.text = "Done")

	expect(not player.is_processing_internal(), "an idle player does not process")
	expect_equal(player.play(), OK, "play")
	expect_equal(label.text, "Wait a moment", "the label shows the waiting node")
	expect(player.is_processing_internal(), "waiting on a FlowWaitNode: processing on")
	await get_tree().create_timer(0.5).timeout
	expect_equal(player.get_playback().get_current_node_id(), choice_id, "the wait ended in the scene tree")
	expect(not player.is_processing_internal(), "waiting for a choice: processing off")
	player.get_playback().choose(0)
	expect_equal(label.text, "Done", "the label shows the end")
	expect(not player.is_playing(), "the player finished")
	player.queue_free()
	label.queue_free()
