extends Node
## Minimal example of Flow Graph Debugger. The guide's only snippet is an editor plugin (needs the editor): left out.
## Run this scene from the editor (F6) with resources/debug_dialogue.tres open in the FlowGraph dock to see the highlight.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Flow-Graph-Debugger

const GRAPH_PATH := "res://flow-graph-debugger/resources/debug_dialogue.tres"


func _ready() -> void:
	# Watch a running graph: a graph saved as its own file is shown in the dock.
	var graph: FlowGraph = load(GRAPH_PATH)
	var player_a := FlowGraphPlayer.new()
	player_a.graph = graph
	add_child(player_a)
	player_a.node_waiting.connect(_on_node_waiting.bind(player_a))
	player_a.play()

	# Several playbacks: the second one starts later, the dock shows a selector.
	await get_tree().create_timer(0.7).timeout
	var player_b := FlowGraphPlayer.new()
	player_b.graph = graph
	add_child(player_b)
	player_b.node_waiting.connect(_on_node_waiting.bind(player_b))
	player_b.play()

	await player_b.finished
	get_tree().quit()


# Follow into subgraphs: the current graph differs from the started graph inside a subgraph.
func _on_node_waiting(node_id: int, _node: FlowNode, player: FlowGraphPlayer) -> void:
	var playback: FlowGraphPlayback = player.get_playback()
	print("Playback %d waits at node %d in %s" % [playback.get_instance_id(), node_id, playback.get_current_graph().resource_path])
