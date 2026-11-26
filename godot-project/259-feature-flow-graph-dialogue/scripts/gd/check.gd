extends FeatureCheck
## GDScript checks of 259-feature-flow-graph-dialogue. Check the same things as scripts/cs/Issue259FeatureFlowGraphDialogueCheck.cs.

const GRAPH_PATH := "user://259_dialogue.tres"
const STATE_PATH := "user://259_dialogue_state.tres"


func _get_check_name() -> String:
	return "259-feature-flow-graph-dialogue"


func _run_checks() -> void:
	for cls in ["DialogueLineNode", "DialogueChoiceNode", "DialogueOption"]:
		expect(ClassDB.class_exists(cls), "ClassDB knows %s" % cls)
	_check_translation()
	_check_dialogue()
	_check_strings_and_state()
	await _check_dialogue_box()


func _option(port: StringName, text: String, condition := "", once := false) -> DialogueOption:
	var option := DialogueOption.new()
	option.port = port
	option.text = text
	option.condition = condition
	option.once = once
	return option


# Start(0) -> Line(1) -> Choice(2) [buy: gold >= 10 -> End "bought"(3), chat (once) -> News(4) -> Choice, bye -> End "bye"(5)].
# The news line is a script node type (Issue259MoodLineNode) with its own translatable string.
func _make_shop_graph() -> FlowGraph:
	var graph := FlowGraph.new()
	var gold := FlowVariable.new()
	gold.name = &"gold"
	gold.type = TYPE_INT
	gold.default_value = 5
	graph.variables = [gold]
	var start := graph.add_node(FlowStartNode.new())
	var hello := DialogueLineNode.new()
	hello.speaker = &"merchant"
	hello.speaker_name = "Merchant"
	hello.text = "DLG_259_HELLO"
	var hello_id := graph.add_node(hello)
	var choice := DialogueChoiceNode.new()
	choice.options = [_option(&"buy", "Buy a sword", "gold >= 10"), _option(&"chat", "Any news?", "", true), _option(&"", "Bye")]
	var choice_id := graph.add_node(choice)
	var bought := FlowEndNode.new()
	bought.end_name = &"bought"
	var bought_id := graph.add_node(bought)
	var news := Issue259MoodLineNode.new()
	news.speaker = &"merchant"
	news.text = "The king is coming, you have {gold} gold."
	news.mood = "worried"
	var news_id := graph.add_node(news)
	var bye := FlowEndNode.new()
	bye.end_name = &"bye"
	var bye_id := graph.add_node(bye)
	graph.connect_nodes(start, &"out", hello_id, &"in")
	graph.connect_nodes(hello_id, &"out", choice_id, &"in")
	graph.connect_nodes(choice_id, &"buy", bought_id, &"in")
	graph.connect_nodes(choice_id, &"chat", news_id, &"in")
	graph.connect_nodes(news_id, &"out", choice_id, &"in")
	graph.connect_nodes(choice_id, choice.get_option_port(2), bye_id, &"in")
	return graph


func _check_translation() -> void:
	var line := DialogueLineNode.new()
	line.text = "DLG_259_KEY"
	line.translation_context = &"shop"
	expect_equal(line.get_translated_text(), "DLG_259_KEY", "untranslated key")
	var translation := Translation.new()
	translation.locale = TranslationServer.get_locale()
	translation.add_message(&"DLG_259_KEY", &"Welcome!", &"shop")
	TranslationServer.add_translation(translation)
	expect_equal(line.get_translated_text(), "Welcome!", "translated through the context")
	TranslationServer.remove_translation(translation)
	line.speaker = &"hero"
	expect_equal(line.get_translated_speaker_name(), "hero", "speaker id when no speaker name")
	expect_equal(line.get_inline_properties(), PackedStringArray(["speaker", "text"]), "inline properties")
	expect_equal(line.get_title(), "Dialogue Line", "title")


func _check_dialogue() -> void:
	var graph := _make_shop_graph()
	var playback := FlowGraphPlayback.new()
	var finished := []
	playback.finished.connect(func(end_id: int): finished.append(end_id))
	expect_equal(playback.start(graph), OK, "start")
	expect(playback.is_waiting() and playback.get_current_node() is DialogueLineNode, "waits at the first line")
	expect_equal(playback.advance(), OK, "advance the line")
	expect_equal(playback.get_current_node_id(), 2, "at the choice")
	var exits := playback.get_available_exits()
	expect_equal(exits.size(), 3, "three options")
	if exits.size() == 3:
		expect(not exits[0].enabled, "buy is disabled: gold 5 < 10")
		expect_equal(exits[1].label, "Any news?", "option label")
		expect_equal(exits[1].node_id, 4, "chat leads to the news line")
		expect_equal(exits[2].port, &"option_2", "default port name")
	expect_equal(playback.choose(1), OK, "choose chat")
	expect_equal(playback.get_current_node_id(), 4, "news line")
	expect_equal(playback.advance(), OK, "advance the news")
	expect(not playback.get_available_exits()[1].enabled, "chat is taken once")
	playback.set_variable(&"gold", 20)
	expect(playback.get_available_exits()[0].enabled, "buy is enabled with 20 gold")
	expect_equal(playback.choose(0), OK, "choose buy")
	expect_equal(finished, [3], "finished at bought")


func _check_strings_and_state() -> void:
	expect_equal(ResourceSaver.save(_make_shop_graph(), GRAPH_PATH), OK, "save the graph as .tres")
	var graph := ResourceLoader.load(GRAPH_PATH, "", ResourceLoader.CACHE_MODE_REPLACE) as FlowGraph
	if not expect(graph != null, "load the graph"):
		return
	var ids := []
	for entry in graph.get_translatable_strings():
		ids.append(entry[0])
	for text in ["DLG_259_HELLO", "Merchant", "Buy a sword", "Any news?", "Bye", "The king is coming, you have {gold} gold.", "worried"]:
		expect(text in ids, "translatable string: %s" % text)

	var playback := FlowGraphPlayback.new()
	playback.start(graph)
	playback.advance()
	playback.choose(1)
	playback.advance()
	expect_equal(ResourceSaver.save(playback.get_state(), STATE_PATH), OK, "save the state")
	var state := ResourceLoader.load(STATE_PATH, "", ResourceLoader.CACHE_MODE_IGNORE) as FlowGraphState
	if not expect(state != null, "load the state"):
		return
	var restored := FlowGraphPlayback.new()
	var waiting := []
	restored.node_waiting.connect(func(id: int, _node: FlowNode): waiting.append(id))
	expect_equal(restored.set_state(state), OK, "restore the state")
	expect_equal(waiting, [2], "the choice is shown again")
	expect(not restored.get_available_exits()[1].enabled, "the taken option stays taken")
	expect_equal(restored.choose(2), OK, "choose bye after the restore")
	expect(not restored.is_playing(), "finished after the restore")


# The example dialogue box, driven by FlowGraphPlayer signals.
func _check_dialogue_box() -> void:
	var player := FlowGraphPlayer.new()
	player.graph = _make_shop_graph()
	add_child(player)
	var box := Issue259DialogueBox.new()
	add_child(box)
	box.attach(player)
	expect_equal(player.play(), OK, "play")
	expect_equal(box.speaker_label.text, "Merchant", "the box shows the speaker")
	expect_equal(box.text_label.text, "DLG_259_HELLO", "the box shows the line")
	box.continue_button.pressed.emit()
	expect_equal(box.choice_box.get_child_count(), 2, "two available options (buy is hidden)")
	(box.choice_box.get_child(0) as Button).pressed.emit()
	expect_equal(box.text_label.text, "The king is coming, you have 5 gold.", "variables formatted into the line")
	box.continue_button.pressed.emit()
	expect_equal(box.choice_box.get_child_count(), 1, "only bye is left")
	(box.choice_box.get_child(0) as Button).pressed.emit()
	expect(not player.is_playing(), "the dialogue finished")
	expect(not box.visible, "the box hides at the end")
	await get_tree().process_frame
	box.queue_free()
	player.queue_free()
