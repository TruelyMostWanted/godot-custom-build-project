extends FeatureCheck
## GDScript checks of 255-feature-flow-graph. Check the same things as scripts/cs/Issue255FeatureFlowGraphCheck.cs.

const DIR := "res://255-feature-flow-graph/"
const STORAGE := ["node_ids", "nodes", "connections", "frames", "next_node_id"]


func _get_check_name() -> String:
	return "255-feature-flow-graph"


func _run_checks() -> void:
	for cls in ["FlowGraph", "FlowNode", "FlowPort", "FlowConnection", "FlowFrame", "FlowVariable", "FlowBlackboard", "FlowGraphValidation",
			"FlowStartNode", "FlowEndNode", "FlowBranchNode", "FlowExpressionNode", "FlowGetVariableNode", "FlowSetVariableNode",
			"FlowEmitEventNode", "FlowSubgraphNode", "FlowWaitNode"]:
		expect(ClassDB.class_exists(cls), "ClassDB knows %s" % cls)
	_check_script_node()
	_check_graph_api()
	_check_two_graphs()
	_check_storage_hidden()
	_check_validation()
	_check_tres_round_trip()
	_check_json_round_trip()
	_check_thousand_connections()
	_check_example()


# Start(0) -> script node(1) -> End(2); GetVariable(3) feeds the data input "amount" of node 1.
func _make_graph() -> FlowGraph:
	var graph := FlowGraph.new()
	var start := graph.add_node(FlowStartNode.new())
	var custom := graph.add_node(Issue255DoubleNode.new(), Vector2(200, 0))
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
	return graph


func _check_script_node() -> void:
	var node := Issue255DoubleNode.new()
	expect_equal(node.get_input_ports().size(), 2, "script node: two input ports")
	var amount := node.find_input_port(&"amount")
	expect(amount != null, "script node: find_input_port(amount)")
	if amount:
		expect_equal(amount.kind, FlowPort.PORT_KIND_DATA, "script node: amount is a data port")
		expect_equal(amount.type, TYPE_INT, "script node: amount is an int")
		expect_equal(amount.default_value, 1, "script node: amount default 1")
	expect_equal(node.find_output_port(&"out").kind, FlowPort.PORT_KIND_FLOW, "script node: out is a flow port")
	expect("Double" in node.get_title(), "script node: title from class_name")
	expect_equal(node.get_category(), "Checks", "script node: category override")
	expect_equal(node.get_inline_properties(), PackedStringArray(["factor"]), "script node: inline properties")


func _check_graph_api() -> void:
	var graph := _make_graph()
	expect_equal(graph.get_node_ids(), PackedInt32Array([0, 1, 2, 3]), "ids 0..3")
	expect_equal(graph.start_node_id, 0, "the first start node is the start")
	expect_equal(graph.find_node_id(graph.get_node(1)), 1, "find_node_id")
	expect(graph.is_nodes_connected(0, &"out", 1, &"in"), "is_nodes_connected")
	expect_equal(graph.get_connections().size(), 3, "three connections")
	expect_equal(graph.get_input_connections(1).size(), 2, "two inputs of the script node")
	var data: FlowConnection = graph.get_input_connections(1)[1]
	expect_equal(data.kind, FlowPort.PORT_KIND_DATA, "connection kind data")
	expect_equal(graph.connect_nodes(0, &"out", 2, &"in"), ERR_ALREADY_IN_USE, "flow output can't be connected twice")
	expect_equal(graph.connect_nodes(3, &"nope", 1, &"in"), ERR_DOES_NOT_EXIST, "unknown port")
	expect(not graph.can_connect_nodes(1, &"doubled", 2, &"in"), "data can't go into flow")
	expect_equal(graph.find_flow_path(0, 2), PackedInt32Array([0, 1, 2]), "find_flow_path")
	expect(graph.validate().is_valid(), "the graph is valid")
	graph.remove_node(1)
	expect_equal(graph.get_connections().size(), 0, "remove_node removes its connections")
	expect_equal(graph.add_node(FlowWaitNode.new()), 4, "ids are never reused")


func _check_two_graphs() -> void:
	var first := FlowGraph.new()
	var second := FlowGraph.new()
	var node := FlowEndNode.new()
	var id := first.add_node(node)
	expect(id != FlowGraph.NODE_ID_NONE, "add to the first graph")
	expect_equal(second.add_node(node), FlowGraph.NODE_ID_NONE, "a node can't join a second graph")
	expect(second.get_node_ids().is_empty(), "the second graph stays empty")
	first.remove_node(id)
	expect(second.add_node(node) != FlowGraph.NODE_ID_NONE, "after remove_node the node is free again")


func _check_storage_hidden() -> void:
	var found := 0
	for property in FlowGraph.new().get_property_list():
		if property.name in STORAGE:
			found += 1
			expect(property.usage & PROPERTY_USAGE_STORAGE != 0, "%s is stored" % property.name)
			expect(not (property.usage & PROPERTY_USAGE_EDITOR), "%s is hidden in the inspector" % property.name)
		elif property.name in ["variables", "start_node_id"]:
			expect(property.usage & PROPERTY_USAGE_EDITOR != 0, "%s is shown in the inspector" % property.name)
	expect_equal(found, STORAGE.size(), "all storage properties found")


func _has_issue(validation: FlowGraphValidation, kind: FlowGraphValidation.IssueKind, node_id: int) -> bool:
	for i in validation.get_issue_count():
		if validation.get_issue_kind(i) == kind and validation.get_issue_node_id(i) == node_id:
			return true
	return false


func _check_validation() -> void:
	var graph := FlowGraph.new()
	graph.add_node(FlowEndNode.new())
	var validation := graph.validate()
	expect(not validation.is_valid(), "a graph without start is invalid")
	expect(_has_issue(validation, FlowGraphValidation.ISSUE_KIND_MISSING_START, FlowGraph.NODE_ID_NONE), "missing start reported")

	graph = _make_graph()
	var lonely := graph.add_node(FlowWaitNode.new())
	var expression := FlowExpressionNode.new()
	expression.expression = "\"text\""
	expression.result_type = TYPE_STRING
	var expression_id := graph.add_node(expression)
	# connect_nodes refuses String -> int, so write the storage directly, as a hand-edited file would.
	var storage: Array = graph.get("connections")
	storage.append_array([expression_id, &"result", 1, &"amount"])
	graph.set("connections", storage)
	validation = graph.validate()
	expect(_has_issue(validation, FlowGraphValidation.ISSUE_KIND_UNREACHABLE_NODE, lonely), "unreachable node reported")
	expect(_has_issue(validation, FlowGraphValidation.ISSUE_KIND_TYPE_MISMATCH, 1), "type mismatch String -> int reported")
	expect_equal(validation.get_issues_for_node(lonely).size(), 1, "get_issues_for_node")
	expect(not validation.get_issue_message(0).is_empty(), "issue message")


func _check_tres_round_trip() -> void:
	var graph := _make_graph()
	var path := "user://check_255_graph.tres"
	expect_equal(ResourceSaver.save(graph, path), OK, "save .tres")
	var loaded := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as FlowGraph
	expect(loaded != null, "load .tres")
	if loaded:
		_expect_same(graph, loaded, ".tres")


func _check_json_round_trip() -> void:
	var graph := _make_graph()
	var text := ResourceTextSerializer.serialize(graph, ResourceTextSerializer.FORMAT_JSON)
	expect(not text.is_empty(), "serialize JSON")
	var loaded := ResourceTextSerializer.deserialize(text, ResourceTextSerializer.FORMAT_JSON) as FlowGraph
	expect(loaded != null, "deserialize JSON")
	if loaded:
		_expect_same(graph, loaded, "JSON")


func _expect_same(graph: FlowGraph, loaded: FlowGraph, label: String) -> void:
	expect_equal(loaded.get_node_ids(), graph.get_node_ids(), "%s: node ids" % label)
	expect_equal(loaded.get_connections().size(), graph.get_connections().size(), "%s: connection count" % label)
	expect(loaded.is_nodes_connected(3, &"value", 1, &"amount"), "%s: data connection" % label)
	var custom := loaded.get_node(1)
	expect(custom is Issue255DoubleNode, "%s: script node type kept" % label)
	var amount := custom.find_input_port(&"amount")
	expect(amount != null and amount.type == TYPE_INT, "%s: script ports kept" % label)
	expect_equal(custom.editor_position, Vector2(200, 0), "%s: editor position kept" % label)
	expect(loaded.validate().is_valid(), "%s: loaded graph is valid" % label)


func _check_thousand_connections() -> void:
	var graph := FlowGraph.new()
	var previous := graph.add_node(FlowStartNode.new())
	for i in 1000:
		var id := graph.add_node(FlowWaitNode.new(), Vector2(i * 10, 0))
		graph.connect_nodes(previous, &"out", id, &"in")
		previous = id
	for ext in ["tres", "res"]:
		var path := "user://check_255_thousand.%s" % ext
		expect_equal(ResourceSaver.save(graph, path), OK, "save 1000 connections (.%s)" % ext)
		var begin := Time.get_ticks_usec()
		var loaded := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as FlowGraph
		var load_ms := (Time.get_ticks_usec() - begin) / 1000.0
		expect(loaded != null and loaded.get_connections().size() == 1000, "load 1000 connections (.%s)" % ext)
		print("255: graph with 1001 nodes and 1000 connections loads from .%s in %.2f ms" % [ext, load_ms])
	var text := ResourceTextSerializer.serialize(graph, ResourceTextSerializer.FORMAT_JSON)
	var begin_json := Time.get_ticks_usec()
	var from_json := ResourceTextSerializer.deserialize(text, ResourceTextSerializer.FORMAT_JSON) as FlowGraph
	print("255: the same graph loads from JSON in %.2f ms" % ((Time.get_ticks_usec() - begin_json) / 1000.0))
	expect(from_json != null and from_json.get_connections().size() == 1000, "load 1000 connections (JSON)")


func _check_example() -> void:
	var graph := load(DIR + "resources/example_flow_graph.tres") as FlowGraph
	expect(graph != null, "example graph loads")
	if graph == null:
		return
	expect(graph.validate().is_valid(), "example graph is valid")
	expect(graph.get_node(1) is Issue255DoubleNode, "example: GDScript node type")
	expect_equal(graph.get_node(2).get_title(), "C# Node", "example: C# node type title")
	expect(graph.get_node(2).find_input_port(&"text") != null, "example: C# node ports")
	expect_equal(graph.get_frames().size(), 1, "example: one frame")
	var board := load(DIR + "resources/example_flow_blackboard.tres") as FlowBlackboard
	expect(board != null and board.get_variable(&"score") == 10, "example blackboard")
	var port := load(DIR + "resources/example_flow_port.tres") as FlowPort
	expect(port != null and port.type == TYPE_INT, "example port")
