extends Node
## Minimal example of Flow Graph Playback: the snippets of the wiki guide, in order. The scene root is called "Main"
## because WaitForSignalNode looks for Main/ContinueButton.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Flow-Graph-Playback

var label: Label
var menu: VBoxContainer
var player: FlowGraphPlayer


func _ready() -> void:
	# Start and play: Start(0) -> Emit say "Hello"(1) -> Wait 2 s(2) -> Emit say "Bye"(3) -> End(4)
	var graph := FlowGraph.new()
	var start: int = graph.add_node(FlowStartNode.new())
	var hello := FlowEmitEventNode.new()
	hello.event_name = &"say"
	hello.arguments = ["Hello"]
	var hello_id: int = graph.add_node(hello)
	var wait := FlowWaitNode.new()
	wait.seconds = 2.0
	var wait_id: int = graph.add_node(wait)
	var bye := FlowEmitEventNode.new()
	bye.event_name = &"say"
	bye.arguments = ["Bye"]
	var bye_id: int = graph.add_node(bye)
	var end: int = graph.add_node(FlowEndNode.new())
	graph.connect_nodes(start, "out", hello_id, "in")
	graph.connect_nodes(hello_id, "out", wait_id, "in")
	graph.connect_nodes(wait_id, "out", bye_id, "in")
	graph.connect_nodes(bye_id, "out", end, "in")
	player = FlowGraphPlayer.new()
	player.graph = graph
	add_child(player)
	player.play()
	player.play(wait_id)
	var playback := FlowGraphPlayback.new()
	var err: Error = playback.start(graph)
	print("start: ", error_string(err))
	playback.stop()

	# Signals: a player wired to a Label
	label = Label.new()
	add_child(label)
	player.event_emitted.connect(_on_event)
	player.finished.connect(func(_end_id: int) -> void: label.text = "")
	player.play()
	print("Label: ", label.text)
	await player.finished
	print("Label after the end: '", label.text, "'")

	# Choices and exits: Start -> LockedChoiceNode -a-> Emit "took_a" -> End / -b-> End
	var choice_graph := FlowGraph.new()
	var choice_start: int = choice_graph.add_node(FlowStartNode.new())
	var choice_id: int = choice_graph.add_node(LockedChoiceNode.new())
	var took_a := FlowEmitEventNode.new()
	took_a.event_name = &"took_a"
	var took_a_id: int = choice_graph.add_node(took_a)
	var end_a: int = choice_graph.add_node(FlowEndNode.new())
	var end_b: int = choice_graph.add_node(FlowEndNode.new())
	choice_graph.connect_nodes(choice_start, "out", choice_id, "in")
	choice_graph.connect_nodes(choice_id, "a", took_a_id, "in")
	choice_graph.connect_nodes(took_a_id, "out", end_a, "in")
	choice_graph.connect_nodes(choice_id, "b", end_b, "in")
	menu = VBoxContainer.new()
	add_child(menu)
	player.graph = choice_graph
	player.node_waiting.connect(_on_node_waiting)
	player.play()
	var first_button: Button = menu.get_child(0)
	print("Menu: ", first_button.text, ", ", (menu.get_child(1) as Button).text)
	first_button.pressed.emit()
	print("Chosen, playing: ", player.is_playing())
	player.node_waiting.disconnect(_on_node_waiting)

	# Waiting: WaitForSignalNode continues when Main/ContinueButton is pressed
	var signal_graph := FlowGraph.new()
	var signal_start: int = signal_graph.add_node(FlowStartNode.new())
	var signal_wait: int = signal_graph.add_node(WaitForSignalNode.new())
	signal_graph.connect_nodes(signal_start, "out", signal_wait, "in")
	var button := Button.new()
	button.name = "ContinueButton"
	add_child(button)
	playback.start(signal_graph)
	print("Waiting for the button: ", playback.is_waiting())
	button.pressed.emit()
	print("After the button, playing: ", playback.is_playing())

	# Travel
	playback.start(choice_graph)
	print(playback.get_travel_path(end_a))
	playback.travel(end_a)
	playback.start(choice_graph)
	playback.travel(end_a, FlowGraphPlayback.TRAVEL_MODE_TELEPORT)
	print("After teleport, history: ", playback.get_history())

	# Variables and the blackboard
	playback.variable_changed.connect(func(variable: StringName, value: Variant) -> void: print(variable, " = ", value))
	playback.start(graph)
	playback.set_variable("gold", 120)
	print(playback.get_variable("gold"), " ", playback.has_variable("gold"))
	playback.stop()

	# Data inputs: the input "gold" of the expression stays unconnected and reads the variable
	var data_graph := FlowGraph.new()
	var data_start: int = data_graph.add_node(FlowStartNode.new())
	var check := FlowExpressionNode.new()
	check.input_names = PackedStringArray(["gold"])
	check.expression = "gold >= 10"
	var check_id: int = data_graph.add_node(check)
	var branch_id: int = data_graph.add_node(FlowBranchNode.new())
	data_graph.connect_nodes(check_id, "result", branch_id, "condition")
	var rich: int = data_graph.add_node(FlowEndNode.new())
	data_graph.connect_nodes(data_start, "out", branch_id, "in")
	data_graph.connect_nodes(branch_id, "true", rich, "in")
	playback.start(data_graph)
	print("gold >= 10 took true: ", playback.get_history()[-1] == rich)

	# Subgraphs: resources/shop.tres ends at an End node named "bought"
	var shop_graph := FlowGraph.new()
	var shop_start: int = shop_graph.add_node(FlowStartNode.new())
	var thanks_id: int = shop_graph.add_node(FlowEndNode.new())
	var farewell_id: int = shop_graph.add_node(FlowEndNode.new())
	var shop := FlowSubgraphNode.new()
	shop.graph = preload("res://flow-graph-playback/resources/shop.tres")
	shop.exit_names = PackedStringArray(["bought", "left"])
	var shop_id: int = shop_graph.add_node(shop)
	shop_graph.connect_nodes(shop_start, "out", shop_id, "in")
	shop_graph.connect_nodes(shop_id, "bought", thanks_id, "in")
	shop_graph.connect_nodes(shop_id, "left", farewell_id, "in")
	playback.start(shop_graph)
	print("The shop ended at thanks: ", playback.get_history()[-1] == thanks_id)

	# Custom node types: AddNode, ItemCheckNode and WaitSecondsNode (scripts/gd/)
	var custom_graph := FlowGraph.new()
	var sum := FlowVariable.new()
	sum.name = &"sum"
	custom_graph.variables = [sum]
	var custom_start: int = custom_graph.add_node(FlowStartNode.new())
	var two := FlowExpressionNode.new()
	two.expression = "2"
	var three := FlowExpressionNode.new()
	three.expression = "3"
	var two_id: int = custom_graph.add_node(two)
	var three_id: int = custom_graph.add_node(three)
	var add_id: int = custom_graph.add_node(AddNode.new())
	custom_graph.connect_nodes(two_id, "result", add_id, "a")
	custom_graph.connect_nodes(three_id, "result", add_id, "b")
	var set_sum := FlowSetVariableNode.new()
	set_sum.variable_name = &"sum"
	var set_id: int = custom_graph.add_node(set_sum)
	custom_graph.connect_nodes(add_id, "sum", set_id, "value")
	var has_key := ItemCheckNode.new()
	has_key.item = &"key"
	var has_key_id: int = custom_graph.add_node(has_key)
	var wait_three_id: int = custom_graph.add_node(WaitSecondsNode.new())
	custom_graph.connect_nodes(custom_start, "out", set_id, "in")
	custom_graph.connect_nodes(set_id, "out", has_key_id, "in")
	custom_graph.connect_nodes(has_key_id, "yes", wait_three_id, "in")
	playback.set_variable("items", [&"key"])
	playback.start(custom_graph)
	print("Waiting at WaitSecondsNode: ", playback.get_current_node_id() == wait_three_id)
	playback.process(3.0)
	print("After 3 s, playing: ", playback.is_playing())

	# Save and restore
	playback.start(choice_graph)
	ResourceSaver.save(playback.get_state(), "user://flow_state.tres")
	playback.set_state(load("user://flow_state.tres"))
	print("Restored at the choice: ", playback.get_current_node_id() == choice_id)

	# Player processing modes
	player.callback_mode_process = FlowGraphPlayer.CALLBACK_MODE_PROCESS_MANUAL
	player.graph = graph
	player.play(wait_id)
	player.get_playback().process(2.0)
	print("Manual processing ended the wait, playing: ", player.is_playing())

	get_tree().quit()


func _on_event(event_name: StringName, arguments: Array) -> void:
	if event_name == &"say":
		label.text = arguments[0]


func _on_node_waiting(_node_id: int, _node: FlowNode) -> void:
	var playback: FlowGraphPlayback = player.get_playback()
	var exits: Array[FlowExit] = playback.get_available_exits()
	for i: int in exits.size():
		var button := Button.new()
		button.text = exits[i].label
		button.disabled = not exits[i].enabled
		button.pressed.connect(playback.choose.bind(i))
		menu.add_child(button)
