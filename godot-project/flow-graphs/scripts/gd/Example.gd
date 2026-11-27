extends Node
## Minimal example of Flow Graphs: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Flow-Graphs


func _ready() -> void:
	# Build a graph in code
	var graph := FlowGraph.new()
	var start: int = graph.add_node(FlowStartNode.new(), Vector2(0, 0))
	var wait := FlowWaitNode.new()
	wait.seconds = 2.0
	var wait_id: int = graph.add_node(wait, Vector2(200, 0))
	var end: int = graph.add_node(FlowEndNode.new(), Vector2(400, 0))
	graph.connect_nodes(start, "out", wait_id, "in")
	graph.connect_nodes(wait_id, "out", end, "in")
	print("Valid: ", graph.validate().is_valid())

	# Ports and connections
	var a: int = graph.add_node(FlowWaitNode.new())
	var b: int = graph.add_node(FlowWaitNode.new())
	if graph.can_connect_nodes(a, "out", b, "in"):
		graph.connect_nodes(a, "out", b, "in")
	for connection: FlowConnection in graph.get_output_connections(a):
		print("Connection: ", connection.to_node_id, ".", connection.to_port)

	# Data connections and variables
	var gold := FlowVariable.new()
	gold.name = &"gold"
	gold.type = TYPE_INT
	gold.default_value = 50
	graph.variables = [gold]
	var get_gold := FlowGetVariableNode.new()
	get_gold.variable_name = &"gold"
	var get_id: int = graph.add_node(get_gold)
	var check := FlowExpressionNode.new()
	check.input_names = PackedStringArray(["gold"])
	check.expression = "gold >= 100"
	check.result_type = TYPE_BOOL
	var check_id: int = graph.add_node(check)
	graph.connect_nodes(get_id, "value", check_id, "gold")
	var branch_id: int = graph.add_node(FlowBranchNode.new())
	graph.connect_nodes(check_id, "result", branch_id, "condition")
	print("Data inputs of the branch: ", graph.get_input_connections(branch_id).size())

	# Write your own node type (scripts/gd/has_item_node.gd)
	var has_item := HasItemNode.new()
	print("HasItemNode warnings: ", has_item.get_configuration_warnings())

	# Validate a graph
	var validation: FlowGraphValidation = graph.validate()
	for i: int in validation.get_issue_count():
		var kind: FlowGraphValidation.IssueKind = validation.get_issue_kind(i)
		print(kind == FlowGraphValidation.ISSUE_KIND_UNREACHABLE_NODE, " ", validation.get_issue_message(i))

	# Save, load and copy (user:// instead of res://graphs/)
	ResourceSaver.save(graph, "user://intro.tres")
	var loaded := load("user://intro.tres") as FlowGraph
	var json: String = ResourceTextSerializer.serialize(graph, ResourceTextSerializer.FORMAT_JSON)
	var from_json := ResourceTextSerializer.deserialize(json, ResourceTextSerializer.FORMAT_JSON) as FlowGraph
	var copy: FlowGraph = graph.duplicate(true)
	print("Nodes loaded: ", loaded.get_node_ids().size())
	print("Nodes from JSON: ", from_json.get_node_ids().size())
	print("Nodes of the copy: ", copy.get_node_ids().size())

	get_tree().quit()
