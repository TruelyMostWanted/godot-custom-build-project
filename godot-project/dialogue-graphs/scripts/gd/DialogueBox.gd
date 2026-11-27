class_name DialogueBox
extends VBoxContainer
## The dialogue box of the wiki guide (step 9): shows lines and choices of a FlowGraphPlayer through its signals.
## Used by scenes/dialogue_box_gd.tscn.

@onready var speaker_label: Label = $Speaker
@onready var text_label: Label = $Text
@onready var choice_box: VBoxContainer = $Choices
@onready var continue_button: Button = $Continue

var player: FlowGraphPlayer


func attach(p_player: FlowGraphPlayer) -> void:
	player = p_player
	player.node_waiting.connect(_on_node_waiting)
	player.finished.connect(func(_end_id: int): hide())
	continue_button.pressed.connect(func(): player.get_playback().advance())


func _on_node_waiting(_node_id: int, node: FlowNode) -> void:
	show()
	for child: Node in choice_box.get_children():
		child.queue_free()
	continue_button.visible = node is DialogueLineNode
	if node is DialogueLineNode:
		var line: DialogueLineNode = node
		speaker_label.text = line.get_translated_speaker_name()
		text_label.text = line.get_translated_text().format(player.get_playback().get_blackboard().get_variables())
	elif node is DialogueChoiceNode:
		var exits: Array[FlowExit] = player.get_playback().get_available_exits()
		for i: int in exits.size():
			if not exits[i].enabled:
				continue # or gray it out: button.disabled = true
			var button := Button.new()
			button.text = exits[i].label
			button.pressed.connect(func(): player.get_playback().choose(i))
			choice_box.add_child(button)
