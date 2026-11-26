extends FeatureCheck
## GDScript checks of 342-feature-text-insertion-server. Check the same things as scripts/cs/Issue342FeatureTextInsertionServerCheck.cs.

const NPC_VALUES := "res://342-feature-text-insertion-server/resources/npc_values.tres"

var _gold := 1250
var _changed_names: Array[StringName] = []
var _values_changed := 0


func _get_check_name() -> String:
	return "342-feature-text-insertion-server"


func _on_value_changed(value_name: StringName) -> void:
	_changed_names.append(value_name)


func _on_values_changed() -> void:
	_values_changed += 1


func _run_checks() -> void:
	expect(Engine.has_singleton("TextInsertionServer"), "TextInsertionServer is a registered singleton")
	var previous_locale := TranslationServer.get_locale()
	TranslationServer.set_locale("en")

	# NPC greeting with the player name from call args.
	expect_equal(TextInsertionServer.insert("Hello {player}, welcome to {town}!", { "player": "Mara", "town": "Ashford" }),
			"Hello Mara, welcome to Ashford!", "insert() fills placeholders from args")
	expect_equal(TextInsertionServer.insert("{{player}} {player}}}", { "player": "Mara" }), "{player} Mara}", "{{ and }} are literal braces")
	expect_equal(TextInsertionServer.insert("No placeholders"), "No placeholders", "text without braces is unchanged")

	# Inventory gold via a provider, read on every insertion.
	TextInsertionServer.register_provider(&"gold", func(): return _gold)
	expect(TextInsertionServer.has_provider(&"gold"), "register_provider registers")
	expect_equal(TextInsertionServer.insert("You have {gold:number} gold."), "You have 1,250 gold.", "provider value with number modifier")
	_gold = 1500000
	expect_equal(TextInsertionServer.insert("You have {gold:number} gold."), "You have 1,500,000 gold.", "provider is called on every insertion")
	expect_equal(TextInsertionServer.insert("{gold}", { "gold": 1 }), "1", "args win over providers")

	# Values and resources by priority.
	var npc_values := load(NPC_VALUES) as TextInsertionValues
	expect(npc_values != null and npc_values.get_value(&"npc") == "Smith", "TextInsertionValues loads from .tres")
	var mod_values := TextInsertionValues.new()
	mod_values.values = { "npc": "Modded Smith" }
	TextInsertionServer.add_values(npc_values)
	TextInsertionServer.add_values(mod_values)
	expect_equal(TextInsertionServer.insert("{npc} of {town}"), "Smith of Ashford", "higher priority resource wins")
	mod_values.priority = 10
	expect_equal(TextInsertionServer.insert("{npc}"), "Modded Smith", "priority change reorders resources")
	TextInsertionServer.set_value(&"npc", "Value Smith")
	expect_equal(TextInsertionServer.insert("{npc}"), "Value Smith", "set_value wins over resources")
	expect(TextInsertionServer.has_value(&"npc") and TextInsertionServer.get_value(&"npc") == "Value Smith", "get_value and has_value")
	TextInsertionServer.remove_value(&"npc")
	TextInsertionServer.remove_values(mod_values)
	TextInsertionServer.remove_values(npc_values)

	# Translated item name.
	var translation := Translation.new()
	translation.locale = "en"
	translation.add_message("ITEM_SWORD", "Sword")
	TranslationServer.add_translation(translation)
	expect_equal(TextInsertionServer.insert("You found a {item:t}. {item:t:upper}!", { "item": "ITEM_SWORD" }),
			"You found a Sword. SWORD!", "t translates, modifiers chain")
	TranslationServer.remove_translation(translation)

	# Other modifiers.
	expect_equal(TextInsertionServer.insert("{n:upper} {n:lower} {n:lower:capitalize}", { "n": "mara STONE" }),
			"MARA STONE mara stone Mara Stone", "upper, lower, capitalize")
	TranslationServer.set_locale("de")
	expect_equal(TextInsertionServer.insert("{x:number}", { "x": -1234.5 }), "-1.234,5", "number uses German separators")
	TranslationServer.set_locale("en")
	TextInsertionServer.register_modifier(&"shout", func(value): return str(value) + "!")
	expect_equal(TextInsertionServer.insert("{n:shout}", { "n": "hey" }), "hey!", "custom modifier")
	TextInsertionServer.unregister_modifier(&"shout")
	expect(not TextInsertionServer.has_modifier(&"shout"), "unregister_modifier removes")
	expect_equal(TextInsertionServer.insert("{intro:f}", { "intro": "Hi {player}", "player": "Mara" }), "Hi Mara", "f inserts into the value")
	expect_equal(TextInsertionServer.insert("{intro}", { "intro": "Hi {player}", "player": "Mara" }), "Hi {player}", "no recursion without f")

	# Missing value: placeholder kept (a warning is printed once).
	expect_equal(TextInsertionServer.insert("Hi {nobody_342}!"), "Hi {nobody_342}!", "missing value keeps the placeholder")

	# BBCode escaping of values, not of the text.
	expect_equal(TextInsertionServer.insert("[b]{p}[/b]", { "p": "[i]x" }, TextInsertionServer.ESCAPE_BBCODE), "[b][lb]i]x[/b]", "ESCAPE_BBCODE escapes values only")

	# Templates.
	var template := TextInsertionServer.parse("{player} has {gold} gold, {player}.")
	expect_equal(template.get_names(), [&"player", &"gold"] as Array[StringName], "template names, each once")
	expect(template.has_name(&"gold") and not template.has_name(&"npc"), "has_name")
	expect_equal(TextInsertionServer.insert_template(template, { "player": "Mara" }), "Mara has 1500000 gold, Mara.", "insert_template")

	# Signals.
	TextInsertionServer.value_changed.connect(_on_value_changed)
	TextInsertionServer.values_changed.connect(_on_values_changed)
	TextInsertionServer.set_value(&"hp", 3)
	TextInsertionServer.notify_changed(&"gold")
	TextInsertionServer.unregister_provider(&"gold")
	TextInsertionServer.remove_value(&"hp")
	expect_equal(_changed_names, [&"hp", &"gold", &"hp"] as Array[StringName], "value_changed from set_value, notify_changed, remove_value")
	expect_equal(_values_changed, 1, "values_changed from unregister_provider")
	TextInsertionServer.value_changed.disconnect(_on_value_changed)
	TextInsertionServer.values_changed.disconnect(_on_values_changed)

	# Timing: parsed template compared to String.format().
	var text := "Hello {name}, you have {gold} gold and {items} items."
	var args := { "name": "Mara", "gold": "1250", "items": "7" }
	var parsed := TextInsertionServer.parse(text)
	var count := 20000
	var begin := Time.get_ticks_usec()
	for _idx in count:
		TextInsertionServer.insert_template(parsed, args)
	var template_ns := (Time.get_ticks_usec() - begin) * 1000.0 / count
	begin = Time.get_ticks_usec()
	for _idx in count:
		text.format(args)
	var format_ns := (Time.get_ticks_usec() - begin) * 1000.0 / count
	expect_equal(TextInsertionServer.insert_template(parsed, args), text.format(args), "insert_template matches String.format")
	print("342 timing (GDScript): insert_template %d ns, String.format %d ns per call" % [template_ns, format_ns])

	TranslationServer.set_locale(previous_locale)
