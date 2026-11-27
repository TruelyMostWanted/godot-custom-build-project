extends FeatureTest
## Full API test of Localized Text (LocalizedText): every method, property, signal and enum. Same tests as scripts/cs/Test.cs.

const PLURAL_RULE := "nplurals=2; plural=(n != 1);"

var _catalog := TextLocalizationCatalog.new()
var _ticks: int = 0
var _changed_texts: Array[String] = []


func _run_tests() -> void:
	var previous_locale: String = TranslationServer.get_locale()
	_fill_catalog()
	TextLocalizationServer.add_catalog(_catalog, 100)
	TranslationServer.set_locale("en")
	TextInsertionServer.set_value(&"player_name", "Ada")
	_test_defaults()
	_test_get_text()
	_test_plural()
	_test_channel()
	await _test_bind()
	await _test_metadata_binding()
	await _test_refresh_mode()
	TextInsertionServer.remove_value(&"player_name")
	TextInsertionServer.remove_value(&"enemy.count")
	TextLocalizationServer.remove_catalog(_catalog)
	TranslationServer.set_locale(previous_locale)


func _fill_catalog() -> void:
	_catalog.set_text(&"NPC_GREETING", "en", "Hello {player_name}!")
	_catalog.set_text(&"NPC_GREETING", "de", "Hallo {player_name}!")
	_catalog.set_text(&"NPC_GREETING", "de", "Na, {player_name}?", &"casual")
	_catalog.set_plural_rule("en", PLURAL_RULE)
	_catalog.set_plural_rule("de", PLURAL_RULE)
	_catalog.set_plural_texts(&"ENEMIES_LEFT", "en", PackedStringArray(["{enemy.count} enemy left", "{enemy.count} enemies left"]))
	_catalog.set_plural_texts(&"ENEMIES_LEFT", "de", PackedStringArray(["{enemy.count} Feind", "{enemy.count} Feinde"]))
	_catalog.set_text(&"TICK", "en", "Tick {tick}")


func _test_defaults() -> void:
	var text := LocalizedText.new()
	expect_equal(text.key, &"", "key defaults to empty")
	expect_equal(text.context, &"", "context defaults to empty")
	expect(text.args.is_empty(), "args defaults to empty")
	expect_equal(text.count_argument, &"", "count_argument defaults to empty")
	expect_equal(text.channel, &"", "channel defaults to empty")
	expect_equal(text.refresh_mode, LocalizedText.REFRESH_ON_CHANGE, "refresh_mode defaults to REFRESH_ON_CHANGE")
	expect_equal(text.get_text(), "", "get_text without a key is empty")


func _test_get_text() -> void:
	var greeting := LocalizedText.new()
	greeting.key = &"NPC_GREETING"
	expect_equal(greeting.get_text(), "Hello Ada!", "get_text fills placeholders from TextInsertionServer")
	greeting.args = { "player_name": "Bob" }
	expect_equal(greeting.get_text(), "Hello Bob!", "args win over server values")
	greeting.args = {}
	TranslationServer.set_locale("de")
	expect_equal(greeting.get_text(), "Hallo Ada!", "get_text follows the locale")
	greeting.context = &"casual"
	expect_equal(greeting.get_text(), "Na, Ada?", "context picks the text of that context")
	TranslationServer.set_locale("en")


func _test_plural() -> void:
	var enemies := LocalizedText.new()
	enemies.key = &"ENEMIES_LEFT"
	enemies.count_argument = &"enemy.count"
	TextInsertionServer.set_value(&"enemy.count", 1)
	expect_equal(enemies.get_text(), "1 enemy left", "count_argument from a server value picks the singular")
	TextInsertionServer.set_value(&"enemy.count", 4)
	expect_equal(enemies.get_text(), "4 enemies left", "count_argument from a server value picks the plural")
	enemies.args = { "enemy.count": 1 }
	expect_equal(enemies.get_text(), "1 enemy left", "count_argument from args")
	enemies.args = {}
	TextInsertionServer.register_provider(&"enemy.count", _two_enemies)
	expect_equal(enemies.get_text(), "2 enemies left", "count_argument from a provider picks the plural")
	TextInsertionServer.unregister_provider(&"enemy.count")


func _test_channel() -> void:
	TranslationServer.set_locale_for(&"subtitles", "de")
	var subtitle := LocalizedText.new()
	subtitle.key = &"NPC_GREETING"
	subtitle.channel = &"subtitles"
	expect_equal(subtitle.get_text(), "Hallo Ada!", "an explicit channel uses the locale of that channel")
	TranslationServer.clear_locale_for(&"subtitles")
	expect_equal(subtitle.get_text(), "Hello Ada!", "a channel without its own locale follows the general one")


func _test_bind() -> void:
	var greeting := LocalizedText.new()
	greeting.key = &"NPC_GREETING"
	var label := Label.new()
	var button := Button.new()
	var rich := RichTextLabel.new()
	add_child(label)
	add_child(button)
	add_child(rich)
	greeting.bind(label)
	greeting.bind(button)
	greeting.bind(button, &"tooltip_text")
	greeting.bind(rich)
	expect_equal(label.text, "Hello Ada!", "bind sets the text at once")
	expect(greeting.is_bound(label), "is_bound finds the binding")
	expect(label.get_meta(&"localized_text") == greeting, "the binding is the metadata localized_text")
	expect_equal(label.auto_translate_mode, Node.AUTO_TRANSLATE_MODE_DISABLED, "a bound node is not translated twice")
	expect(greeting.is_bound(button, &"tooltip_text"), "is_bound finds another property")
	expect_equal(button.tooltip_text, "Hello Ada!", "bind sets another property")
	var button_meta: Dictionary = button.get_meta(&"localized_text")
	expect_equal(button_meta.size(), 2, "with two properties the metadata is a dictionary")
	expect(button_meta["tooltip_text"] == greeting, "the dictionary maps the property to the LocalizedText")

	greeting.text_changed.connect(_on_text_changed)
	TextInsertionServer.set_value(&"player_name", "Bob")
	expect_equal(label.text, "Hello Ada!", "a refresh waits for the end of the frame")
	await _settle()
	expect_equal(label.text, "Hello Bob!", "a value change refreshes a Label")
	expect_equal(button.text, "Hello Bob!", "a value change refreshes a Button")
	expect_equal(rich.text, "Hello Bob!", "a value change refreshes a RichTextLabel")
	expect_equal(_changed_texts, ["Hello Bob!"] as Array[String], "text_changed is emitted once")
	TranslationServer.set_locale("de")
	await _settle()
	expect_equal(label.text, "Hallo Bob!", "a locale change refreshes")
	TranslationServer.set_locale("en")
	greeting.key = &"ENEMIES_LEFT"
	greeting.count_argument = &"enemy.count"
	await _settle()
	expect_equal(label.text, "4 enemies left", "a property change refreshes")
	greeting.text_changed.disconnect(_on_text_changed)

	greeting.unbind(label)
	expect(not greeting.is_bound(label), "unbind removes the binding")
	expect(not label.has_meta(&"localized_text"), "unbind removes the metadata")
	expect_equal(label.auto_translate_mode, Node.AUTO_TRANSLATE_MODE_INHERIT, "unbind restores auto translation")
	greeting.unbind(button)
	greeting.unbind(button, &"tooltip_text")
	greeting.unbind(rich)
	label.free()
	button.free()
	rich.free()


func _test_metadata_binding() -> void:
	var greeting := LocalizedText.new()
	greeting.key = &"NPC_GREETING"
	var npc := Label.new()
	npc.set_meta(&"localized_text", greeting)
	var answer := Button.new()
	answer.set_meta(&"localized_text", { "text": greeting, "tooltip_text": greeting })
	add_child(npc)
	add_child(answer)
	expect_equal(npc.text, "Hello Bob!", "metadata binds when the node enters the tree")
	expect(greeting.is_bound(npc), "the metadata LocalizedText is bound")
	expect_equal(answer.tooltip_text, "Hello Bob!", "dictionary metadata binds each property")
	TextInsertionServer.set_value(&"player_name", "Cy")
	await _settle()
	expect_equal(npc.text, "Hello Cy!", "a metadata binding refreshes")
	expect_equal(answer.tooltip_text, "Hello Cy!", "a dictionary binding refreshes")
	npc.free()
	answer.free()


func _test_refresh_mode() -> void:
	TextInsertionServer.register_provider(&"tick", _tick)
	var ticker := LocalizedText.new()
	ticker.key = &"TICK"
	var label := Label.new()
	add_child(label)
	ticker.bind(label)
	await _settle()
	var before: int = _ticks
	TextInsertionServer.set_value(&"player_name", "Dee")
	await _settle()
	expect_equal(_ticks, before, "a value change does not refresh texts that do not use it")
	ticker.refresh_mode = LocalizedText.REFRESH_EVERY_FRAME
	await _settle()
	var frame_before: int = _ticks
	await _settle()
	expect(_ticks >= frame_before + 2, "REFRESH_EVERY_FRAME refreshes every frame")
	expect_equal(label.text, "Tick %d" % _ticks, "REFRESH_EVERY_FRAME sets the new text")
	ticker.unbind(label)
	TextInsertionServer.unregister_provider(&"tick")
	var enemies := LocalizedText.new()
	enemies.key = &"ENEMIES_LEFT"
	enemies.count_argument = &"enemy.count"
	enemies.refresh_mode = LocalizedText.REFRESH_EVERY_FRAME
	TextInsertionServer.set_value(&"enemy.count", 1)
	enemies.bind(label)
	TextInsertionServer.register_provider(&"enemy.count", _two_enemies)
	await _settle()
	expect_equal(label.text, "2 enemies left", "REFRESH_EVERY_FRAME picks the plural form of a provider count")
	enemies.unbind(label)
	TextInsertionServer.unregister_provider(&"enemy.count")
	label.free()


func _settle() -> void:
	# Refreshes run at the end of the frame.
	await get_tree().process_frame
	await get_tree().process_frame


func _two_enemies() -> int:
	return 2


func _tick() -> int:
	_ticks += 1
	return _ticks


func _on_text_changed(text: String) -> void:
	_changed_texts.append(text)
