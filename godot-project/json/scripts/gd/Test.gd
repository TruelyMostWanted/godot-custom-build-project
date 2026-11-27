extends FeatureTest
## Full API test of JSON: what this engine adds to Godot's JSON (serialize(), deserialize(), the .json resource loader and
## saver) and the behavior the wiki guide promises. Same tests as scripts/cs/Test.cs.

const ROOT := "user://json/gd/"
const ENEMIES := "res://json/resources/enemies.json"
const SCHEMA := "res://json/resources/enemies_schema.tres"
const PLAIN_TEXT := "{\"answer\": 42}"


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	_test_data_file()
	_test_parse_error()
	_test_schema()
	_test_serialize()
	_test_deserialize()
	_test_files()
	_test_numbers()


func _make_sword() -> Item:
	var sword := Item.new()
	sword.name = "Sword"
	sword.rarity = Item.Rarity.Epic
	sword.code = "007"
	return sword


func _test_data_file() -> void:
	var file := load(ENEMIES) as JSON
	expect(file != null, "load() of a .json without \"$type\" returns a JSON")
	var data: Dictionary = file.data
	expect_equal(data.keys(), ["enemies", "boss"], "data is the parsed Dictionary")
	var slime: Dictionary = data["enemies"][0]
	expect_equal(typeof(slime["hp"]), TYPE_FLOAT, "every JSON number is a float")
	expect_equal(slime["drops"], ["gel"], "arrays are parsed")
	expect(data["boss"] == null, "null is parsed")
	expect_equal(ResourceLoader.get_resource_type(ENEMIES), "JSON", "get_resource_type() of a plain .json is JSON")


func _test_parse_error() -> void:
	var json := JSON.new()
	expect_equal(json.parse("{\"name\": "), ERR_PARSE_ERROR, "parse() of broken text")
	expect_equal(json.get_error_line(), 0, "get_error_line(): the text has one line, counted from 0")
	expect_equal(json.get_error_message(), "Expected value, got 'EOF'", "get_error_message()")
	expect(JSON.parse_string("{\"name\": ") == null, "parse_string() of broken text is null")


func _test_schema() -> void:
	var text: String = FileAccess.get_file_as_string(ENEMIES)
	var data: Variant = JSON.parse_string(text)
	var schema := load(SCHEMA) as DataSchema
	var check: DataValidation = schema.validate(data)
	expect(check.is_valid(), "enemies.json matches enemies_schema.tres (int fields accept JSON floats without a fraction)")
	data["enemies"][1]["hp"] = 6.5
	var fraction: DataValidation = schema.validate(data)
	expect_equal(fraction.get_issue_kind(0), DataValidation.ISSUE_KIND_TYPE_MISMATCH, "an int field rejects a JSON float with a fraction")


func _test_serialize() -> void:
	var sword := _make_sword()
	var text: String = JSON.serialize(sword)
	expect_equal(text, ResourceTextSerializer.serialize(sword, ResourceTextSerializer.FORMAT_JSON), "serialize() is ResourceTextSerializer.serialize(FORMAT_JSON)")
	expect(text.begins_with("{\n  \"$type\": \"Item\",\n  \"$format_version\": 1,\n"), "serialize() writes \"$type\" and \"$format_version\" first")
	var options := SerializerOptions.new()
	options.indent = ""
	options.sort_keys = true
	var compact: String = JSON.serialize(sword, options)
	expect_equal(compact, ResourceTextSerializer.serialize(sword, ResourceTextSerializer.FORMAT_JSON, options), "serialize() passes the options")
	expect(not compact.contains("\n"), "indent = \"\" writes one line")


func _test_deserialize() -> void:
	var text: String = JSON.serialize(_make_sword())
	var copy := JSON.deserialize(text) as Item
	expect_equal(copy.code, "007", "deserialize() restores the resource")
	expect_equal(copy.rarity, Item.Rarity.Epic, "deserialize() reads enum names")
	var hand_written := "{\"name\": \"Shield\", \"count\": 2}"
	var shield := JSON.deserialize(hand_written, &"Item") as Item
	expect_equal(shield.name, "Shield", "deserialize() with a type hint reads a file without \"$type\"")
	expect_equal(shield.weight, 0.5, "missing values keep the defaults")
	var options := SerializerOptions.new()
	options.enums_as_names = false
	var numbers: String = JSON.serialize(_make_sword(), options)
	var from_numbers := JSON.deserialize(numbers, &"", options) as Item
	expect_equal(from_numbers.rarity, Item.Rarity.Epic, "deserialize() with options reads enum numbers")


func _test_files() -> void:
	var path := ROOT + "sword.json"
	expect_equal(ResourceSaver.save(_make_sword(), path), OK, "ResourceSaver.save() to .json")
	expect_equal(ResourceLoader.get_resource_type(path), "Resource", "get_resource_type() reads \"$type\" (native base of Item)")
	var loaded := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as Item
	expect_equal(loaded.code, "007", "a .json with \"$type\" loads as its class")

	var plain_path := ROOT + "shield.json"
	var file := FileAccess.open(plain_path, FileAccess.WRITE)
	file.store_string("{\"name\": \"Shield\"}")
	file.close()
	var plain: Resource = ResourceLoader.load(plain_path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(plain is JSON, "a .json without \"$type\" loads as JSON")
	var shield := ResourceLoader.load(plain_path, "Item", ResourceLoader.CACHE_MODE_IGNORE) as Item
	expect_equal(shield.name, "Shield", "a .json without \"$type\" loads as Item with the type hint")

	var data_path := ROOT + "plain.json"
	var json := JSON.new()
	json.parse(PLAIN_TEXT)
	expect_equal(ResourceSaver.save(json, data_path), OK, "ResourceSaver.save() of a JSON resource")
	var again := ResourceLoader.load(data_path, "", ResourceLoader.CACHE_MODE_IGNORE) as JSON
	expect_equal(again.data, { "answer": 42.0 }, "a saved JSON resource loads as JSON again")


func _test_numbers() -> void:
	var inventory := Inventory.new()
	inventory.gold = 7
	inventory.anything = 3
	var text: String = JSON.serialize(inventory)
	var copy := JSON.deserialize(text) as Inventory
	expect_equal(typeof(copy.gold), TYPE_INT, "a typed int property stays int")
	expect_equal(typeof(copy.anything), TYPE_FLOAT, "a Variant property can't tell int from float: 3 reads back as 3.0")
