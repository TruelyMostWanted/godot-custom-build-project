extends FeatureCheck
## GDScript checks of 345-feature-localized-text. Check the same things as scripts/cs/Issue345FeatureLocalizedTextCheck.cs.

const NPC_GREETING_SCENE := "res://345-feature-localized-text/packed-scenes/npc_greeting.tscn"

var _ticks := 0
var _changed_texts: Array[String] = []


func _get_check_name() -> String:
	return "345-feature-localized-text"


func _settle() -> void:
	# Refreshes are deferred to the end of the frame.
	await get_tree().process_frame
	await get_tree().process_frame


func _on_text_changed(text: String) -> void:
	_changed_texts.append(text)


func _tick() -> int:
	_ticks += 1
	return _ticks


func _make_catalog() -> TextLocalizationCatalog:
	var catalog := TextLocalizationCatalog.new()
	catalog.set_text(&"NPC_GREETING", "en", "Hello {player_name}!")
	catalog.set_text(&"NPC_GREETING", "de", "Hallo {player_name}!")
	catalog.set_text(&"NPC_GREETING", "de", "Na, {player_name}?", &"casual")
	catalog.set_plural_rule("en", "nplurals=2; plural=(n != 1);")
	catalog.set_plural_rule("de", "nplurals=2; plural=(n != 1);")
	catalog.set_plural_texts(&"ENEMIES_LEFT", "en", PackedStringArray(["{enemy.count} enemy left", "{enemy.count} enemies left"]))
	catalog.set_plural_texts(&"ENEMIES_LEFT", "de", PackedStringArray(["{enemy.count} Feind", "{enemy.count} Feinde"]))
	catalog.set_text(&"TICK", "en", "Tick {tick}")
	return catalog


func _run_checks() -> void:
	expect(ClassDB.class_exists("LocalizedText"), "LocalizedText is a registered class")
	var previous_locale := TranslationServer.get_locale()
	var catalog := _make_catalog()
	TextLocalizationServer.add_catalog(catalog, 100)
	TranslationServer.set_locale("en")
	TextInsertionServer.set_value(&"player_name", "Ada")

	# Defaults, as shown in the inspector.
	var defaults := LocalizedText.new()
	expect(defaults.key == &"" and defaults.context == &"" and defaults.args.is_empty() and defaults.count_argument == &"" and defaults.channel == &"",
			"empty defaults")
	expect_equal(defaults.refresh_mode, LocalizedText.REFRESH_ON_CHANGE, "refresh_mode defaults to ON_CHANGE")
	expect_equal(defaults.get_text(), "", "no key, no text")

	# NPC greeting: key, values, args, context.
	var greeting := LocalizedText.new()
	greeting.key = &"NPC_GREETING"
	expect_equal(greeting.get_text(), "Hello Ada!", "placeholder from TextInsertionServer")
	greeting.args = { "player_name": "Bob" }
	expect_equal(greeting.get_text(), "Hello Bob!", "args win over server values")
	greeting.args = {}
	TranslationServer.set_locale("de")
	expect_equal(greeting.get_text(), "Hallo Ada!", "follows the locale")
	greeting.context = &"casual"
	expect_equal(greeting.get_text(), "Na, Ada?", "context")
	greeting.context = &""

	# HUD line with a plural from a value.
	var enemies := LocalizedText.new()
	enemies.key = &"ENEMIES_LEFT"
	enemies.count_argument = &"enemy.count"
	TextInsertionServer.set_value(&"enemy.count", 1)
	expect_equal(enemies.get_text(), "1 Feind", "plural from a server value (one)")
	TextInsertionServer.set_value(&"enemy.count", 4)
	expect_equal(enemies.get_text(), "4 Feinde", "plural from a server value (other)")
	enemies.args = { "enemy.count": 1 }
	expect_equal(enemies.get_text(), "1 Feind", "plural from args")

	# Channel with its own locale.
	TranslationServer.set_locale_for(&"subtitles", "en")
	var subtitle := LocalizedText.new()
	subtitle.key = &"NPC_GREETING"
	subtitle.channel = &"subtitles"
	expect_equal(subtitle.get_text(), "Hello Ada!", "explicit channel uses its locale")
	TranslationServer.clear_locale_for(&"subtitles")

	# Binding Label, Button and RichTextLabel.
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
	expect_equal(label.text, "Hallo Ada!", "bind sets the text at once")
	expect(greeting.is_bound(label) and label.get_meta(&"localized_text") == greeting, "binding is the metadata localized_text")
	expect_equal(label.auto_translate_mode, Node.AUTO_TRANSLATE_MODE_DISABLED, "bound node is not translated twice")
	expect_equal(button.tooltip_text, "Hallo Ada!", "other properties bind too")

	greeting.text_changed.connect(_on_text_changed)
	TextInsertionServer.set_value(&"player_name", "Bob")
	expect_equal(label.text, "Hallo Ada!", "refresh is deferred")
	await _settle()
	expect_equal(label.text, "Hallo Bob!", "value change refreshes the label")
	expect_equal(button.text, "Hallo Bob!", "value change refreshes the button")
	expect_equal(rich.text, "Hallo Bob!", "value change refreshes the rich text label")
	expect_equal(_changed_texts, ["Hallo Bob!"] as Array[String], "text_changed once")
	TranslationServer.set_locale("en")
	await _settle()
	expect_equal(label.text, "Hello Bob!", "locale change refreshes")
	greeting.unbind(label)
	expect(not greeting.is_bound(label) and not label.has_meta(&"localized_text"), "unbind removes the metadata")
	expect_equal(label.auto_translate_mode, Node.AUTO_TRANSLATE_MODE_INHERIT, "unbind restores auto translation")
	greeting.unbind(button)
	greeting.unbind(button, &"tooltip_text")
	greeting.unbind(rich)
	greeting.text_changed.disconnect(_on_text_changed)

	# Inspector binding: metadata saved in a scene.
	var npc := (load(NPC_GREETING_SCENE) as PackedScene).instantiate() as Label
	add_child(npc)
	expect_equal(npc.text, "Hello Bob!", "metadata binding applies on entering the tree")
	var npc_text := npc.get_meta(&"localized_text") as LocalizedText
	expect(npc_text != null and npc_text.is_bound(npc), "scene metadata holds a bound LocalizedText")
	TextInsertionServer.set_value(&"player_name", "Cy")
	await _settle()
	expect_equal(npc.text, "Hello Cy!", "scene binding refreshes")

	# Only texts using a value refresh; EVERY_FRAME for provider values.
	_ticks = 0
	TextInsertionServer.register_provider(&"tick", _tick)
	var ticker := LocalizedText.new()
	ticker.key = &"TICK"
	var tick_label := Label.new()
	add_child(tick_label)
	ticker.bind(tick_label)
	await _settle()
	var before := _ticks
	TextInsertionServer.set_value(&"player_name", "Dee")
	await _settle()
	expect_equal(_ticks, before, "a value change does not touch texts that do not use it")
	ticker.refresh_mode = LocalizedText.REFRESH_EVERY_FRAME
	await _settle()
	var frame_before := _ticks
	await _settle()
	expect(_ticks >= frame_before + 2, "EVERY_FRAME refreshes every frame")
	expect_equal(tick_label.text, "Tick %d" % _ticks, "EVERY_FRAME sets the new text")
	TextInsertionServer.unregister_provider(&"tick")

	await _measure()

	for node: Node in [label, button, rich, npc, tick_label]:
		node.queue_free()
	TextInsertionServer.remove_value(&"player_name")
	TextInsertionServer.remove_value(&"enemy.count")
	TextLocalizationServer.remove_catalog(catalog)
	TranslationServer.set_locale(previous_locale)


func _measure() -> void:
	const COUNT := 1000
	var labels: Array[Label] = []
	var texts: Array[LocalizedText] = []
	for idx in COUNT:
		var label := Label.new()
		add_child(label)
		labels.append(label)
		var text := LocalizedText.new()
		text.key = &"ENEMIES_LEFT" if idx == 0 else &"NPC_GREETING"
		text.count_argument = &"enemy.count" if idx == 0 else &""
		texts.append(text)
	await _settle()

	var start := Time.get_ticks_usec()
	for idx in COUNT:
		texts[idx].bind(labels[idx])
	var bind_usec := Time.get_ticks_usec() - start

	# Frame times include the engine's frame work; the idle frame is the baseline.
	start = Time.get_ticks_usec()
	await get_tree().process_frame
	var idle_usec := Time.get_ticks_usec() - start
	start = Time.get_ticks_usec()
	TranslationServer.set_locale("de")
	await get_tree().process_frame
	var locale_usec := Time.get_ticks_usec() - start
	start = Time.get_ticks_usec()
	TextInsertionServer.set_value(&"enemy.count", 7)
	await get_tree().process_frame
	var one_usec := Time.get_ticks_usec() - start
	start = Time.get_ticks_usec()
	TextInsertionServer.set_value(&"player_name", "Eve")
	await get_tree().process_frame
	var all_usec := Time.get_ticks_usec() - start
	await _settle()
	expect_equal(labels[COUNT - 1].text, "Hallo Eve!", "1 000 labels refreshed")
	expect_equal(labels[0].text, "7 Feinde", "the plural label refreshed")
	for text in texts:
		text.refresh_mode = LocalizedText.REFRESH_EVERY_FRAME
	await _settle()
	start = Time.get_ticks_usec()
	await get_tree().process_frame
	var frame_usec := Time.get_ticks_usec() - start
	print("345 timing (GDScript, %d labels, whole frames): bind %d us, idle frame %d us, locale switch %d us, value for 1 label %d us, for %d labels %d us, EVERY_FRAME frame %d us"
			% [COUNT, bind_usec, idle_usec, locale_usec, one_usec, COUNT - 1, all_usec, frame_usec])
	TranslationServer.set_locale("en")
	for label in labels:
		label.queue_free()
