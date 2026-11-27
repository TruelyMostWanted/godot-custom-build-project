extends FeatureTest
## Full API test of Flow Graph Playback: FlowGraphPlayback, FlowGraphPlayer, FlowGraphState, FlowExit and the runtime
## virtuals of FlowNode. Same tests as scripts/cs/Test.cs.

const GRAPH_PATH := "user://flow_graph_playback_test.tres"
const STATE_PATH := "user://flow_graph_playback_state.tres"


func _run_tests() -> void:
	_test_start_and_stop()
	_test_signals()
	_test_choices()
	_test_waiting()
	_test_travel()
	_test_variables()
	_test_data_inputs()
	_test_subgraph()
	_test_custom_nodes()
	_test_state()
	_test_guards()
	await _test_player()


# Start(0) -> LockedChoiceNode(1) -a-> Emit "took_a" [1, "two"](2) -> End "a"(3) / -b-> End "b"(4).
func _make_choice_graph() -> FlowGraph:
	var graph := FlowGraph.new()
	graph.add_node(FlowStartNode.new())
	graph.add_node(LockedChoiceNode.new())
	var event := FlowEmitEventNode.new()
	event.event_name = &"took_a"
	event.arguments = [1, "two"]
	graph.add_node(event)
	var end_a := FlowEndNode.new()
	end_a.end_name = &"a"
	graph.add_node(end_a)
	var end_b := FlowEndNode.new()
	end_b.end_name = &"b"
	graph.add_node(end_b)
	graph.connect_nodes(0, &"out", 1, &"in")
	graph.connect_nodes(1, &"a", 2, &"in")
	graph.connect_nodes(2, &"out", 3, &"in")
	graph.connect_nodes(1, &"b", 4, &"in")
	return graph


# Start(0) -> Wait(1) -> End(2).
func _make_wait_graph(seconds: float) -> FlowGraph:
	var graph := FlowGraph.new()
	graph.add_node(FlowStartNode.new())
	var wait := FlowWaitNode.new()
	wait.seconds = seconds
	graph.add_node(wait)
	graph.add_node(FlowEndNode.new())
	graph.connect_nodes(0, &"out", 1, &"in")
	graph.connect_nodes(1, &"out", 2, &"in")
	return graph


func _test_start_and_stop() -> void:
	var playback := FlowGraphPlayback.new()
	expect_equal(playback.start(FlowGraph.new()), ERR_INVALID_DATA, "start without a start node returns ERR_INVALID_DATA")
	var graph: FlowGraph = _make_choice_graph()
	var calls: Array[String] = []
	playback.started.connect(func() -> void: calls.append("started"))
	playback.stopped.connect(func() -> void: calls.append("stopped"))
	expect_equal(playback.start(graph), OK, "start returns OK")
	expect(playback.is_playing(), "is_playing after start")
	expect(playback.get_graph() == graph, "get_graph")
	expect_equal(playback.get_current_node_id(), 1, "get_current_node_id: the choice node")
	expect(playback.get_current_node() is LockedChoiceNode, "get_current_node")
	playback.stop()
	expect(not playback.is_playing(), "stop ends the playback")
	expect_equal(calls, ["started", "stopped"], "started and stopped are emitted")
	playback.start(graph, 4)
	expect_equal(playback.get_history(), PackedInt32Array([4]), "start from a node id")
	expect_equal(FlowGraphPlayback.START_NODE_DEFAULT, -1, "START_NODE_DEFAULT")


func _test_signals() -> void:
	var playback := FlowGraphPlayback.new()
	var entered: Array[int] = []
	var exited: Array[int] = []
	var waiting: Array[int] = []
	var events: Array = []
	var finished: Array[int] = []
	playback.node_entered.connect(func(id: int, _node: FlowNode) -> void: entered.append(id))
	playback.node_exited.connect(func(id: int, _node: FlowNode) -> void: exited.append(id))
	playback.node_waiting.connect(func(id: int, _node: FlowNode) -> void: waiting.append(id))
	playback.event_emitted.connect(func(event_name: StringName, arguments: Array) -> void: events.append([event_name, arguments]))
	playback.finished.connect(func(end_id: int) -> void: finished.append(end_id))
	playback.start(_make_choice_graph())
	expect_equal(entered, [0, 1], "node_entered for the start and the choice node")
	expect_equal(exited, [0], "node_exited for the start node")
	expect_equal(waiting, [1], "node_waiting for the choice node")
	playback.emit_event(&"manual", [7])
	expect_equal(events, [[&"manual", [7]]], "emit_event emits event_emitted")
	playback.choose(0)
	expect_equal(events[1], [&"took_a", [1, "two"]], "a FlowEmitEventNode emits event_emitted with its arguments")
	expect_equal(finished, [3], "finished with the id of the end node")


func _test_choices() -> void:
	var graph: FlowGraph = _make_choice_graph()
	var playback := FlowGraphPlayback.new()
	playback.set_variable(&"locked", true)
	playback.start(graph)
	expect(playback.is_waiting(), "is_waiting at the choice node")
	var exits: Array[FlowExit] = playback.get_available_exits()
	expect_equal(exits.size(), 2, "get_available_exits returns two exits")
	expect_equal(exits[0].label, "Take A", "FlowExit.label")
	expect_equal(exits[0].port, &"a", "FlowExit.port")
	expect_equal(exits[0].node_id, 2, "FlowExit.node_id is the target node")
	expect(exits[0].enabled, "FlowExit.enabled")
	expect(not exits[1].enabled, "_get_available_exits disables b while locked")
	expect_equal(playback.choose(0), OK, "choose returns OK")
	expect_equal(playback.get_history(), PackedInt32Array([0, 1, 2, 3]), "get_history after choose(0)")
	expect_equal(playback.get_variable(&"left"), true, "_exit runs when the node is left")
	playback.set_variable(&"locked", false)
	playback.start(graph)
	expect(playback.get_available_exits()[1].enabled, "b is enabled when unlocked")
	expect_equal(playback.advance_to_exit(&"b"), OK, "advance_to_exit returns OK")
	expect_equal(playback.get_history()[-1], 4, "advance_to_exit leaves through the named exit")
	playback.start(graph)
	expect_equal(playback.advance(), OK, "advance returns OK")
	expect_equal(playback.get_history()[-1], 3, "advance takes the first flow output")
	expect_equal(playback.advance(), ERR_UNAVAILABLE, "advance without a waiting node returns ERR_UNAVAILABLE")


func _test_waiting() -> void:
	var playback := FlowGraphPlayback.new()
	playback.start(_make_wait_graph(0.5))
	expect(playback.is_waiting(), "a FlowWaitNode waits")
	playback.process(0.3)
	expect(playback.is_waiting(), "process(0.3): still waiting for 0.5 s")
	playback.process(0.3)
	expect(not playback.is_playing(), "process: the wait ends after 0.5 s")


func _test_travel() -> void:
	var graph: FlowGraph = _make_choice_graph()
	var playback := FlowGraphPlayback.new()
	var events: Array[StringName] = []
	playback.event_emitted.connect(func(event_name: StringName, _arguments: Array) -> void: events.append(event_name))
	playback.start(graph)
	expect_equal(playback.get_travel_path(3), PackedInt32Array([1, 2, 3]), "get_travel_path")
	expect_equal(playback.travel(3), OK, "travel walks")
	expect_equal(events, [&"took_a"], "walking runs the nodes on the way")
	expect(not playback.is_playing(), "travel to an end node finishes")
	playback.start(graph)
	playback.travel(3, FlowGraphPlayback.TRAVEL_MODE_TELEPORT)
	expect_equal(events.size(), 1, "TRAVEL_MODE_TELEPORT skips the nodes on the way")
	playback.start(graph)
	expect_equal(playback.travel(0), ERR_CANT_RESOLVE, "walking without a path returns ERR_CANT_RESOLVE")
	expect_equal(FlowGraphPlayback.TRAVEL_MODE_MAX, 2, "TRAVEL_MODE_MAX")


func _test_variables() -> void:
	var graph: FlowGraph = _make_wait_graph(1.0)
	var gold := FlowVariable.new()
	gold.name = &"gold"
	gold.default_value = 6
	var level := FlowVariable.new()
	level.name = &"level"
	level.default_value = 1
	graph.variables = [gold, level]
	var board := FlowBlackboard.new()
	board.set_variable(&"gold", 9)
	var playback := FlowGraphPlayback.new()
	playback.blackboard = board
	expect(playback.blackboard == board, "blackboard")
	playback.start(graph)
	expect_equal(playback.get_variable(&"gold"), 9, "start keeps an existing value")
	expect_equal(playback.get_variable(&"level"), 1, "start adds a declared variable with its default value")
	var changed: Array = []
	playback.variable_changed.connect(func(variable: StringName, value: Variant) -> void: changed.append([variable, value]))
	playback.set_variable(&"gold", 120)
	expect_equal(changed, [[&"gold", 120]], "variable_changed is emitted")
	expect_equal(board.get_variable(&"gold"), 120, "set_variable writes to the blackboard")
	expect(playback.has_variable(&"gold"), "has_variable")
	expect(not playback.has_variable(&"missing"), "has_variable of an unknown name")


# Start -> Set "total" (value = expression "gold * 2") -> Branch (condition = expression "total > 10") -> End "rich" / End.
# Both expression inputs are unconnected and read the variables of the same name.
func _test_data_inputs() -> void:
	var graph := FlowGraph.new()
	var gold := FlowVariable.new()
	gold.name = &"gold"
	gold.default_value = 6
	graph.variables = [gold]
	var start: int = graph.add_node(FlowStartNode.new())
	var set_total := FlowSetVariableNode.new()
	set_total.variable_name = &"total"
	var set_id: int = graph.add_node(set_total)
	var twice := FlowExpressionNode.new()
	twice.input_names = PackedStringArray(["gold"])
	twice.expression = "gold * 2"
	var twice_id: int = graph.add_node(twice)
	var test := FlowExpressionNode.new()
	test.input_names = PackedStringArray(["total"])
	test.expression = "total > 10"
	var test_id: int = graph.add_node(test)
	var branch: int = graph.add_node(FlowBranchNode.new())
	var rich_id: int = graph.add_node(FlowEndNode.new())
	var poor_id: int = graph.add_node(FlowEndNode.new())
	graph.connect_nodes(start, &"out", set_id, &"in")
	graph.connect_nodes(twice_id, &"result", set_id, &"value")
	graph.connect_nodes(set_id, &"out", branch, &"in")
	graph.connect_nodes(test_id, &"result", branch, &"condition")
	graph.connect_nodes(branch, &"true", rich_id, &"in")
	graph.connect_nodes(branch, &"false", poor_id, &"in")
	var playback := FlowGraphPlayback.new()
	playback.start(graph)
	expect_equal(playback.get_variable(&"total"), 12, "an unconnected expression input reads the variable")
	expect_equal(playback.get_history()[-1], rich_id, "the branch reads its condition when entered")
	expect_equal(playback.get_node_input_value(set_id, &"value"), 12, "get_node_input_value")


# Outer: Start -> Subgraph (inner choice graph, exit_names ["b"]) -b-> End "after_b" / -out-> End.
func _test_subgraph() -> void:
	var inner: FlowGraph = _make_choice_graph()
	var outer := FlowGraph.new()
	var start: int = outer.add_node(FlowStartNode.new())
	var subgraph := FlowSubgraphNode.new()
	subgraph.graph = inner
	subgraph.exit_names = PackedStringArray(["b"])
	var sub_id: int = outer.add_node(subgraph)
	var after_b_id: int = outer.add_node(FlowEndNode.new())
	var after_other_id: int = outer.add_node(FlowEndNode.new())
	outer.connect_nodes(start, &"out", sub_id, &"in")
	outer.connect_nodes(sub_id, &"b", after_b_id, &"in")
	outer.connect_nodes(sub_id, &"out", after_other_id, &"in")
	var playback := FlowGraphPlayback.new()
	playback.start(outer)
	expect_equal(playback.get_graph_stack_depth(), 2, "get_graph_stack_depth inside the subgraph")
	expect(playback.get_current_graph() == inner, "get_current_graph is the inner graph")
	expect(playback.get_graph() == outer, "get_graph stays the outer graph")
	playback.advance_to_exit(&"b")
	expect_equal(playback.get_history()[-1], after_b_id, "an inner end named in exit_names leaves through that output")
	playback.start(outer)
	playback.choose(0)
	expect_equal(playback.get_history()[-1], after_other_id, "another inner end leaves through out")


func _test_custom_nodes() -> void:
	var graph := FlowGraph.new()
	var start: int = graph.add_node(FlowStartNode.new())
	var two := FlowExpressionNode.new()
	two.expression = "2"
	var two_id: int = graph.add_node(two)
	var three := FlowExpressionNode.new()
	three.expression = "3"
	var three_id: int = graph.add_node(three)
	var add_id: int = graph.add_node(AddNode.new())
	graph.connect_nodes(two_id, &"result", add_id, &"a")
	graph.connect_nodes(three_id, &"result", add_id, &"b")
	var set_sum := FlowSetVariableNode.new()
	set_sum.variable_name = &"sum"
	var set_id: int = graph.add_node(set_sum)
	graph.connect_nodes(add_id, &"sum", set_id, &"value")
	var has_key := ItemCheckNode.new()
	has_key.item = &"key"
	var has_key_id: int = graph.add_node(has_key)
	var wait_id: int = graph.add_node(WaitSecondsNode.new())
	var no_key_id: int = graph.add_node(FlowEndNode.new())
	graph.connect_nodes(start, &"out", set_id, &"in")
	graph.connect_nodes(set_id, &"out", has_key_id, &"in")
	graph.connect_nodes(has_key_id, &"yes", wait_id, &"in")
	graph.connect_nodes(has_key_id, &"no", no_key_id, &"in")
	var playback := FlowGraphPlayback.new()
	playback.set_variable(&"items", [&"key"])
	playback.start(graph)
	expect_equal(playback.get_variable(&"sum"), 5, "_evaluate_output with get_input_value: 2 + 3")
	expect_equal(playback.get_current_node_id(), wait_id, "_choose_exit took yes")
	playback.process(2.0)
	expect(playback.is_waiting(), "_process_waiting: still waiting after 2 s")
	playback.process(1.0)
	expect(not playback.is_playing(), "_process_waiting returned true after 3 s")
	playback.set_variable(&"items", [])
	playback.start(graph)
	expect_equal(playback.get_history()[-1], no_key_id, "_choose_exit took no")
	expect_equal(FlowGraphPlayback.ENTER_RESULT_MAX, 3, "ENTER_RESULT_MAX")


func _test_state() -> void:
	expect_equal(ResourceSaver.save(_make_choice_graph(), GRAPH_PATH), OK, "save the graph")
	var graph := load(GRAPH_PATH) as FlowGraph
	var playback := FlowGraphPlayback.new()
	playback.set_variable(&"coins", 5)
	playback.start(graph)
	expect_equal(ResourceSaver.save(playback.get_state(), STATE_PATH), OK, "save the state")
	var state := ResourceLoader.load(STATE_PATH, "", ResourceLoader.CACHE_MODE_IGNORE) as FlowGraphState
	expect_equal(state.graph_path, graph.resource_path, "FlowGraphState.graph_path")
	expect_equal(state.node_stack, PackedInt32Array([1]), "FlowGraphState.node_stack")
	expect_equal(state.history, PackedInt32Array([0, 1]), "FlowGraphState.history")
	expect(state.waiting, "FlowGraphState.waiting")
	var memory: Dictionary = state.node_memory[0]
	expect_equal(memory[1], 1, "FlowGraphState.node_memory of the choice node")
	expect_equal(state.variables[&"coins"], 5, "FlowGraphState.variables")
	expect_equal(state.layout_version, FlowGraphState.STATE_LAYOUT_VERSION, "FlowGraphState.layout_version")

	var restored := FlowGraphPlayback.new()
	var waiting: Array[int] = []
	restored.node_waiting.connect(func(id: int, _node: FlowNode) -> void: waiting.append(id))
	expect_equal(restored.set_state(state), OK, "set_state loads the graph")
	expect(restored.get_graph() == graph, "set_state uses the cached graph")
	expect_equal(waiting, [1], "set_state waits again and emits node_waiting")
	expect_equal(restored.get_variable(&"coins"), 5, "set_state restores the variables")
	expect_equal(restored.choose(0), OK, "the playback continues after set_state")

	var newer: FlowGraphState = state.duplicate()
	newer.layout_version = FlowGraphState.STATE_LAYOUT_VERSION + 1
	expect_equal(FlowGraphPlayback.new().set_state(newer), ERR_FILE_UNRECOGNIZED, "a newer layout_version: ERR_FILE_UNRECOGNIZED")
	var unknown_node: FlowGraphState = state.duplicate()
	unknown_node.node_stack = PackedInt32Array([99])
	expect_equal(FlowGraphPlayback.new().set_state(unknown_node), ERR_INVALID_DATA, "an unknown node: ERR_INVALID_DATA")
	var missing_graph: FlowGraphState = state.duplicate()
	missing_graph.graph_path = "user://missing_graph.tres"
	expect_equal(FlowGraphPlayback.new().set_state(missing_graph), ERR_CANT_OPEN, "a missing graph: ERR_CANT_OPEN")


func _test_guards() -> void:
	var playback := FlowGraphPlayback.new()
	expect_equal(playback.max_steps_per_advance, 1024, "max_steps_per_advance default")
	expect_equal(playback.max_data_depth, 256, "max_data_depth default")
	expect_equal(playback.max_subgraph_depth, 64, "max_subgraph_depth default")
	var stops: Array[int] = [0]
	playback.stopped.connect(func() -> void: stops[0] += 1)

	# Start -> Emit "ping"(1) -> Emit "pong"(2) -> back to 1.
	var flow_loop := FlowGraph.new()
	flow_loop.add_node(FlowStartNode.new())
	var ping := FlowEmitEventNode.new()
	ping.event_name = &"ping"
	flow_loop.add_node(ping)
	var pong := FlowEmitEventNode.new()
	pong.event_name = &"pong"
	flow_loop.add_node(pong)
	flow_loop.connect_nodes(0, &"out", 1, &"in")
	flow_loop.connect_nodes(1, &"out", 2, &"in")
	flow_loop.connect_nodes(2, &"out", 1, &"in")
	playback.max_steps_per_advance = 10
	expect_equal(playback.start(flow_loop), ERR_CYCLIC_LINK, "max_steps_per_advance stops a flow loop")
	expect_equal(stops[0], 1, "a guard emits stopped")

	# Start -> Set "x" whose value comes from two expressions that feed each other.
	var data_loop := FlowGraph.new()
	data_loop.add_node(FlowStartNode.new())
	var set_x := FlowSetVariableNode.new()
	set_x.variable_name = &"x"
	data_loop.add_node(set_x)
	var first := FlowExpressionNode.new()
	first.input_names = PackedStringArray(["a"])
	first.expression = "a"
	data_loop.add_node(first)
	var second := FlowExpressionNode.new()
	second.input_names = PackedStringArray(["b"])
	second.expression = "b"
	data_loop.add_node(second)
	data_loop.connect_nodes(0, &"out", 1, &"in")
	data_loop.connect_nodes(2, &"result", 1, &"value")
	data_loop.connect_nodes(3, &"result", 2, &"a")
	data_loop.connect_nodes(2, &"result", 3, &"b")
	playback.max_data_depth = 8
	expect_equal(playback.start(data_loop), ERR_CYCLIC_LINK, "max_data_depth stops a data loop")

	# Start -> Subgraph that contains its own graph.
	var self_graph := FlowGraph.new()
	self_graph.add_node(FlowStartNode.new())
	var inner := FlowSubgraphNode.new()
	inner.graph = self_graph
	self_graph.add_node(inner)
	self_graph.connect_nodes(0, &"out", 1, &"in")
	playback.max_subgraph_depth = 4
	expect_equal(playback.start(self_graph), ERR_CYCLIC_LINK, "max_subgraph_depth stops a subgraph that contains itself")
	inner.graph = null


# A FlowGraphPlayer wired to a Label through its signals: Start -> Wait 0.1 s -> LockedChoiceNode -a-> End.
func _test_player() -> void:
	var graph := FlowGraph.new()
	var start: int = graph.add_node(FlowStartNode.new())
	var wait := FlowWaitNode.new()
	wait.seconds = 0.1
	wait.title_override = "Wait a moment"
	var wait_id: int = graph.add_node(wait)
	var choice_id: int = graph.add_node(LockedChoiceNode.new())
	var end_id: int = graph.add_node(FlowEndNode.new())
	graph.connect_nodes(start, &"out", wait_id, &"in")
	graph.connect_nodes(wait_id, &"out", choice_id, &"in")
	graph.connect_nodes(choice_id, &"a", end_id, &"in")

	var player := FlowGraphPlayer.new()
	expect(not player.autostart, "autostart is off by default")
	expect_equal(player.callback_mode_process, FlowGraphPlayer.CALLBACK_MODE_PROCESS_IDLE, "callback_mode_process default")
	expect_equal(FlowGraphPlayer.CALLBACK_MODE_PROCESS_MAX, 3, "CALLBACK_MODE_PROCESS_MAX")
	player.graph = graph
	var board := FlowBlackboard.new()
	player.blackboard = board
	var label := Label.new()
	add_child(label)
	add_child(player)
	var calls: Array[String] = []
	player.started.connect(func() -> void: calls.append("started"))
	player.stopped.connect(func() -> void: calls.append("stopped"))
	player.node_entered.connect(func(_id: int, node: FlowNode) -> void: label.text = node.get_title())
	player.node_exited.connect(func(_id: int, _node: FlowNode) -> void: calls.append("exited"))
	player.node_waiting.connect(func(_id: int, _node: FlowNode) -> void: calls.append("waiting"))
	player.variable_changed.connect(func(variable: StringName, _value: Variant) -> void: calls.append(String(variable)))
	player.event_emitted.connect(func(event_name: StringName, _arguments: Array) -> void: calls.append(String(event_name)))
	player.finished.connect(func(_end_id: int) -> void: label.text = "Done")

	expect(not player.is_processing_internal(), "an idle player does not process")
	expect_equal(player.play(), OK, "play returns OK")
	expect(player.is_playing(), "is_playing after play")
	expect_equal(label.text, "Wait a moment", "node_entered: the label shows the waiting node")
	expect(player.is_processing_internal(), "waiting on a FlowWaitNode: processing on")
	await get_tree().create_timer(0.5).timeout
	expect_equal(player.get_playback().get_current_node_id(), choice_id, "the wait ended in the scene tree")
	expect(not player.is_processing_internal(), "waiting for a choice: processing off")
	player.get_playback().emit_event(&"hint")
	player.get_playback().choose(0)
	expect_equal(label.text, "Done", "finished: the label shows the end")
	expect(not player.is_playing(), "the player finished")
	expect_equal(board.get_variable(&"left"), true, "the player plays on its blackboard")
	expect_equal(calls, ["started", "exited", "waiting", "exited", "waiting", "hint", "left", "exited", "exited"], "the player forwards the signals of its playback")

	player.callback_mode_process = FlowGraphPlayer.CALLBACK_MODE_PROCESS_MANUAL
	player.play()
	await get_tree().create_timer(0.2).timeout
	expect_equal(player.get_playback().get_current_node_id(), wait_id, "CALLBACK_MODE_PROCESS_MANUAL: no processing by itself")
	player.get_playback().process(0.2)
	expect_equal(player.get_playback().get_current_node_id(), choice_id, "CALLBACK_MODE_PROCESS_MANUAL: process(delta) advances")
	player.stop()
	expect(not player.is_playing(), "stop")
	expect_equal(calls[-1], "stopped", "stopped is forwarded")

	var autostart := FlowGraphPlayer.new()
	autostart.graph = graph
	autostart.autostart = true
	add_child(autostart)
	expect(autostart.is_playing(), "autostart plays when the node is ready")
	autostart.queue_free()
	player.queue_free()
	label.queue_free()
