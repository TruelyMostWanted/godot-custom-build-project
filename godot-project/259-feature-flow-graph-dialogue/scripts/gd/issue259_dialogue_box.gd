class_name Issue259DialogueBox
extends VBoxContainer
## Minimal dialogue box: shows the lines and choices of a FlowGraphPlayer through its signals.
## The engine has no dialogue UI node; a game builds its own like this one.

var player: FlowGraphPlayer
var speaker_label := Label.new()
var text_label := Label.new()
var choice_box := VBoxContainer.new()
var continue_button := Button.new()


func _init() -> void:
	add_child(speaker_label)
	add_child(text_label)
	add_child(choice_box)
	continue_button.text = "Continue"
	continue_button.pressed.connect(func(): player.get_playback().advance())
	add_child(continue_button)


func attach(p_player: FlowGraphPlayer) -> void:
	player = p_player
	player.node_waiting.connect(_on_node_waiting)
	player.finished.connect(func(_end_id: int): hide())


func _on_node_waiting(_node_id: int, node: FlowNode) -> void:
	show()
	for child in choice_box.get_children():
		choice_box.remove_child(child)
		child.queue_free()
	continue_button.visible = node is DialogueLineNode
	var line := node as DialogueLineNode
	if line:
		speaker_label.text = line.get_translated_speaker_name()
		text_label.text = line.get_translated_text().format(player.get_playback().get_blackboard().get_variables())
	elif node is DialogueChoiceNode:
		var exits := player.get_playback().get_available_exits()
		for i in exits.size():
			if not exits[i].enabled:
				continue # Hidden here; a game could gray it out instead.
			var button := Button.new()
			button.text = exits[i].label
			button.pressed.connect(func(): player.get_playback().choose(i))
			choice_box.add_child(button)
