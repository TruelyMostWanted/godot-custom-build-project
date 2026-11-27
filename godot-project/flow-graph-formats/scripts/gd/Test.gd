extends FeatureTest
## Full API test of Flow Graph Formats (FlowGraphMermaid, FlowGraphMermaidOptions, text formats). Same tests as scripts/cs/Test.cs.

const DOOR_PATH := "res://flow-graph-formats/resources/door.mmd"
const OPTIONS_PATH := "res://flow-graph-formats/resources/mermaid_options.tres"
const MMD_PATH := "user://flow_graph_formats.mmd"
const TITLE := "Say \"hi\" #1"
const HEADERS := {
	FlowGraphMermaidOptions.DIRECTION_TOP_DOWN: "flowchart TD\n",
	FlowGraphMermaidOptions.DIRECTION_LEFT_RIGHT: "flowchart LR\n",
	FlowGraphMermaidOptions.DIRECTION_BOTTOM_UP: "flowchart BT\n",
	FlowGraphMermaidOptions.DIRECTION_RIGHT_LEFT: "flowchart RL\n",
}


func _run_tests() -> void:
	_test_mermaid_round_trip()
	_test_option_defaults()
	_test_directions()
	_test_options()
	_test_hand_written()
	_test_errors()
	_test_mmd_file()
	_test_text_formats()
	_test_export_setting()


# Start(0) -> FormatsAmountNode(1) -> End(2); GetVariable(3) feeds "amount" of node 1; frame "Intro" around 0 and 1.
func _make_graph() -> FlowGraph:
	var graph := FlowGraph.new()
	var start: int = graph.add_node(FlowStartNode.new())
	var amount_node := FormatsAmountNode.new()
	amount_node.factor = 5
	amount_node.title_override = TITLE
	var custom: int = graph.add_node(amount_node, Vector2(200, 0))
	var end: int = graph.add_node(FlowEndNode.new(), Vector2(400, 0))
	var get_amount := FlowGetVariableNode.new()
	get_amount.variable_name = &"amount"
	var amount: int = graph.add_node(get_amount, Vector2(0, 200))
	var variable := FlowVariable.new()
	variable.name = &"amount"
	variable.type = TYPE_INT
	graph.variables = [variable]
	graph.connect_nodes(start, &"out", custom, &"in")
	graph.connect_nodes(custom, &"out", end, &"in")
	graph.connect_nodes(amount, &"value", custom, &"amount")
	var frame := FlowFrame.new()
	frame.title = "Intro"
	frame.node_ids = PackedInt32Array([start, custom])
	graph.frames = [frame]
	return graph


func _expect_same(loaded: FlowGraph, label: String) -> void:
	expect_equal(loaded.get_node_ids(), PackedInt32Array([0, 1, 2, 3]), "%s: node ids kept" % label)
	expect_equal(loaded.start_node_id, 0, "%s: start node kept" % label)
	expect_equal(loaded.get_connections().size(), 3, "%s: three connections" % label)
	expect(loaded.is_nodes_connected(0, &"out", 1, &"in"), "%s: flow connection kept" % label)
	expect(loaded.is_nodes_connected(3, &"value", 1, &"amount"), "%s: data connection kept" % label)
	var custom: FlowNode = loaded.get_node(1)
	expect(custom is FormatsAmountNode, "%s: script node type kept" % label)
	expect_equal(custom.get(&"factor"), 5, "%s: script property kept" % label)
	expect_equal(custom.get_title(), TITLE, "%s: title with quotes and # kept" % label)
	expect_equal(custom.editor_position, Vector2(200, 0), "%s: editor position kept" % label)
	expect_equal(loaded.variables.size(), 1, "%s: variables kept" % label)
	expect_equal(loaded.frames.size(), 1, "%s: frame kept" % label)
	expect_equal(loaded.frames[0].title, "Intro", "%s: frame title kept" % label)
	expect_equal(loaded.frames[0].node_ids, PackedInt32Array([0, 1]), "%s: frame nodes kept" % label)
	expect(loaded.validate().is_valid(), "%s: loaded graph is valid" % label)


func _test_mermaid_round_trip() -> void:
	var text: String = FlowGraphMermaid.serialize(_make_graph())
	expect(text.begins_with("flowchart TD\n"), "serialize writes a top-down flowchart by default")
	expect(text.contains("n0 -->|out| n1"), "a flow connection is a Mermaid edge")
	expect(text.contains("n3 -.->|value:amount| n1"), "a data connection is a dotted edge")
	expect(text.contains("subgraph f0"), "a frame is a subgraph")
	var loaded: FlowGraph = FlowGraphMermaid.deserialize(text)
	_expect_same(loaded, "Mermaid")
	expect_equal(FlowGraphMermaid.serialize(loaded), text, "a second round trip gives the same text")


func _test_option_defaults() -> void:
	var options := FlowGraphMermaidOptions.new()
	expect_equal(options.direction, FlowGraphMermaidOptions.DIRECTION_TOP_DOWN, "direction defaults to top-down")
	expect(options.include_data_comments, "include_data_comments defaults to true")
	expect(options.include_editor_data, "include_editor_data defaults to true")
	expect(options.draw_data_connections, "draw_data_connections defaults to true")
	expect_equal(FlowGraphMermaidOptions.DIRECTION_MAX, 4, "DIRECTION_MAX is the number of directions")
	var sample: FlowGraphMermaidOptions = load(OPTIONS_PATH)
	expect_equal(sample.direction, FlowGraphMermaidOptions.DIRECTION_LEFT_RIGHT, "the sample mermaid_options.tres loads")


func _test_directions() -> void:
	var graph: FlowGraph = _make_graph()
	var options := FlowGraphMermaidOptions.new()
	for direction: int in HEADERS:
		options.direction = direction
		var text: String = FlowGraphMermaid.serialize(graph, options)
		expect(text.begins_with(HEADERS[direction]), "direction %d writes %s" % [direction, HEADERS[direction].strip_edges()])


func _test_options() -> void:
	var graph: FlowGraph = _make_graph()
	var options := FlowGraphMermaidOptions.new()
	options.draw_data_connections = false
	options.include_editor_data = false
	var text: String = FlowGraphMermaid.serialize(graph, options)
	expect(not text.contains("-.->"), "draw_data_connections off: no dotted edge")
	expect(text.contains("%% flow:edge n3 value n1 amount"), "draw_data_connections off: data edge as a comment")
	var loaded: FlowGraph = FlowGraphMermaid.deserialize(text)
	expect(loaded.is_nodes_connected(3, &"value", 1, &"amount"), "the data edge comment loads as a connection")
	expect_equal(loaded.get_node(1).editor_position, Vector2(), "include_editor_data off: no positions")
	options.include_data_comments = false
	text = FlowGraphMermaid.serialize(graph, options)
	expect(not text.contains("%%"), "include_data_comments off: plain Mermaid without comments")
	expect(FlowGraphMermaid.deserialize(text) != null, "plain Mermaid loads")


func _test_hand_written() -> void:
	var graph: FlowGraph = load(DOOR_PATH) as FlowGraph
	expect_equal(graph.get_node_ids(), PackedInt32Array([0, 1, 2, 3]), "hand-written .mmd loads four nodes")
	expect(graph.get_node(0) is FlowStartNode, "no incoming edge: start node")
	expect(graph.get_node(1) is FlowBranchNode, "rhombus: branch node")
	expect(graph.get_node(2) is FlowEmitEventNode, "other node: emit event node")
	expect(graph.get_node(3) is FlowEndNode, "no outgoing edge: end node")
	expect_equal(graph.get_node(1).get_title(), "Door open?", "the label is the title")
	expect_equal((graph.get_node(2) as FlowEmitEventNode).event_name, &"Walk in", "the label is the event name")
	expect(graph.is_nodes_connected(1, &"true", 2, &"in"), "first branch edge (yes) leaves true")
	expect(graph.is_nodes_connected(1, &"false", 3, &"in"), "second branch edge (no) leaves false")
	expect_equal(graph.frames.size(), 1, "a subgraph is a frame")
	expect_equal(graph.start_node_id, 0, "the first start node is the start of the graph")


func _test_errors() -> void:
	var first := FlowGraphMermaid.new()
	var second := FlowGraphMermaid.new()
	expect_equal(first.parse("sequenceDiagram\n  A->>B: hi\n"), ERR_PARSE_ERROR, "parse fails on another diagram type")
	expect_equal(second.parse("flowchart TD\n  A --> B\n  B -.-> C\n"), ERR_PARSE_ERROR, "parse fails on a data edge without ports")
	expect_equal(first.get_error_line(), 1, "get_error_line of the first instance")
	expect_equal(second.get_error_line(), 3, "get_error_line of the second instance")
	expect(not second.get_error_message().is_empty(), "get_error_message is set")
	expect(first.get_graph() == null, "get_graph is null after an error")
	expect_equal(first.parse("flowchart TD\n  A --> B\n"), OK, "the next parse succeeds")
	expect(first.get_graph() != null, "get_graph returns the parsed graph")
	expect_equal(first.get_error_line(), 0, "a successful parse clears the error line")
	expect(first.get_error_message().is_empty(), "a successful parse clears the error message")
	expect_equal(second.get_error_line(), 3, "the other instance keeps its error")


func _test_mmd_file() -> void:
	expect_equal(ResourceSaver.save(_make_graph(), MMD_PATH), OK, "ResourceSaver saves .mmd")
	var text: String = FileAccess.get_file_as_string(MMD_PATH)
	expect(text.begins_with("flowchart"), "the .mmd file is Mermaid text")
	var loaded: FlowGraph = ResourceLoader.load(MMD_PATH, "", ResourceLoader.CACHE_MODE_IGNORE) as FlowGraph
	_expect_same(loaded, ".mmd")


func _test_text_formats() -> void:
	var graph: FlowGraph = _make_graph()
	for format: int in [ResourceTextSerializer.FORMAT_JSON, ResourceTextSerializer.FORMAT_YAML, ResourceTextSerializer.FORMAT_TOML]:
		var text: String = ResourceTextSerializer.serialize(graph, format)
		var loaded: FlowGraph = ResourceTextSerializer.deserialize(text, format) as FlowGraph
		_expect_same(loaded, "format %d" % format)


func _test_export_setting() -> void:
	var convert: bool = ProjectSettings.get_setting("flow_graph/export/convert_text_to_binary")
	expect(convert, "flow_graph/export/convert_text_to_binary defaults to true")
