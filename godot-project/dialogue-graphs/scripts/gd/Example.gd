extends Control
## Minimal example of Dialogue Graphs: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Dialogue-Graphs

@onready var player: FlowGraphPlayer = $FlowGraphPlayer
@onready var box: DialogueBox = $DialogueBox


func _ready() -> void:
	# Full example (with make_shop_graph() and the DialogueBox of "First dialogue in 10 steps")
	player.graph = make_shop_graph()
	box.attach(player)
	player.play()                                   # shows the first line
	player.get_playback().set_variable(&"gold", 20) # "Buy a sword" is available now
	print(box.speaker_label.text, ": ", box.text_label.text)

	# Lines (voice needs an audio file: left out)
	var line := DialogueLineNode.new()
	line.speaker = &"merchant"
	line.speaker_name = "Merchant"
	line.text = "Welcome, traveler!"
	print("Line speaker: ", line.get_translated_speaker_name())

	# Choices and options
	var buy := DialogueOption.new()
	buy.port = &"buy"
	buy.text = "Buy a sword"
	buy.condition = "gold >= 10"
	var bye := DialogueOption.new()
	bye.text = "Bye"
	var choice := DialogueChoiceNode.new()
	choice.options = [buy, bye]
	print("Ports: ", choice.get_option_port(0), ", ", choice.get_option_port(1))

	# Save and restore
	ResourceSaver.save(player.get_playback().get_state(), "user://dialogue_state.tres")
	player.get_playback().set_state(load("user://dialogue_state.tres"))
	print("Restored: ", box.text_label.text)

	# Localization (MoodLineNode: scripts/gd/MoodLineNode.gd)
	TranslationServer.set_locale("de")
	var playback: FlowGraphPlayback = player.get_playback()
	box.text_label.text = line.get_translated_text().format(playback.get_blackboard().get_variables())
	print("Translated: ", box.text_label.text)

	get_tree().quit()


func make_shop_graph() -> FlowGraph:
	var graph := FlowGraph.new()
	var gold := FlowVariable.new()
	gold.name = &"gold"
	gold.type = TYPE_INT
	gold.default_value = 5
	graph.variables = [gold]

	var start: int = graph.add_node(FlowStartNode.new())

	var hello := DialogueLineNode.new()
	hello.speaker = &"merchant"
	hello.speaker_name = "Merchant"
	hello.text = "Welcome, traveler!"
	var hello_id: int = graph.add_node(hello)

	var buy := DialogueOption.new()
	buy.port = &"buy"
	buy.text = "Buy a sword"
	buy.condition = "gold >= 10"
	var chat := DialogueOption.new()
	chat.port = &"chat"
	chat.text = "Any news?"
	chat.once = true
	var bye := DialogueOption.new()
	bye.text = "Bye"
	var choice := DialogueChoiceNode.new()
	choice.options = [buy, chat, bye]
	var choice_id: int = graph.add_node(choice)

	var bought := FlowEndNode.new()
	bought.end_name = &"bought"
	var bought_id: int = graph.add_node(bought)
	var left := FlowEndNode.new()
	left.end_name = &"bye"
	var left_id: int = graph.add_node(left)
	var news := DialogueLineNode.new()
	news.speaker = &"merchant"
	news.text = "The king is coming."
	var news_id: int = graph.add_node(news)

	graph.connect_nodes(start, &"out", hello_id, &"in")
	graph.connect_nodes(hello_id, &"out", choice_id, &"in")
	graph.connect_nodes(choice_id, &"buy", bought_id, &"in")
	graph.connect_nodes(choice_id, &"chat", news_id, &"in") # a node can't connect to itself: back through a line
	graph.connect_nodes(news_id, &"out", choice_id, &"in")
	graph.connect_nodes(choice_id, choice.get_option_port(2), left_id, &"in")
	return graph
