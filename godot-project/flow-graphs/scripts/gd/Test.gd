extends FeatureTest
## Full API test of Flow Graphs: FlowGraph, FlowNode, FlowPort, FlowConnection, FlowFrame, FlowVariable, FlowBlackboard,
## FlowGraphValidation and the built-in node types. Same tests as scripts/cs/Test.cs.

const DIR := "res://flow-graphs/resources/"
const STORAGE := ["node_ids", "nodes", "connections", "frames", "next_node_id"]
# Input and output port names of the built-in node types (FlowExpressionNode without input_names).
const PORTS := {
	"FlowStartNode": [[], ["out"]],
	"FlowEndNode": [["in"], []],
	"FlowBranchNode": [["in", "condition"], ["true", "false"]],
	"FlowExpressionNode": [[], ["result"]],
	"FlowGetVariableNode": [[], ["value"]],
	"FlowSetVariableNode": [["in", "value"], ["out"]],
	"FlowEmitEventNode": [["in"], ["out"]],
	"FlowSubgraphNode": [["in"], ["out"]],
	"FlowWaitNode": [["in"], ["out"]],
}
const CHAIN_LENGTH := 1000


func _run_tests() -> void:
	_test_add_and_remove()
	_test_built_in_ports()
	_test_built_in_properties()
	_test_connect()
	_test_read_connections()
	_test_port()
	_test_node()
	_test_custom_node()
	_test_variables()
	_test_blackboard()
	_test_frames()
	_test_storage_hidden()
	_test_validation()
	_test_save_load()
	_test_sample_resources()


# Start(0) -> Wait(1) -> Branch(5) -true-> End(2); GetVariable "gold"(3) -> Expression "gold >= 100"(4) -> condition of 5.
func _make_graph() -> FlowGraph:
	var graph := FlowGraph.new()
	var gold := FlowVariable.new()
	gold.name = &"gold"
	gold.type = TYPE_INT
	graph.variables = [gold]
	graph.add_node(FlowStartNode.new())
	graph.add_node(FlowWaitNode.new(), Vector2(200, 0))
	graph.add_node(FlowEndNode.new(), Vector2(400, 0))
	var get_gold := FlowGetVariableNode.new()
	get_gold.variable_name = &"gold"
	graph.add_node(get_gold)
	var check := FlowExpressionNode.new()
	check.input_names = PackedStringArray(["gold"])
	check.expression = "gold >= 100"
	check.result_type = TYPE_BOOL
	graph.add_node(check)
	graph.add_node(FlowBranchNode.new())
	graph.connect_nodes(0, &"out", 1, &"in")
	graph.connect_nodes(1, &"out", 5, &"in")
	graph.connect_nodes(5, &"true", 2, &"in")
	graph.connect_nodes(3, &"value", 4, &"gold")
	graph.connect_nodes(4, &"result", 5, &"condition")
	return graph


func _port_names(ports: Array[FlowPort]) -> Array:
	var names: Array = []
	for port: FlowPort in ports:
		names.append(String(port.name))
	return names


func _has_issue(validation: FlowGraphValidation, kind: FlowGraphValidation.IssueKind, node_id: int) -> bool:
	for i: int in validation.get_issue_count():
		if validation.get_issue_kind(i) == kind and validation.get_issue_node_id(i) == node_id:
			return true
	return false


func _test_add_and_remove() -> void:
	var graph := FlowGraph.new()
	var added: Array[int] = []
	var removed: Array[int] = []
	graph.node_added.connect(func(id: int) -> void: added.append(id))
	graph.node_removed.connect(func(id: int) -> void: removed.append(id))
	var end := FlowEndNode.new()
	var end_id: int = graph.add_node(end, Vector2(400, 0))
	var start_id: int = graph.add_node(FlowStartNode.new())
	expect_equal(graph.get_node_ids(), PackedInt32Array([0, 1]), "add_node returns the ids 0 and 1")
	expect_equal(added, [0, 1], "node_added is emitted with the id")
	expect_equal(graph.start_node_id, start_id, "the first FlowStartNode becomes the start")
	expect(graph.has_node(end_id), "has_node finds an added node")
	expect(graph.get_node(end_id) == end, "get_node returns the node")
	expect_equal(graph.find_node_id(end), end_id, "find_node_id returns the id")
	expect_equal(end.editor_position, Vector2(400, 0), "add_node stores the position in editor_position")
	var other := FlowGraph.new()
	expect_equal(other.add_node(end), FlowGraph.NODE_ID_NONE, "a node can't join a second graph")
	graph.remove_node(end_id)
	expect_equal(removed, [end_id], "node_removed is emitted with the id")
	expect(not graph.has_node(end_id), "remove_node removes the node")
	expect_equal(graph.find_node_id(end), FlowGraph.NODE_ID_NONE, "find_node_id of a removed node is NODE_ID_NONE")
	expect(other.add_node(end) != FlowGraph.NODE_ID_NONE, "a removed node can join another graph")
	expect_equal(graph.add_node(FlowEndNode.new()), 2, "ids are never reused")


func _test_built_in_ports() -> void:
	for type: String in PORTS:
		var node: FlowNode = ClassDB.instantiate(type)
		expect_equal(_port_names(node.get_input_ports()), PORTS[type][0], "%s: input ports" % type)
		expect_equal(_port_names(node.get_output_ports()), PORTS[type][1], "%s: output ports" % type)
	var condition: FlowPort = FlowBranchNode.new().find_input_port(&"condition")
	expect_equal(condition.type, TYPE_BOOL, "FlowBranchNode: condition is a bool")


func _test_built_in_properties() -> void:
	var expression := FlowExpressionNode.new()
	expression.input_names = PackedStringArray(["a", "b"])
	expression.result_type = TYPE_INT
	expression.expression = "a + b"
	expect_equal(_port_names(expression.get_input_ports()), ["a", "b"], "FlowExpressionNode: one input per input_names entry")
	expect_equal(expression.find_output_port(&"result").type, TYPE_INT, "FlowExpressionNode: result_type types the result")
	expect_equal(expression.expression, "a + b", "FlowExpressionNode: expression")
	var subgraph := FlowSubgraphNode.new()
	subgraph.graph = FlowGraph.new()
	subgraph.exit_names = PackedStringArray(["bought"])
	expect_equal(_port_names(subgraph.get_output_ports()), ["out", "bought"], "FlowSubgraphNode: one output per exit_names entry")
	expect(subgraph.graph != null, "FlowSubgraphNode: graph")
	var end := FlowEndNode.new()
	end.end_name = &"done"
	expect_equal(end.end_name, &"done", "FlowEndNode: end_name")
	var event := FlowEmitEventNode.new()
	event.event_name = &"say"
	event.arguments = ["Hello"]
	expect_equal(event.arguments, ["Hello"], "FlowEmitEventNode: arguments")
	var wait := FlowWaitNode.new()
	wait.seconds = 2.0
	expect_equal(wait.seconds, 2.0, "FlowWaitNode: seconds")
	var set_gold := FlowSetVariableNode.new()
	set_gold.variable_name = &"gold"
	expect_equal(set_gold.variable_name, &"gold", "FlowSetVariableNode: variable_name")


func _test_connect() -> void:
	var graph: FlowGraph = _make_graph()
	var changes: Array[int] = [0]
	graph.connections_changed.connect(func() -> void: changes[0] += 1)
	var lonely: int = graph.add_node(FlowWaitNode.new())
	expect(graph.can_connect_nodes(lonely, &"out", 2, &"in"), "can_connect_nodes: a free flow output into a flow input")
	expect_equal(graph.connect_nodes(lonely, &"out", 2, &"in"), OK, "connect_nodes returns OK")
	expect_equal(changes[0], 1, "connections_changed is emitted")
	expect(graph.is_nodes_connected(lonely, &"out", 2, &"in"), "is_nodes_connected after connect_nodes")
	graph.disconnect_nodes(lonely, &"out", 2, &"in")
	expect(not graph.is_nodes_connected(lonely, &"out", 2, &"in"), "disconnect_nodes removes the connection")
	expect_equal(changes[0], 2, "connections_changed is emitted on disconnect")
	expect_equal(graph.connect_nodes(lonely, &"out", 99, &"in"), ERR_DOES_NOT_EXIST, "unknown node: ERR_DOES_NOT_EXIST")
	expect_equal(graph.connect_nodes(lonely, &"nope", 2, &"in"), ERR_DOES_NOT_EXIST, "unknown port: ERR_DOES_NOT_EXIST")
	expect_equal(graph.connect_nodes(lonely, &"out", lonely, &"in"), ERR_INVALID_PARAMETER, "same node: ERR_INVALID_PARAMETER")
	expect_equal(graph.connect_nodes(3, &"value", 2, &"in"), ERR_INVALID_PARAMETER, "data into flow: ERR_INVALID_PARAMETER")
	var text := FlowExpressionNode.new()
	text.result_type = TYPE_STRING
	var text_id: int = graph.add_node(text)
	var branch: int = graph.add_node(FlowBranchNode.new())
	expect_equal(graph.connect_nodes(text_id, &"result", branch, &"condition"), ERR_INVALID_PARAMETER, "String into bool: ERR_INVALID_PARAMETER")
	expect_equal(graph.connect_nodes(0, &"out", 2, &"in"), ERR_ALREADY_IN_USE, "flow output used twice: ERR_ALREADY_IN_USE")
	expect_equal(graph.connect_nodes(3, &"value", 5, &"condition"), ERR_ALREADY_IN_USE, "data input used twice: ERR_ALREADY_IN_USE")
	expect(not graph.can_connect_nodes(0, &"out", 2, &"in"), "can_connect_nodes answers false for a used flow output")
	var any_type := FlowExpressionNode.new()
	var any_id: int = graph.add_node(any_type)
	expect_equal(graph.connect_nodes(any_id, &"result", branch, &"condition"), OK, "a Variant output fits a bool input")


func _test_read_connections() -> void:
	var graph: FlowGraph = _make_graph()
	expect_equal(graph.get_connections().size(), 5, "get_connections returns all connections")
	var outputs: Array[FlowConnection] = graph.get_output_connections(0)
	expect_equal(outputs.size(), 1, "get_output_connections of the start node")
	var flow: FlowConnection = outputs[0]
	expect_equal(flow.from_node_id, 0, "FlowConnection.from_node_id")
	expect_equal(flow.from_port, &"out", "FlowConnection.from_port")
	expect_equal(flow.to_node_id, 1, "FlowConnection.to_node_id")
	expect_equal(flow.to_port, &"in", "FlowConnection.to_port")
	expect_equal(flow.kind, FlowPort.PORT_KIND_FLOW, "FlowConnection.kind of a flow connection")
	var inputs: Array[FlowConnection] = graph.get_input_connections(4)
	expect_equal(inputs.size(), 1, "get_input_connections of the expression")
	expect_equal(inputs[0].kind, FlowPort.PORT_KIND_DATA, "FlowConnection.kind of a data connection")
	expect_equal(graph.find_flow_path(0, 2), PackedInt32Array([0, 1, 5, 2]), "find_flow_path returns the ids along the path")
	expect(graph.find_flow_path(2, 0).is_empty(), "find_flow_path without a path is empty")
	graph.remove_node(1)
	expect_equal(graph.get_connections().size(), 3, "remove_node removes the connections of the node")


func _test_port() -> void:
	var flow: FlowPort = FlowPort.create_flow(&"yes", "Yes")
	expect_equal(flow.kind, FlowPort.PORT_KIND_FLOW, "create_flow: kind")
	expect_equal(flow.name, &"yes", "create_flow: name")
	expect_equal(flow.label, "Yes", "create_flow: label")
	var data: FlowPort = FlowPort.create_data(&"amount", TYPE_INT, 1, "Amount")
	expect_equal(data.kind, FlowPort.PORT_KIND_DATA, "create_data: kind")
	expect_equal(data.type, TYPE_INT, "create_data: type")
	expect_equal(data.default_value, 1, "create_data: default_value")
	expect_equal(data.label, "Amount", "create_data: label")
	data.class_hint = &"Texture2D"
	expect_equal(data.class_hint, &"Texture2D", "class_hint")
	expect_equal(FlowPort.PORT_KIND_MAX, 2, "PORT_KIND_MAX")


func _test_node() -> void:
	var wait := FlowWaitNode.new()
	expect_equal(wait.get_title(), "Wait", "get_title of a built-in node: class name without Flow and Node")
	wait.title_override = "Pause"
	expect_equal(wait.get_title(), "Pause", "title_override replaces the title")
	expect_equal(wait.get_category(), "Flow", "get_category of a built-in node")
	expect_equal(wait.get_inline_properties(), PackedStringArray(["seconds"]), "get_inline_properties of a built-in node")
	expect(wait.get_configuration_warnings().is_empty(), "FlowWaitNode has no warnings")
	expect_equal(FlowEmitEventNode.new().get_configuration_warnings().size(), 1, "FlowEmitEventNode without event_name warns")
	expect(wait.get_translatable_strings().is_empty(), "FlowWaitNode has no translatable strings")
	wait.editor_comment = "Breathe"
	expect_equal(wait.editor_comment, "Breathe", "editor_comment")
	wait.editor_size = Vector2(120, 80)
	expect_equal(wait.editor_size, Vector2(120, 80), "editor_size")


func _test_custom_node() -> void:
	var node := HasItemNode.new()
	expect_equal(_port_names(node.get_input_ports()), ["in"], "_get_input_ports")
	expect_equal(_port_names(node.get_output_ports()), ["yes", "no"], "_get_output_ports")
	expect_equal(node.find_output_port(&"no").kind, FlowPort.PORT_KIND_FLOW, "find_output_port")
	expect(node.find_input_port(&"nope") == null, "find_input_port of an unknown port is null")
	expect_equal(node.get_title(), "Has Item", "get_title from the class name")
	expect_equal(node.get_category(), "Inventory", "_get_category")
	expect_equal(node.get_configuration_warnings(), PackedStringArray(["No item is set."]), "_get_configuration_warnings")
	var changes: Array[int] = [0]
	node.changed.connect(func() -> void: changes[0] += 1)
	node.item = &"key"
	expect_equal(changes[0], 1, "notify_ports_changed emits changed")
	expect(node.get_configuration_warnings().is_empty(), "no warning once the item is set")
	expect_equal(node.get_inline_properties(), PackedStringArray(["item"]), "_get_inline_properties")
	var strings: Array[PackedStringArray] = node.get_translatable_strings()
	expect_equal(strings, [PackedStringArray(["Do you have the key?"])], "_get_translatable_strings")
	var graph := FlowGraph.new()
	graph.add_node(node)
	expect_equal(graph.get_translatable_strings(), strings, "FlowGraph.get_translatable_strings collects the nodes")


func _test_variables() -> void:
	var gold := FlowVariable.new()
	gold.name = &"gold"
	gold.type = TYPE_INT
	gold.default_value = 50
	gold.description = "Coins of the player."
	expect_equal(gold.name, &"gold", "FlowVariable.name")
	expect_equal(gold.type, TYPE_INT, "FlowVariable.type")
	expect_equal(gold.default_value, 50, "FlowVariable.default_value")
	expect_equal(gold.description, "Coins of the player.", "FlowVariable.description")
	var graph := FlowGraph.new()
	graph.variables = [gold]
	expect(graph.variables[0] == gold, "FlowGraph.variables")


func _test_blackboard() -> void:
	var board := FlowBlackboard.new()
	var changed: Array = []
	board.variable_changed.connect(func(name: StringName, value: Variant) -> void: changed.append([name, value]))
	board.set_variable(&"gold", 10)
	expect_equal(changed, [[&"gold", 10]], "variable_changed is emitted with name and value")
	expect_equal(board.get_variable(&"gold"), 10, "get_variable")
	expect_equal(board.get_variable(&"missing", 7), 7, "get_variable returns the default of an unknown name")
	expect(board.has_variable(&"gold"), "has_variable")
	expect_equal(board.get_variable_names(), [&"gold"], "get_variable_names")
	expect_equal(board.variables, {&"gold": 10}, "variables")
	board.remove_variable(&"gold")
	expect(not board.has_variable(&"gold"), "remove_variable")


func _test_frames() -> void:
	var frame := FlowFrame.new()
	frame.title = "Shop"
	frame.color = Color.ORANGE
	frame.rect = Rect2(0, 0, 300, 200)
	frame.node_ids = PackedInt32Array([1, 2])
	var graph := FlowGraph.new()
	graph.frames = [frame]
	var stored: FlowFrame = graph.frames[0]
	expect_equal(stored.title, "Shop", "FlowFrame.title")
	expect_equal(stored.color, Color.ORANGE, "FlowFrame.color")
	expect_equal(stored.rect, Rect2(0, 0, 300, 200), "FlowFrame.rect")
	expect_equal(stored.node_ids, PackedInt32Array([1, 2]), "FlowFrame.node_ids")


func _test_storage_hidden() -> void:
	for property: Dictionary in FlowGraph.new().get_property_list():
		var usage: int = property.usage
		if property.name in STORAGE:
			expect(usage & PROPERTY_USAGE_STORAGE != 0, "%s is saved" % property.name)
			expect(usage & PROPERTY_USAGE_EDITOR == 0, "%s is hidden in the inspector" % property.name)
		elif property.name in ["variables", "start_node_id"]:
			expect(usage & PROPERTY_USAGE_EDITOR != 0, "%s is shown in the inspector" % property.name)
	var graph: FlowGraph = _make_graph()
	expect_equal(graph.next_node_id, 6, "next_node_id is the next free id")


func _test_validation() -> void:
	var graph: FlowGraph = _make_graph()
	var valid: FlowGraphValidation = graph.validate()
	expect(valid.is_valid(), "is_valid of a correct graph")
	expect_equal(valid.get_issue_count(), 0, "a valid graph has no issues")

	var no_start := FlowGraph.new()
	no_start.add_node(FlowEndNode.new())
	var missing: FlowGraphValidation = no_start.validate()
	expect(_has_issue(missing, FlowGraphValidation.ISSUE_KIND_MISSING_START, FlowGraph.NODE_ID_NONE), "ISSUE_KIND_MISSING_START")

	var lonely: int = graph.add_node(FlowWaitNode.new())
	var ghost := FlowGetVariableNode.new()
	ghost.variable_name = &"ghost"
	var ghost_id: int = graph.add_node(ghost)
	var silent: int = graph.add_node(FlowEmitEventNode.new())
	var first := FlowExpressionNode.new()
	first.input_names = PackedStringArray(["x"])
	var first_id: int = graph.add_node(first)
	var second := FlowExpressionNode.new()
	second.input_names = PackedStringArray(["y"])
	var second_id: int = graph.add_node(second)
	graph.connect_nodes(first_id, &"result", second_id, &"y")
	graph.connect_nodes(second_id, &"result", first_id, &"x")
	var text := FlowExpressionNode.new()
	text.expression = "\"text\""
	text.result_type = TYPE_STRING
	var text_id: int = graph.add_node(text)
	var branch: int = graph.add_node(FlowBranchNode.new())
	# connect_nodes refuses these, so write the storage directly, as a hand-edited file would.
	var storage: Array = graph.connections
	storage.append_array([text_id, &"result", branch, &"condition"])
	storage.append_array([0, &"out", 2, &"in"])
	storage.append_array([lonely, &"gone", 2, &"in"])
	graph.connections = storage
	var validation: FlowGraphValidation = graph.validate()
	expect(_has_issue(validation, FlowGraphValidation.ISSUE_KIND_UNREACHABLE_NODE, lonely), "ISSUE_KIND_UNREACHABLE_NODE")
	expect(_has_issue(validation, FlowGraphValidation.ISSUE_KIND_TYPE_MISMATCH, branch), "ISSUE_KIND_TYPE_MISMATCH")
	expect(_has_issue(validation, FlowGraphValidation.ISSUE_KIND_UNKNOWN_PORT, lonely), "ISSUE_KIND_UNKNOWN_PORT")
	expect(_has_issue(validation, FlowGraphValidation.ISSUE_KIND_MULTIPLE_FLOW_OUTPUTS, 0), "ISSUE_KIND_MULTIPLE_FLOW_OUTPUTS")
	expect(_has_issue(validation, FlowGraphValidation.ISSUE_KIND_UNDECLARED_VARIABLE, ghost_id), "ISSUE_KIND_UNDECLARED_VARIABLE")
	expect(_has_issue(validation, FlowGraphValidation.ISSUE_KIND_DATA_CYCLE, first_id), "ISSUE_KIND_DATA_CYCLE")
	expect(_has_issue(validation, FlowGraphValidation.ISSUE_KIND_NODE_WARNING, silent), "ISSUE_KIND_NODE_WARNING")
	expect_equal(FlowGraphValidation.ISSUE_KIND_MAX, 8, "ISSUE_KIND_MAX")
	expect_equal(validation.get_issues_for_node(ghost_id).size(), 2, "get_issues_for_node: undeclared and unreachable")
	expect(not validation.get_issue_message(0).is_empty(), "get_issue_message")


func _expect_same(graph: FlowGraph, loaded: FlowGraph, label: String) -> void:
	expect(loaded != null, "%s: loaded" % label)
	expect_equal(loaded.get_node_ids(), graph.get_node_ids(), "%s: node ids" % label)
	expect_equal(loaded.get_connections().size(), graph.get_connections().size(), "%s: connections" % label)
	expect_equal(loaded.next_node_id, graph.next_node_id, "%s: next_node_id" % label)


func _test_save_load() -> void:
	var graph: FlowGraph = _make_graph()
	graph.add_node(HasItemNode.new(), Vector2(10, 20))
	expect_equal(ResourceSaver.save(graph, "user://flow_graphs_test.tres"), OK, "save .tres")
	var from_tres := ResourceLoader.load("user://flow_graphs_test.tres", "", ResourceLoader.CACHE_MODE_IGNORE) as FlowGraph
	_expect_same(graph, from_tres, ".tres")
	expect(from_tres.get_node(6) is HasItemNode, ".tres: script node type kept")
	expect_equal(from_tres.get_node(6).editor_position, Vector2(10, 20), ".tres: editor_position kept")
	var json: String = ResourceTextSerializer.serialize(graph, ResourceTextSerializer.FORMAT_JSON)
	var from_json := ResourceTextSerializer.deserialize(json, ResourceTextSerializer.FORMAT_JSON) as FlowGraph
	_expect_same(graph, from_json, "JSON")
	var copy: FlowGraph = graph.duplicate(true)
	_expect_same(graph, copy, "duplicate(true)")
	expect(copy.get_node(0) != graph.get_node(0), "duplicate(true) copies the nodes")
	var shallow: FlowGraph = graph.duplicate()
	expect(shallow.get_node(0) == graph.get_node(0), "a shallow duplicate shares the node objects")

	var chain := FlowGraph.new()
	var previous: int = chain.add_node(FlowStartNode.new())
	for _i: int in CHAIN_LENGTH:
		var id: int = chain.add_node(FlowWaitNode.new())
		chain.connect_nodes(previous, &"out", id, &"in")
		previous = id
	expect_equal(ResourceSaver.save(chain, "user://flow_graphs_chain.res"), OK, "save .res")
	var begin: int = Time.get_ticks_usec()
	var from_res := ResourceLoader.load("user://flow_graphs_chain.res", "", ResourceLoader.CACHE_MODE_IGNORE) as FlowGraph
	var elapsed: float = (Time.get_ticks_usec() - begin) / 1000.0
	_expect_same(chain, from_res, ".res with %d connections" % CHAIN_LENGTH)
	print("  %d connections load from .res in %.2f ms" % [CHAIN_LENGTH, elapsed])


func _test_sample_resources() -> void:
	var graph := load(DIR + "flow_graph.tres") as FlowGraph
	expect(graph.validate().is_valid(), "flow_graph.tres is valid")
	expect(graph.get_node(1) is HasItemNode, "flow_graph.tres: GDScript node type")
	expect_equal(graph.get_node(2).get_category(), "Inventory", "flow_graph.tres: C# node type")
	expect_equal(graph.frames.size(), 1, "flow_graph.tres: one frame")
	var board := load(DIR + "flow_blackboard.tres") as FlowBlackboard
	expect_equal(board.get_variable(&"score"), 10, "flow_blackboard.tres: score")
	var port := load(DIR + "flow_port.tres") as FlowPort
	expect_equal(port.type, TYPE_INT, "flow_port.tres: type")
