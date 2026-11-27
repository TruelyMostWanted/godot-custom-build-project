extends FeatureTest
## Full API test of Dialogue Graphs (DialogueLineNode, DialogueChoiceNode, DialogueOption). Same tests as scripts/cs/Test.cs.

const BOX_SCENE := "res://dialogue-graphs/scenes/dialogue_box_gd.tscn"
const SAMPLE_PATH := "res://dialogue-graphs/resources/shop.tres"
const GRAPH_PATH := "user://dialogue_graphs_shop.tres"
const STATE_PATH := "user://dialogue_graphs_state.tres"
const NEWS_TEXT := "The king is coming, you have {gold} gold."


func _run_tests() -> void:
	_test_line_node()
	_test_option()
	_test_choice_node()
	_test_translation()
	_test_conditions_and_once()
	_test_translatable_strings()
	_test_save_and_restore()
	_test_sample_graph()
	await _test_dialogue_box()


# Start(0) -> Line(1) -> Choice(2) [buy: gold >= 10 -> End "bought"(3), chat (once) -> MoodLineNode(4) -> Choice, bye -> End "bye"(5)].
func _make_shop_graph() -> FlowGraph:
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
	var news := MoodLineNode.new()
	news.speaker = &"merchant"
	news.text = NEWS_TEXT
	news.mood = "worried"
	var news_id: int = graph.add_node(news)
	var left := FlowEndNode.new()
	left.end_name = &"bye"
	var left_id: int = graph.add_node(left)
	graph.connect_nodes(start, &"out", hello_id, &"in")
	graph.connect_nodes(hello_id, &"out", choice_id, &"in")
	graph.connect_nodes(choice_id, &"buy", bought_id, &"in")
	graph.connect_nodes(choice_id, &"chat", news_id, &"in")
	graph.connect_nodes(news_id, &"out", choice_id, &"in")
	graph.connect_nodes(choice_id, choice.get_option_port(2), left_id, &"in")
	return graph


func _test_line_node() -> void:
	var line := DialogueLineNode.new()
	expect_equal(line.get_title(), "Dialogue Line", "the line title")
	expect_equal(line.get_inline_properties(), PackedStringArray(["speaker", "text"]), "speaker and text are inline properties")
	line.speaker = &"hero"
	expect_equal(line.get_translated_speaker_name(), "hero", "get_translated_speaker_name falls back to speaker")
	line.speaker_name = "Hero"
	expect_equal(line.get_translated_speaker_name(), "Hero", "get_translated_speaker_name returns speaker_name")
	var voice := AudioStreamWAV.new()
	line.voice = voice
	expect(line.voice == voice, "voice keeps the audio stream")
	var subtitle := Subtitle.new()
	line.subtitle = subtitle
	expect(line.subtitle == subtitle, "subtitle keeps the subtitle")


func _test_option() -> void:
	var option := DialogueOption.new()
	expect_equal(option.port, &"", "port defaults to empty")
	expect_equal(option.text, "", "text defaults to empty")
	expect_equal(option.condition, "", "condition defaults to empty")
	expect(not option.once, "once defaults to false")


func _test_choice_node() -> void:
	var named := DialogueOption.new()
	named.port = &"buy"
	var choice := DialogueChoiceNode.new()
	choice.options = [named, DialogueOption.new()]
	expect_equal(choice.get_option_port(0), &"buy", "get_option_port returns the port of a named option")
	expect_equal(choice.get_option_port(1), &"option_1", "get_option_port returns option_<index> for an unnamed option")
	var ports: Array[StringName] = []
	for port: FlowPort in choice.get_output_ports():
		ports.append(port.name)
	expect_equal(ports, [&"buy", &"option_1"], "one flow output per option")


func _test_translation() -> void:
	var locale: String = TranslationServer.get_locale()
	var catalog := TextLocalizationCatalog.new()
	catalog.set_text(&"DLG_HELLO", locale, "Welcome!", &"shop")
	catalog.set_text(&"DLG_BYE", locale, "Goodbye!", &"shop")
	TextLocalizationServer.add_catalog(catalog, 100)
	var line := DialogueLineNode.new()
	line.text = "DLG_HELLO"
	expect_equal(line.get_translated_text(), "DLG_HELLO", "without context the key stays")
	line.translation_context = &"shop"
	expect_equal(line.get_translated_text(), "Welcome!", "get_translated_text uses translation_context")
	var bye := DialogueOption.new()
	bye.text = "DLG_BYE"
	var choice := DialogueChoiceNode.new()
	choice.options = [bye]
	choice.translation_context = &"shop"
	var graph := FlowGraph.new()
	var start: int = graph.add_node(FlowStartNode.new())
	var choice_id: int = graph.add_node(choice)
	graph.connect_nodes(start, &"out", choice_id, &"in")
	var playback := FlowGraphPlayback.new()
	playback.start(graph)
	var exits: Array[FlowExit] = playback.get_available_exits()
	expect_equal(exits[0].label, "Goodbye!", "option labels are translated with the choice context")
	TextLocalizationServer.remove_catalog(catalog)


func _test_conditions_and_once() -> void:
	var playback := FlowGraphPlayback.new()
	var finished: Array[int] = []
	playback.finished.connect(func(end_id: int): finished.append(end_id))
	expect_equal(playback.start(_make_shop_graph()), OK, "start the shop dialogue")
	expect(playback.get_current_node() is DialogueLineNode, "waits at the first line")
	expect_equal(playback.advance(), OK, "advance leaves a line")
	var exits: Array[FlowExit] = playback.get_available_exits()
	expect_equal(exits.size(), 3, "one exit per option")
	expect(not exits[0].enabled, "condition gold >= 10 is false with 5 gold")
	expect_equal(exits[1].label, "Any news?", "the exit label is the option text")
	expect_equal(exits[1].node_id, 4, "the exit leads to the connected node")
	expect_equal(exits[2].port, &"option_2", "the exit port of an unnamed option")
	expect_equal(playback.choose(0), ERR_UNAVAILABLE, "choose on a disabled option fails")
	expect_equal(playback.choose(1), OK, "choose an enabled option")
	expect_equal(playback.get_current_node_id(), 4, "the chosen option leads to the news line")
	playback.advance()
	exits = playback.get_available_exits()
	expect(not exits[1].enabled, "a once option is disabled after it was taken")
	playback.set_variable(&"gold", 20)
	exits = playback.get_available_exits()
	expect(exits[0].enabled, "the condition sees the new variable value")
	expect_equal(playback.choose(0), OK, "choose buy")
	expect_equal(finished, [3], "the dialogue finishes at the bought end")


func _test_translatable_strings() -> void:
	var ids: Array[String] = []
	for entry: PackedStringArray in _make_shop_graph().get_translatable_strings():
		ids.append(entry[0])
	for text: String in ["Welcome, traveler!", "Merchant", "Buy a sword", "Any news?", "Bye", NEWS_TEXT, "worried"]:
		expect(text in ids, "translatable string: %s" % text)


func _test_save_and_restore() -> void:
	expect_equal(ResourceSaver.save(_make_shop_graph(), GRAPH_PATH), OK, "save the graph as .tres")
	var graph: FlowGraph = ResourceLoader.load(GRAPH_PATH, "", ResourceLoader.CACHE_MODE_REPLACE) as FlowGraph
	var playback := FlowGraphPlayback.new()
	playback.start(graph)
	playback.advance()
	playback.choose(1)
	playback.advance()
	expect_equal(ResourceSaver.save(playback.get_state(), STATE_PATH), OK, "save the state")
	var state: FlowGraphState = ResourceLoader.load(STATE_PATH, "", ResourceLoader.CACHE_MODE_IGNORE) as FlowGraphState
	var restored := FlowGraphPlayback.new()
	var waiting: Array[int] = []
	restored.node_waiting.connect(func(id: int, _node: FlowNode): waiting.append(id))
	expect_equal(restored.set_state(state), OK, "set_state restores the dialogue")
	expect_equal(waiting, [2], "set_state emits node_waiting for the choice again")
	var exits: Array[FlowExit] = restored.get_available_exits()
	expect(not exits[1].enabled, "a taken once option stays taken after the restore")
	expect_equal(restored.choose(2), OK, "choose bye after the restore")
	expect(not restored.is_playing(), "the restored dialogue finishes")


func _test_sample_graph() -> void:
	var graph: FlowGraph = load(SAMPLE_PATH)
	expect(graph.validate().is_valid(), "the sample shop.tres is a valid graph")
	var choice: DialogueChoiceNode = graph.get_node(2)
	expect_equal(choice.options.size(), 3, "the sample choice has three options")


func _test_dialogue_box() -> void:
	var player := FlowGraphPlayer.new()
	player.graph = _make_shop_graph()
	add_child(player)
	var box_scene: PackedScene = load(BOX_SCENE)
	var box: DialogueBox = box_scene.instantiate()
	add_child(box)
	box.attach(player)
	expect_equal(player.play(), OK, "play")
	expect_equal(box.speaker_label.text, "Merchant", "the box shows the speaker")
	expect_equal(box.text_label.text, "Welcome, traveler!", "the box shows the line")
	box.continue_button.pressed.emit()
	await get_tree().process_frame
	expect_equal(box.choice_box.get_child_count(), 2, "the box shows the two enabled options")
	var chat_button: Button = box.choice_box.get_child(0)
	chat_button.pressed.emit()
	expect_equal(box.text_label.text, "The king is coming, you have 5 gold.", "the box formats variables into the line")
	box.continue_button.pressed.emit()
	await get_tree().process_frame
	expect_equal(box.choice_box.get_child_count(), 1, "only bye is left")
	var bye_button: Button = box.choice_box.get_child(0)
	bye_button.pressed.emit()
	expect(not player.is_playing(), "the dialogue finished")
	expect(not box.visible, "the box hides at the end")
	box.queue_free()
	player.queue_free()
