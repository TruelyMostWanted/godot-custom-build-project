extends Node
## Minimal example of Flow Graph Formats: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Flow-Graph-Formats


func _ready() -> void:
	# Mermaid files (door.mmd is the hand-written flowchart of the guide)
	var graph: FlowGraph = load("res://flow-graph-formats/resources/door.mmd") as FlowGraph
	ResourceSaver.save(graph, "user://door_copy.mmd")
	var text: String = FlowGraphMermaid.serialize(graph)
	var copy: FlowGraph = FlowGraphMermaid.deserialize(text)
	print(text)
	print("Copy has nodes: ", copy.get_node_ids())

	# Options
	var options := FlowGraphMermaidOptions.new()
	options.direction = FlowGraphMermaidOptions.DIRECTION_LEFT_RIGHT
	options.draw_data_connections = false
	var left_right: String = FlowGraphMermaid.serialize(graph, options)
	var header: String = left_right.get_slice("\n", 0)
	print(header)

	# Errors
	var mermaid := FlowGraphMermaid.new()
	if mermaid.parse("flowchart TD\n  A -.-> B\n") != OK:
		print("Line %d: %s" % [mermaid.get_error_line(), mermaid.get_error_message()])

	# JSON, YAML and TOML
	var yaml: String = ResourceTextSerializer.serialize(graph, ResourceTextSerializer.FORMAT_YAML)
	var yaml_copy: FlowGraph = ResourceTextSerializer.deserialize(yaml, ResourceTextSerializer.FORMAT_YAML) as FlowGraph
	print("YAML copy has nodes: ", yaml_copy.get_node_ids())

	get_tree().quit()
