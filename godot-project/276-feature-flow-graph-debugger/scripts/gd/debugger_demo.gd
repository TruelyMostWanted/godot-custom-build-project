extends Node
## Manual check of 276-feature-flow-graph-debugger: open resources/debug_dialogue.tres in the FlowGraph dock,
## run this scene (F6) and watch the highlight move. Two players run the same graph, PlayerB starts 0.7 s later,
## so the dock shows a playback selector. Press "Follow" to follow a playback into the subgraph and back.


func _ready() -> void:
	for player: FlowGraphPlayer in [$PlayerA, $PlayerB]:
		player.finished.connect(func(_end_id: int): player.play())
	await get_tree().create_timer(0.7).timeout
	$PlayerB.play()
