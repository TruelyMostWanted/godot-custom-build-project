extends FeatureTest
## Full API test of Text Serialization: ResourceTextSerializer, SerializerOptions and the TextDataFormat base.
## Same tests as scripts/cs/Test.cs.

const ROOT := "user://text-serialization/gd/"
const OWNER_NAME := "Ada & <Bob> \"the\" 2nd = ok: yes\nline two"
const COMPACT_OPTIONS := "res://text-serialization/resources/compact_options.tres"
const SETTINGS := "filesystem/resources/text_formats/"

# The texts of the wiki section "Compare the formats".
const SWORD_JSON := "{\n  \"$type\": \"Item\",\n  \"$format_version\": 1,\n  \"name\": \"Sword\",\n  \"count\": 1,\n  \"weight\": 0.5,\n  \"rarity\": \"Epic\",\n  \"offset\": [\n    0.5,\n    -2.0\n  ],\n  \"code\": \"007\"\n}\n"
const SWORD_JSON_COMPACT := "{\"$format_version\":1,\"$type\":\"Item\",\"code\":\"007\",\"count\":1,\"name\":\"Sword\",\"offset\":[0.5,-2.0],\"rarity\":\"Epic\",\"weight\":0.5}"
const SWORD_INI := "[root]\n$type=\"Item\"\n$format_version=1\nname=\"Sword\"\ncount=1\nweight=0.5\nrarity=\"Epic\"\noffset=Vector2(0.5, -2)\ncode=\"007\"\n"
const SWORD_XML := "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<resource type=\"Item\" format_version=\"1\">\n  <name>Sword</name>\n  <count>1</count>\n  <weight>0.5</weight>\n  <rarity>Epic</rarity>\n  <offset kind=\"array\">0.5, -2.0</offset>\n  <code kind=\"string\">007</code>\n</resource>\n"
const SWORD_TOML := "\"$type\" = \"Item\"\n\"$format_version\" = 1\nname = \"Sword\"\ncount = 1\nweight = 0.5\nrarity = \"Epic\"\noffset = [0.5, -2.0]\ncode = \"007\"\n"
const SWORD_YAML := "$type: Item\n$format_version: 1\nname: Sword\ncount: 1\nweight: 0.5\nrarity: Epic\noffset: [0.5, -2.0]\ncode: \"007\"\n"
const TABLE_CSV := "name,count,weight,rarity,offset,code\nSword,1,0.5,Epic,\"0.5, -2.0\",007\nPotion,3,0.5,Common,\"0.0, 0.0\",\n"


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	_test_formats()
	_test_compare_formats()
	_test_files()
	_test_round_trip()
	_test_variant_values()
	_test_csv_table()
	_test_type_hint()
	_test_dictionaries()
	_test_option_defaults()
	_test_options()
	_test_preset()
	_test_project_settings()
	_test_text_data_format()


func _make_sword() -> Item:
	var sword := Item.new()
	sword.name = "Sword"
	sword.rarity = Item.Rarity.Epic
	sword.offset = Vector2(0.5, -2.0)
	sword.code = "007"
	return sword


func _make_inventory() -> Inventory:
	var potion := Item.new()
	potion.name = "Potion"
	potion.count = 3
	var space := Item.new()
	space.name = " "
	var inventory := Inventory.new()
	inventory.owner_name = OWNER_NAME
	inventory.gold = 250
	inventory.tint = Color(0.5, 0.25, 1.0, 0.5)
	inventory.cell = Vector3i(1, -2, 3)
	inventory.tags = PackedStringArray(["a", "b c"])
	inventory.main_hand = _make_sword()
	inventory.items = [potion, space]
	inventory.scores = { "ann": 10, "my key": 20 }
	return inventory


func _expect_inventory(loaded: Resource, label: String) -> void:
	var inventory := loaded as Inventory
	if not expect(inventory != null, "%s: loads as Inventory" % label):
		return
	expect_equal(inventory.owner_name, OWNER_NAME, "%s: string with & < > quotes = : and a line break" % label)
	expect_equal(inventory.gold, 250, "%s: int" % label)
	expect(inventory.tint.is_equal_approx(Color(0.5, 0.25, 1.0, 0.5)), "%s: Color" % label)
	expect_equal(inventory.cell, Vector3i(1, -2, 3), "%s: Vector3i" % label)
	expect_equal(inventory.tags, PackedStringArray(["a", "b c"]), "%s: PackedStringArray" % label)
	expect_equal(inventory.main_hand.code, "007", "%s: nested resource, a string that looks like a number" % label)
	expect_equal(inventory.main_hand.rarity, Item.Rarity.Epic, "%s: enum" % label)
	expect(inventory.main_hand.offset.is_equal_approx(Vector2(0.5, -2.0)), "%s: Vector2" % label)
	expect(inventory.spare == null, "%s: a missing resource stays null" % label)
	expect_equal(inventory.items.size(), 2, "%s: typed array of resources" % label)
	expect_equal(inventory.items[0].count, 3, "%s: array element" % label)
	expect_equal(inventory.items[1].name, " ", "%s: a name that is a space" % label)
	expect_equal(inventory.scores, { "ann": 10, "my key": 20 }, "%s: typed Dictionary, a key with a space" % label)


func _test_formats() -> void:
	var formats: PackedInt32Array = ResourceTextSerializer.get_available_formats()
	expect_equal(formats, PackedInt32Array([0, 1, 2, 3, 4, 5]), "get_available_formats() lists all six formats")
	expect(ResourceTextSerializer.is_format_available(ResourceTextSerializer.FORMAT_TOML), "is_format_available(FORMAT_TOML)")
	expect(ResourceTextSerializer.is_format_available(ResourceTextSerializer.FORMAT_YAML), "is_format_available(FORMAT_YAML)")
	expect(not ResourceTextSerializer.is_format_available(ResourceTextSerializer.FORMAT_MAX), "is_format_available(FORMAT_MAX) is false")
	expect_equal(ResourceTextSerializer.get_format_extension(ResourceTextSerializer.FORMAT_JSON), "json", "get_format_extension(FORMAT_JSON)")
	expect_equal(ResourceTextSerializer.get_format_extension(ResourceTextSerializer.FORMAT_INI), "ini", "get_format_extension(FORMAT_INI)")
	expect_equal(ResourceTextSerializer.get_format_extension(ResourceTextSerializer.FORMAT_CSV), "csv", "get_format_extension(FORMAT_CSV)")
	expect_equal(ResourceTextSerializer.get_format_extension(ResourceTextSerializer.FORMAT_XML), "xml", "get_format_extension(FORMAT_XML)")
	expect_equal(ResourceTextSerializer.get_format_extension(ResourceTextSerializer.FORMAT_TOML), "toml", "get_format_extension(FORMAT_TOML)")
	expect_equal(ResourceTextSerializer.get_format_extension(ResourceTextSerializer.FORMAT_YAML), "yaml", "get_format_extension(FORMAT_YAML)")
	expect_equal(ResourceTextSerializer.get_format_from_extension(".YML"), ResourceTextSerializer.FORMAT_YAML, "get_format_from_extension(\".YML\") ignores case and dot")
	expect_equal(ResourceTextSerializer.get_format_from_extension("ini"), ResourceTextSerializer.FORMAT_INI, "get_format_from_extension(\"ini\")")
	expect_equal(ResourceTextSerializer.get_format_from_extension("tres"), ResourceTextSerializer.FORMAT_MAX, "get_format_from_extension() of an unknown extension is FORMAT_MAX")
	expect_equal(ResourceTextSerializer.FORMAT_MAX, 6, "FORMAT_MAX is 6")
	expect_equal(ResourceTextSerializer.TEXT_LAYOUT_VERSION, 1, "TEXT_LAYOUT_VERSION is 1")


func _test_compare_formats() -> void:
	var sword := _make_sword()
	expect_equal(ResourceTextSerializer.serialize(sword, ResourceTextSerializer.FORMAT_JSON), SWORD_JSON, "JSON text of the sword")
	expect_equal(ResourceTextSerializer.serialize(sword, ResourceTextSerializer.FORMAT_INI), SWORD_INI, "INI text of the sword")
	expect_equal(ResourceTextSerializer.serialize(sword, ResourceTextSerializer.FORMAT_XML), SWORD_XML, "XML text of the sword")
	expect_equal(ResourceTextSerializer.serialize(sword, ResourceTextSerializer.FORMAT_TOML), SWORD_TOML, "TOML text of the sword")
	expect_equal(ResourceTextSerializer.serialize(sword, ResourceTextSerializer.FORMAT_YAML), SWORD_YAML, "YAML text of the sword")
	var potion := Item.new()
	potion.name = "Potion"
	potion.count = 3
	var table := ItemTable.new()
	table.entries = [sword, potion]
	expect_equal(ResourceTextSerializer.serialize(table, ResourceTextSerializer.FORMAT_CSV), TABLE_CSV, "CSV text of the item table")


func _test_files() -> void:
	var inventory := _make_inventory()
	for extension: String in ["json", "ini", "xml", "toml", "yaml", "yml"]:
		var path := ROOT + "inventory." + extension
		var format: int = ResourceTextSerializer.get_format_from_extension(extension)
		expect_equal(ResourceSaver.save(inventory, path), OK, ".%s: ResourceSaver.save()" % extension)
		var text: String = ResourceTextSerializer.serialize(inventory, format)
		expect_equal(FileAccess.get_file_as_string(path), text, ".%s: the file text is serialize()" % extension)
		expect_equal(ResourceLoader.get_resource_type(path), "Resource", ".%s: get_resource_type() reads the type marker (native base)" % extension)
		var loaded: Resource = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
		_expect_inventory(loaded, "." + extension)


func _test_round_trip() -> void:
	var inventory := _make_inventory()
	for format: int in [ResourceTextSerializer.FORMAT_JSON, ResourceTextSerializer.FORMAT_INI, ResourceTextSerializer.FORMAT_XML, ResourceTextSerializer.FORMAT_TOML, ResourceTextSerializer.FORMAT_YAML]:
		var text: String = ResourceTextSerializer.serialize(inventory, format)
		var copy: Resource = ResourceTextSerializer.deserialize(text, format)
		_expect_inventory(copy, "deserialize(%s)" % ResourceTextSerializer.get_format_extension(format))


func _test_variant_values() -> void:
	var inventory := Inventory.new()
	inventory.notes = { "lives": 3, "ratio": 1.0, "empty": {}, "none": null }
	inventory.anything = [1, 2.5, "3", true, null]
	for format: int in [ResourceTextSerializer.FORMAT_XML, ResourceTextSerializer.FORMAT_YAML]:
		var label: String = ResourceTextSerializer.get_format_extension(format)
		var text: String = ResourceTextSerializer.serialize(inventory, format)
		var copy := ResourceTextSerializer.deserialize(text, format) as Inventory
		expect_equal(typeof(copy.notes["lives"]), TYPE_INT, "%s: an int in an untyped Dictionary stays int" % label)
		expect_equal(typeof(copy.notes["ratio"]), TYPE_FLOAT, "%s: a float without fraction stays float" % label)
		expect_equal(copy.notes["empty"], {}, "%s: an empty Dictionary" % label)
		expect(copy.notes.has("none"), "%s: a null value keeps its key" % label)
		expect_equal(copy.anything, [1, 2.5, "3", true, null], "%s: a Variant array keeps int, float, String, bool and null" % label)


func _test_csv_table() -> void:
	var table := ItemTable.new()
	table.entries = [_make_sword()]
	expect(ResourceTextSerializer.can_serialize(table, ResourceTextSerializer.FORMAT_CSV), "can_serialize(): a typed array of flat records fits CSV")
	expect(not ResourceTextSerializer.can_serialize(_make_inventory(), ResourceTextSerializer.FORMAT_CSV), "can_serialize(): nested data does not fit CSV")
	expect(ResourceTextSerializer.can_serialize(_make_inventory(), ResourceTextSerializer.FORMAT_JSON), "can_serialize(): any resource fits JSON")
	var path := ROOT + "items.csv"
	expect_equal(ResourceSaver.save(table, path), OK, "save an ItemTable as .csv")
	var loaded := ResourceLoader.load(path, "ItemTable", ResourceLoader.CACHE_MODE_IGNORE) as ItemTable
	expect_equal(loaded.entries[0].code, "007", ".csv loads as ItemTable with the type hint")
	var plain: Resource = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(plain is CSV, ".csv without a type hint loads as CSV")


func _test_type_hint() -> void:
	var options := SerializerOptions.new()
	options.include_type_marker = false
	for format: int in [ResourceTextSerializer.FORMAT_JSON, ResourceTextSerializer.FORMAT_INI, ResourceTextSerializer.FORMAT_XML, ResourceTextSerializer.FORMAT_TOML, ResourceTextSerializer.FORMAT_YAML]:
		var label: String = ResourceTextSerializer.get_format_extension(format)
		var text: String = ResourceTextSerializer.serialize(_make_sword(), format, options)
		expect(not text.contains("Item"), "%s: include_type_marker = false writes no marker" % label)
		var copy := ResourceTextSerializer.deserialize(text, format, &"Item") as Item
		expect_equal(copy.code, "007", "%s: text without a marker reads with the type hint" % label)
		var path := ROOT + "untyped." + label
		var file := FileAccess.open(path, FileAccess.WRITE)
		file.store_string(text)
		file.close()
		var data_file: Resource = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
		expect(data_file is JSON or data_file is TextDataFormat, "%s: a file without a marker loads as the data file class" % label)


func _test_dictionaries() -> void:
	var tree: Dictionary = ResourceTextSerializer.resource_to_dictionary(_make_sword())
	expect_equal(tree["$type"], "Item", "resource_to_dictionary(): type marker")
	expect_equal(tree["name"], "Sword", "resource_to_dictionary(): property")
	expect_equal(tree["rarity"], "Epic", "resource_to_dictionary(): enum by name")
	var copy := ResourceTextSerializer.dictionary_to_resource(tree) as Item
	expect_equal(copy.code, "007", "dictionary_to_resource() restores the resource")
	var options := SerializerOptions.new()
	options.include_type_marker = false
	var untyped: Dictionary = ResourceTextSerializer.resource_to_dictionary(_make_sword(), options)
	expect(not untyped.has("$type"), "resource_to_dictionary() passes the options")
	var hinted := ResourceTextSerializer.dictionary_to_resource(untyped, &"Item") as Item
	expect_equal(hinted.name, "Sword", "dictionary_to_resource() with a type hint")


func _test_option_defaults() -> void:
	var options := SerializerOptions.new()
	expect_equal(options.indent, "  ", "indent defaults to two spaces")
	expect(options.enums_as_names, "enums_as_names defaults to true")
	expect(not options.sort_keys, "sort_keys defaults to false")
	expect(not options.full_precision_floats, "full_precision_floats defaults to false")
	expect(options.include_type_marker, "include_type_marker defaults to true")
	expect(options.defaults == null, "defaults defaults to null")
	expect_equal(options.csv_delimiter, ",", "csv_delimiter defaults to ','")
	expect(options.csv_headers, "csv_headers defaults to true")
	expect_equal(options.ini_value_style, SerializerOptions.INI_VALUE_STYLE_GODOT_VARIANT, "ini_value_style defaults to GODOT_VARIANT")
	expect(options.xml_declaration, "xml_declaration defaults to true")
	expect_equal(SerializerOptions.INI_VALUE_STYLE_PLAIN, 0, "INI_VALUE_STYLE_PLAIN is 0")
	expect_equal(SerializerOptions.INI_VALUE_STYLE_MAX, 2, "INI_VALUE_STYLE_MAX is 2")
	var text: String = ResourceTextSerializer.serialize(_make_sword(), ResourceTextSerializer.FORMAT_JSON, options)
	expect_equal(text, SWORD_JSON, "new options give the same text as null (project settings)")


func _test_options() -> void:
	var sword := _make_sword()
	var options := SerializerOptions.new()
	options.indent = "\t"
	var tabs: String = ResourceTextSerializer.serialize(sword, ResourceTextSerializer.FORMAT_JSON, options)
	expect(tabs.contains("\n\t\"name\""), "indent")

	options = SerializerOptions.new()
	options.enums_as_names = false
	var numbers: String = ResourceTextSerializer.serialize(sword, ResourceTextSerializer.FORMAT_JSON, options)
	expect(numbers.contains("\"rarity\": 3"), "enums_as_names = false writes numbers")
	var from_numbers := ResourceTextSerializer.deserialize(numbers, ResourceTextSerializer.FORMAT_JSON) as Item
	expect_equal(from_numbers.rarity, Item.Rarity.Epic, "enum numbers read back")

	options = SerializerOptions.new()
	options.sort_keys = true
	options.indent = ""
	var compact: String = ResourceTextSerializer.serialize(sword, ResourceTextSerializer.FORMAT_JSON, options)
	expect_equal(compact, SWORD_JSON_COMPACT, "sort_keys and an empty indent write one sorted line")

	options = SerializerOptions.new()
	options.defaults = Item.new()
	var changed: String = ResourceTextSerializer.serialize(sword, ResourceTextSerializer.FORMAT_JSON, options)
	expect(not changed.contains("\"count\""), "defaults: unchanged values are not written")
	expect(changed.contains("\"code\""), "defaults: changed values are written")

	var precise := Item.new()
	precise.offset = Vector2(0.1, 0.5)
	options = SerializerOptions.new()
	options.full_precision_floats = true
	var full: String = ResourceTextSerializer.serialize(precise, ResourceTextSerializer.FORMAT_JSON, options)
	expect(full.contains("0.10000000149011612"), "full_precision_floats writes every digit")
	var short: String = ResourceTextSerializer.serialize(precise, ResourceTextSerializer.FORMAT_JSON)
	expect(not short.contains("0.10000000149011612"), "without full_precision_floats the shortest text")

	var table := ItemTable.new()
	table.entries = [sword]
	options = SerializerOptions.new()
	options.csv_delimiter = ";"
	var semicolons: String = ResourceTextSerializer.serialize(table, ResourceTextSerializer.FORMAT_CSV, options)
	expect(semicolons.contains("\nSword;1;"), "csv_delimiter")
	options.csv_headers = false
	var rows: String = ResourceTextSerializer.serialize(table, ResourceTextSerializer.FORMAT_CSV, options)
	expect(rows.begins_with("Sword;"), "csv_headers = false writes no header")
	var from_rows := ResourceTextSerializer.deserialize(rows, ResourceTextSerializer.FORMAT_CSV, &"ItemTable", options) as ItemTable
	expect_equal(from_rows.entries[0].code, "007", "csv_headers = false reads back with the same options")

	options = SerializerOptions.new()
	options.ini_value_style = SerializerOptions.INI_VALUE_STYLE_PLAIN
	var plain: String = ResourceTextSerializer.serialize(sword, ResourceTextSerializer.FORMAT_INI, options)
	expect(plain.contains("name=Sword\n"), "ini_value_style PLAIN writes strings without quotes")
	expect(plain.contains("code=\"007\""), "ini_value_style PLAIN quotes a string that looks like a number")
	var from_plain := ResourceTextSerializer.deserialize(plain, ResourceTextSerializer.FORMAT_INI) as Item
	expect(from_plain.offset.is_equal_approx(Vector2(0.5, -2.0)), "plain INI reads back")

	options = SerializerOptions.new()
	options.xml_declaration = false
	var bare: String = ResourceTextSerializer.serialize(sword, ResourceTextSerializer.FORMAT_XML, options)
	expect(bare.begins_with("<resource"), "xml_declaration = false")


func _test_preset() -> void:
	var preset := load(COMPACT_OPTIONS) as SerializerOptions
	var text: String = ResourceTextSerializer.serialize(_make_sword(), ResourceTextSerializer.FORMAT_JSON, preset)
	expect_equal(text, SWORD_JSON_COMPACT, "a SerializerOptions .tres works as a preset")
	var path := ROOT + "options.tres"
	expect_equal(ResourceSaver.save(preset, path), OK, "save SerializerOptions as .tres")
	var again := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as SerializerOptions
	expect(again.sort_keys, "the saved preset keeps sort_keys")


func _test_project_settings() -> void:
	expect_equal(ProjectSettings.get_setting(SETTINGS + "indent"), "  ", "project setting indent")
	expect_equal(ProjectSettings.get_setting(SETTINGS + "enums_as_names"), true, "project setting enums_as_names")
	expect_equal(ProjectSettings.get_setting(SETTINGS + "sort_keys"), false, "project setting sort_keys")
	expect_equal(ProjectSettings.get_setting(SETTINGS + "full_precision_floats"), false, "project setting full_precision_floats")
	expect_equal(ProjectSettings.get_setting(SETTINGS + "csv_delimiter"), ",", "project setting csv_delimiter")
	expect_equal(ProjectSettings.get_setting(SETTINGS + "csv_headers"), true, "project setting csv_headers")
	expect_equal(ProjectSettings.get_setting(SETTINGS + "ini_value_style"), SerializerOptions.INI_VALUE_STYLE_GODOT_VARIANT, "project setting ini_value_style")
	expect_equal(ProjectSettings.get_setting(SETTINGS + "xml_declaration"), true, "project setting xml_declaration")
	expect(not ProjectSettings.has_setting(SETTINGS + "ini_plain_values"), "the old bool ini_plain_values is not registered")


func _test_text_data_format() -> void:
	expect(not ClassDB.can_instantiate("TextDataFormat"), "TextDataFormat is abstract")
	expect(ClassDB.is_parent_class("TextDataFormat", "Resource"), "TextDataFormat is a Resource")
	for format_class: String in ["INI", "CSV", "XML", "TOML", "YAML"]:
		expect(ClassDB.is_parent_class(format_class, "TextDataFormat"), "%s is a TextDataFormat" % format_class)
	expect(not ClassDB.is_parent_class("JSON", "TextDataFormat"), "JSON is Godot's own class")
	expect_equal(INI.new().get_recognized_extensions(), PackedStringArray(["ini"]), "INI extensions")
	expect_equal(CSV.new().get_recognized_extensions(), PackedStringArray(["csv"]), "CSV extensions")
	expect_equal(XML.new().get_recognized_extensions(), PackedStringArray(["xml"]), "XML extensions")
	expect_equal(TOML.new().get_recognized_extensions(), PackedStringArray(["toml"]), "TOML extensions")
	expect_equal(YAML.new().get_recognized_extensions(), PackedStringArray(["yaml", "yml"]), "YAML extensions")

	var text := "; comment\n[a]\nb=1\n"
	var ini := INI.new()
	expect_equal(ini.parse(text, true), OK, "parse() with keep_text")
	expect_equal(ini.get_parsed_text(), text, "get_parsed_text() returns the kept text")
	expect_equal(ini.encode_to_text(), "[a]\nb=1\n", "encode_to_text() writes the data, not the kept text")
	var path := ROOT + "kept.ini"
	expect_equal(ini.save(path), OK, "save()")
	expect_equal(FileAccess.get_file_as_string(path), text, "save() of unchanged data keeps the comment")
	ini.set_value("a", "b", 2)
	expect_equal(ini.get_parsed_text(), "", "a change of data drops the kept text")
	expect(not ini.encode_to_text().contains("comment"), "encode_to_text() after a change has no comment")

	var loaded := INI.new()
	expect_equal(loaded.load(path), OK, "load()")
	expect_equal(loaded.get_value("a", "b"), 1, "load() reads the file")
	var options := SerializerOptions.new()
	loaded.options = options
	expect(loaded.options == options, "options")
	expect_equal(loaded.parse("[broken"), ERR_PARSE_ERROR, "parse() of broken text")
	expect_equal(loaded.get_error_line(), 1, "get_error_line()")
	expect_equal(loaded.get_error_column(), 0, "get_error_column(): INI reports no column")
	expect_equal(loaded.get_error_message(), "Line 1: ']' expected.", "get_error_message()")
	loaded.data = { "s": { "k": 1 } }
	expect_equal(loaded.get_error_message(), "", "setting data clears the error")
	loaded.clear()
	expect_equal(loaded.data, {}, "clear() empties data")
	loaded.set_value("s", "k", 1)
	expect_equal(loaded.save(path), OK, "save() of changed data")
	var saved: String = FileAccess.get_file_as_string(path)
	expect_equal(saved, "[s]\nk=1\n", "a data file saves as plain text without a type marker")
