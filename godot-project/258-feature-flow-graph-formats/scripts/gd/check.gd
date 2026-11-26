extends FeatureCheck
## GDScript checks of 258-feature-flow-graph-formats. Check the same things as scripts/cs/Issue258FeatureFlowGraphFormatsCheck.cs.

const DIR := "res://258-feature-flow-graph-formats/"


func _get_check_name() -> String:
	return "258-feature-flow-graph-formats"


func _run_checks() -> void:
	for cls in ["FlowGraphMermaid", "FlowGraphMermaidOptions"]:
		expect(ClassDB.class_exists(cls), "ClassDB knows %s" % cls)
	_check_mermaid_round_trip()
	_check_options()
	_check_hand_written()
	_check_errors()
	_check_mmd_file()
	_check_text_formats()
	expect_equal(ProjectSettings.get_setting("flow_graph/export/convert_text_to_binary"), true, "export setting defaults to true")


# Start(0) -> script node(1) -> End(2); GetVariable(3) feeds "amount" of node 1; frame around 0 and 1.
func _make_graph() -> FlowGraph:
	var graph := FlowGraph.new()
	var start := graph.add_node(FlowStartNode.new())
	var custom_node := Issue255DoubleNode.new()
	custom_node.factor = 5
	custom_node.title_override = "Say \"hi\" #1"
	var custom := graph.add_node(custom_node, Vector2(200, 0))
	var end := graph.add_node(FlowEndNode.new(), Vector2(400, 0))
	var amount := graph.add_node(FlowGetVariableNode.new(), Vector2(0, 200))
	(graph.get_node(amount) as FlowGetVariableNode).variable_name = &"amount"
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


func _expect_same(graph: FlowGraph, loaded: FlowGraph, label: String) -> void:
	expect(loaded != null, "%s: loaded" % label)
	if loaded == null:
		return
	expect_equal(loaded.get_node_ids(), graph.get_node_ids(), "%s: node ids" % label)
	expect_equal(loaded.start_node_id, graph.start_node_id, "%s: start node" % label)
	expect_equal(loaded.get_connections().size(), graph.get_connections().size(), "%s: connection count" % label)
	expect(loaded.is_nodes_connected(0, &"out", 1, &"in"), "%s: flow connection" % label)
	expect(loaded.is_nodes_connected(3, &"value", 1, &"amount"), "%s: data connection" % label)
	var custom := loaded.get_node(1) as Issue255DoubleNode
	expect(custom != null, "%s: script node type kept" % label)
	if custom:
		expect_equal(custom.factor, 5, "%s: property kept" % label)
		expect_equal(custom.get_title(), "Say \"hi\" #1", "%s: title kept" % label)
		expect_equal(custom.editor_position, Vector2(200, 0), "%s: editor position kept" % label)
	expect_equal(loaded.variables.size(), 1, "%s: variables kept" % label)
	expect_equal(loaded.frames.size(), 1, "%s: frame kept" % label)
	if loaded.frames.size() == 1:
		expect_equal(loaded.frames[0].title, "Intro", "%s: frame title" % label)
		expect_equal(loaded.frames[0].node_ids, PackedInt32Array([0, 1]), "%s: frame nodes" % label)
	expect(loaded.validate().is_valid(), "%s: loaded graph is valid" % label)


func _check_mermaid_round_trip() -> void:
	var graph := _make_graph()
	var text := FlowGraphMermaid.serialize(graph)
	expect(text.begins_with("flowchart TD\n"), "Mermaid header")
	expect("n0 -->|out| n1" in text, "flow edge as Mermaid edge")
	expect("n3 -.->|value:amount| n1" in text, "data edge dotted")
	expect("subgraph f0" in text, "frame as subgraph")
	var loaded := FlowGraphMermaid.deserialize(text)
	_expect_same(graph, loaded, "Mermaid")
	if loaded:
		expect_equal(FlowGraphMermaid.serialize(loaded), text, "Mermaid: second round trip gives the same text")


func _check_options() -> void:
	var graph := _make_graph()
	var options := FlowGraphMermaidOptions.new()
	options.direction = FlowGraphMermaidOptions.DIRECTION_LEFT_RIGHT
	options.draw_data_connections = false
	options.include_editor_data = false
	var text := FlowGraphMermaid.serialize(graph, options)
	expect(text.begins_with("flowchart LR\n"), "options: direction")
	expect(not "-.->" in text and "%% flow:edge n3 value n1 amount" in text, "options: data edge as comment")
	var loaded := FlowGraphMermaid.deserialize(text)
	expect(loaded != null and loaded.is_nodes_connected(3, &"value", 1, &"amount"), "options: data edge kept")
	expect(loaded != null and loaded.get_node(1).editor_position == Vector2(), "options: editor data left out")
	options.include_data_comments = false
	text = FlowGraphMermaid.serialize(graph, options)
	expect(not "%%" in text, "options: plain Mermaid without comments")
	expect(FlowGraphMermaid.deserialize(text) != null, "options: plain Mermaid loads")


func _check_hand_written() -> void:
	var graph := load(DIR + "resources/door.mmd") as FlowGraph
	expect(graph != null, "hand-written .mmd loads as FlowGraph")
	if graph == null:
		return
	expect_equal(graph.get_node_ids(), PackedInt32Array([0, 1, 2, 3]), "hand-written: four nodes")
	expect(graph.get_node(0) is FlowStartNode, "hand-written: start")
	expect(graph.get_node(1) is FlowBranchNode, "hand-written: rhombus is a branch")
	expect(graph.get_node(2) is FlowEmitEventNode, "hand-written: step is an event node")
	expect(graph.get_node(3) is FlowEndNode, "hand-written: end")
	expect_equal(graph.get_node(1).get_title(), "Door open?", "hand-written: label is the title")
	expect(graph.is_nodes_connected(1, &"true", 2, &"in"), "hand-written: yes -> true")
	expect(graph.is_nodes_connected(1, &"false", 3, &"in"), "hand-written: no -> false")
	expect_equal(graph.frames.size(), 1, "hand-written: subgraph is a frame")
	expect_equal(graph.start_node_id, 0, "hand-written: start node id")


func _check_errors() -> void:
	var first := FlowGraphMermaid.new()
	var second := FlowGraphMermaid.new()
	expect_equal(first.parse("sequenceDiagram\n  A->>B: hi\n"), ERR_PARSE_ERROR, "errors: not a flowchart")
	expect_equal(second.parse("flowchart TD\n  A --> B\n  B -.-> C\n"), ERR_PARSE_ERROR, "errors: data edge without ports")
	expect_equal(first.get_error_line(), 1, "errors: line of the first instance")
	expect_equal(second.get_error_line(), 3, "errors: line of the second instance")
	expect(not second.get_error_message().is_empty(), "errors: message")
	expect(first.get_graph() == null, "errors: no graph")
	expect_equal(first.parse("flowchart TD\n  A --> B\n"), OK, "errors: next parse succeeds")
	expect_equal(first.get_error_line(), 0, "errors: cleared")
	expect_equal(second.get_error_line(), 3, "errors: other instance unchanged")


func _check_mmd_file() -> void:
	var graph := _make_graph()
	var path := "user://258_flow_graph.mmd"
	expect_equal(ResourceSaver.save(graph, path), OK, ".mmd: save")
	expect(FileAccess.get_file_as_string(path).begins_with("flowchart"), ".mmd: file is Mermaid text")
	_expect_same(graph, ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as FlowGraph, ".mmd")


func _check_text_formats() -> void:
	var graph := _make_graph()
	var formats := {"JSON": ResourceTextSerializer.FORMAT_JSON, "YAML": ResourceTextSerializer.FORMAT_YAML, "TOML": ResourceTextSerializer.FORMAT_TOML}
	for label: String in formats:
		var text := ResourceTextSerializer.serialize(graph, formats[label])
		expect(not text.is_empty(), "%s: serialize" % label)
		_expect_same(graph, ResourceTextSerializer.deserialize(text, formats[label]) as FlowGraph, label)
