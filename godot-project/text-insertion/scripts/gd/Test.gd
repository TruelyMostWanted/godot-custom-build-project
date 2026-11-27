extends FeatureTest
## Full API test of Text Insertion (TextInsertionServer, TextInsertionValues, TextInsertionTemplate). Same tests as scripts/cs/Test.cs.

const TEXT_VALUES := "res://text-insertion/resources/text_values.tres"
const SWORD_KEY := &"item.sword"

var _gold: int = 1250
var _changed_names: Array[StringName] = []
var _values_changed: int = 0


func _run_tests() -> void:
	var previous_locale: String = TranslationServer.get_locale()
	TranslationServer.set_locale("en")
	_test_insert()
	_test_providers()
	_test_values()
	_test_values_resource()
	_test_values_priority()
	_test_translate_modifier()
	_test_builtin_modifiers()
	_test_custom_modifiers()
	_test_escape()
	_test_template()
	_test_signals()
	TranslationServer.set_locale(previous_locale)


func _test_insert() -> void:
	var filled: String = TextInsertionServer.insert("Hello {player}, welcome to {town}!", { "player": "Mara", "town": "Ashford" })
	expect_equal(filled, "Hello Mara, welcome to Ashford!", "insert fills placeholders from args")
	var braces: String = TextInsertionServer.insert("{{player}} {player}}}", { "player": "Mara" })
	expect_equal(braces, "{player} Mara}", "{{ and }} are literal braces")
	var plain: String = TextInsertionServer.insert("No placeholders")
	expect_equal(plain, "No placeholders", "a text without braces stays the same")
	var missing: String = TextInsertionServer.insert("Hi {nobody}!")
	expect_equal(missing, "Hi {nobody}!", "a placeholder without a value stays as written")


func _test_providers() -> void:
	TextInsertionServer.register_provider(&"gold", _get_gold)
	expect(TextInsertionServer.has_provider(&"gold"), "has_provider finds a registered provider")
	expect_equal(TextInsertionServer.insert("{gold}"), "1250", "insert reads the provider")
	_gold = 1500
	expect_equal(TextInsertionServer.insert("{gold}"), "1500", "the provider is called on every insertion")
	expect_equal(TextInsertionServer.insert("{gold}", { "gold": 1 }), "1", "args win over providers")
	TextInsertionServer.set_value(&"gold", 7)
	expect_equal(TextInsertionServer.insert("{gold}"), "1500", "providers win over set_value")
	TextInsertionServer.remove_value(&"gold")
	TextInsertionServer.unregister_provider(&"gold")
	expect(not TextInsertionServer.has_provider(&"gold"), "unregister_provider removes the provider")


func _test_values() -> void:
	TextInsertionServer.set_value(&"hp", 30)
	expect(TextInsertionServer.has_value(&"hp"), "has_value finds a set value")
	expect_equal(TextInsertionServer.get_value(&"hp"), 30, "get_value returns the value")
	expect_equal(TextInsertionServer.insert("{hp} HP"), "30 HP", "insert reads set_value")
	TextInsertionServer.remove_value(&"hp")
	expect(not TextInsertionServer.has_value(&"hp"), "remove_value removes the value")
	expect_equal(TextInsertionServer.get_value(&"hp", 5), 5, "get_value returns the default without a value")


func _test_values_resource() -> void:
	var text_values: TextInsertionValues = load(TEXT_VALUES)
	expect_equal(text_values.get_value(&"game_name"), "Ashfall", "TextInsertionValues loads from .tres")
	expect_equal(text_values.priority, 0, "priority defaults to 0")
	var values := TextInsertionValues.new()
	values.set_value(&"npc", "Smith")
	expect(values.has_value(&"npc"), "TextInsertionValues.has_value finds a set value")
	expect_equal(values.values[&"npc"], "Smith", "values holds the set value")
	values.remove_value(&"npc")
	expect(not values.has_value(&"npc"), "TextInsertionValues.remove_value removes the value")
	expect_equal(values.get_value(&"npc", "none"), "none", "TextInsertionValues.get_value returns the default")


func _test_values_priority() -> void:
	var game_values := TextInsertionValues.new()
	game_values.set_value(&"npc", "Smith")
	var mod_values := TextInsertionValues.new()
	mod_values.set_value(&"npc", "Modded Smith")
	TextInsertionServer.add_values(mod_values)
	TextInsertionServer.add_values(game_values)
	expect_equal(TextInsertionServer.insert("{npc}"), "Smith", "with equal priority, the resource added later wins")
	mod_values.priority = 10
	expect_equal(TextInsertionServer.insert("{npc}"), "Modded Smith", "a higher priority wins")
	TextInsertionServer.set_value(&"npc", "Value Smith")
	expect_equal(TextInsertionServer.insert("{npc}"), "Value Smith", "set_value wins over resources")
	TextInsertionServer.remove_value(&"npc")
	TextInsertionServer.remove_values(mod_values)
	expect_equal(TextInsertionServer.insert("{npc}"), "Smith", "remove_values removes a resource")
	TextInsertionServer.remove_values(game_values)


func _test_translate_modifier() -> void:
	var catalog := TextLocalizationCatalog.new()
	catalog.set_text(SWORD_KEY, "en", "Sword")
	TextLocalizationServer.add_catalog(catalog)
	var found: String = TextInsertionServer.insert("You found a {item:t}. {item:t:upper}!", { "item": SWORD_KEY })
	expect_equal(found, "You found a Sword. SWORD!", "t translates the value, modifiers chain")
	TextLocalizationServer.remove_catalog(catalog)


func _test_builtin_modifiers() -> void:
	var cases: String = TextInsertionServer.insert("{n:upper} {n:lower} {n:lower:capitalize}", { "n": "mara STONE" })
	expect_equal(cases, "MARA STONE mara stone Mara Stone", "upper, lower and capitalize")
	expect_equal(TextInsertionServer.insert("{x:number}", { "x": 1234567 }), "1,234,567", "number uses English separators")
	TranslationServer.set_locale("de")
	expect_equal(TextInsertionServer.insert("{x:number}", { "x": -1234.5 }), "-1.234,5", "number uses German separators")
	TranslationServer.set_locale("en")
	var args: Dictionary = { "intro": "Hi {player}", "player": "Mara" }
	expect_equal(TextInsertionServer.insert("{intro:f}", args), "Hi Mara", "f inserts placeholders inside the value")
	expect_equal(TextInsertionServer.insert("{intro}", args), "Hi {player}", "without f a value is not expanded")


func _test_custom_modifiers() -> void:
	TextInsertionServer.register_modifier(&"shout", _shout)
	expect(TextInsertionServer.has_modifier(&"shout"), "has_modifier finds a registered modifier")
	expect_equal(TextInsertionServer.insert("{n:shout}", { "n": "hey" }), "hey!", "a custom modifier changes the value")
	TextInsertionServer.unregister_modifier(&"shout")
	expect(not TextInsertionServer.has_modifier(&"shout"), "unregister_modifier removes the modifier")
	expect(not TextInsertionServer.has_modifier(&"upper"), "has_modifier does not list built-in modifiers")


func _test_escape() -> void:
	var args: Dictionary = { "p": "[i]x" }
	var escaped: String = TextInsertionServer.insert("[b]{p}[/b]", args, TextInsertionServer.ESCAPE_BBCODE)
	expect_equal(escaped, "[b][lb]i]x[/b]", "ESCAPE_BBCODE escapes the values, not the text")
	var raw: String = TextInsertionServer.insert("[b]{p}[/b]", args, TextInsertionServer.ESCAPE_NONE)
	expect_equal(raw, "[b][i]x[/b]", "ESCAPE_NONE keeps the values as they are")
	var by_default: String = TextInsertionServer.insert("[b]{p}[/b]", args, TextInsertionServer.ESCAPE_DEFAULT)
	expect_equal(by_default, "[b][i]x[/b]", "ESCAPE_DEFAULT follows the project setting (off)")


func _test_template() -> void:
	var source: String = "{player} has {gold} gold, {player}."
	var template: TextInsertionTemplate = TextInsertionServer.parse(source)
	expect_equal(template.get_source(), source, "get_source returns the parsed text")
	expect_equal(template.get_names(), [&"player", &"gold"] as Array[StringName], "get_names lists each name once")
	expect(template.has_name(&"gold"), "has_name finds a used name")
	expect(not template.has_name(&"npc"), "has_name misses an unused name")
	var filled: String = TextInsertionServer.insert_template(template, { "player": "Mara", "gold": 3 })
	expect_equal(filled, "Mara has 3 gold, Mara.", "insert_template fills the template")
	var escaped: String = TextInsertionServer.insert_template(template, { "player": "[b]", "gold": 3 }, TextInsertionServer.ESCAPE_BBCODE)
	expect_equal(escaped, "[lb]b] has 3 gold, [lb]b].", "insert_template escapes with ESCAPE_BBCODE")


func _test_signals() -> void:
	TextInsertionServer.value_changed.connect(_on_value_changed)
	TextInsertionServer.values_changed.connect(_on_values_changed)
	TextInsertionServer.register_provider(&"gold", _get_gold)
	TextInsertionServer.set_value(&"hp", 3)
	TextInsertionServer.notify_changed(&"gold")
	TextInsertionServer.remove_value(&"hp")
	TextInsertionServer.unregister_provider(&"gold")
	TextInsertionServer.value_changed.disconnect(_on_value_changed)
	TextInsertionServer.values_changed.disconnect(_on_values_changed)
	var expected: Array[StringName] = [&"hp", &"gold", &"hp"]
	expect_equal(_changed_names, expected, "value_changed from set_value, notify_changed and remove_value")
	expect_equal(_values_changed, 2, "values_changed from register_provider and unregister_provider")


func _get_gold() -> int:
	return _gold


func _shout(value: Variant) -> String:
	return str(value) + "!"


func _on_value_changed(value_name: StringName) -> void:
	_changed_names.append(value_name)


func _on_values_changed() -> void:
	_values_changed += 1
