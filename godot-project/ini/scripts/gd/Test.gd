extends FeatureTest
## Full API test of INI: every method of the INI class, its two value styles and the INI text of a resource.
## Same tests as scripts/cs/Test.cs.

const ROOT := "user://ini/gd/"
const SETTINGS := "res://ini/resources/settings.ini"
const HAND_WRITTEN := "; comment\ntop=1\n\n[player]\nname=\"Ann\"\npos=Vector2(1, 2)\nplain=just text\nflags=[1, 2]\n\n[player]\nlevel=3\n"
const GAME := "res://ini/resources/game.ini"
const PLAYER_KEYS: Array = ["name", "speed", "lives", "invincible", "spawn"]
const PASSWORD := "secret"


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	_test_data_file()
	_test_parse()
	_test_parse_errors()
	_test_sections()
	_test_config_file()
	_test_load_save()
	_test_encrypted()
	_test_stringify()
	_test_value_styles()
	_test_serialize()
	_test_dock_api()


func _make_sword() -> Item:
	var sword := Item.new()
	sword.name = "Sword"
	sword.offset = Vector2(0.5, -2.0)
	sword.code = "007"
	return sword


func _test_data_file() -> void:
	var settings := load(SETTINGS) as INI
	expect(settings != null, "load() of a plain .ini returns an INI")
	expect_equal(settings.get_sections(), PackedStringArray(["video", "audio"]), "get_sections()")
	expect_equal(settings.get_section_keys("video"), PackedStringArray(["width", "height", "vsync"]), "get_section_keys()")
	expect_equal(settings.get_value("video", "width"), 1280, "an int value")
	expect_equal(settings.get_value("video", "vsync"), true, "a bool value")
	expect_equal(settings.get_value("audio", "music"), 0.8, "a float value")
	expect_equal(settings.get_value("audio", "device"), "Speakers", "a quoted String value")
	expect_equal(settings.get_value("audio", "missing", 7), 7, "get_value() returns the default of a missing key")
	var extensions: PackedStringArray = ResourceLoader.get_recognized_extensions_for_type("INI")
	expect(extensions.has("ini"), "INI loads .ini files")
	expect(extensions.has("cfg"), "INI also loads .cfg files (#392)")


func _test_parse() -> void:
	var ini := INI.new()
	expect_equal(ini.parse(HAND_WRITTEN), OK, "parse() of a hand-written file")
	expect_equal(ini.get_value("", "top"), 1, "keys before the first header are in section \"\"")
	expect_equal(ini.get_value("player", "name"), "Ann", "a quoted string")
	expect_equal(ini.get_value("player", "pos"), Vector2(1, 2), "Godot syntax")
	expect_equal(ini.get_value("player", "plain"), "just text", "a plain value is a String")
	expect_equal(ini.get_value("player", "flags"), [1, 2], "an array")
	expect_equal(ini.get_value("player", "level"), 3, "a section that appears twice is merged")
	expect_equal(ini.get_section_keys("player").size(), 5, "the merged section has all keys")
	var sections: Dictionary = INI.parse_string("[a]\nb=1\n")
	expect_equal(sections, { "a": { "b": 1 } }, "parse_string() returns the sections")


func _test_parse_errors() -> void:
	var ini := INI.new()
	expect_equal(ini.parse("[video"), ERR_PARSE_ERROR, "an unclosed section header fails")
	expect_equal(ini.get_error_message(), "Line 1: ']' expected.", "error message of the unclosed header")
	expect_equal(ini.parse("a=1\n[broken\n"), ERR_PARSE_ERROR, "an unclosed header in line 2 fails")
	expect_equal(ini.get_error_line(), 2, "error line of the header")
	expect_equal(ini.parse("[s]\nno value here\n"), ERR_PARSE_ERROR, "a line without '=' fails")
	expect_equal(ini.get_error_line(), 2, "error line of the key line")
	expect(not ini.get_error_message().is_empty(), "error message of the key line")


func _test_sections() -> void:
	var ini := INI.new()
	ini.set_value("video", "width", 1280)
	ini.set_value("video", "height", 720)
	ini.set_value("audio", "music", 0.8)
	expect(ini.has_section("video"), "has_section()")
	expect(not ini.has_section("input"), "has_section() of a missing section")
	expect(ini.has_section_key("video", "width"), "has_section_key()")
	expect(not ini.has_section_key("video", "depth"), "has_section_key() of a missing key")
	ini.erase_section_key("video", "height")
	expect(not ini.has_section_key("video", "height"), "erase_section_key()")
	ini.set_value("video", "width", null)
	expect(not ini.has_section("video"), "set_value() with null erases the key and the emptied section")
	ini.erase_section("audio")
	expect_equal(ini.get_sections(), PackedStringArray(), "erase_section()")
	expect_equal(ini.data, {}, "data is empty")


func _test_config_file() -> void:
	var ini := INI.new()
	ini.set_value("player", "name", "Ann")
	ini.set_value("player", "pos", Vector2(1, 2))
	var config: ConfigFile = ini.to_config_file()
	expect_equal(config.get_value("player", "name"), "Ann", "to_config_file() copies the values")
	config.set_value("audio", "volume", 0.5)
	var back := INI.new()
	expect_equal(back.from_config_file(config), OK, "from_config_file()")
	expect_equal(back.get_value("player", "pos"), Vector2(1, 2), "the round trip keeps a Vector2")
	expect_equal(back.get_value("audio", "volume"), 0.5, "the round trip keeps a float")


func _test_load_save() -> void:
	var path := ROOT + "settings.ini"
	var ini := INI.new()
	ini.set_value("slot1", "level", 3)
	expect_equal(ini.save(path), OK, "save()")
	expect_equal(FileAccess.get_file_as_string(path), "[slot1]\nlevel=3\n", "save() writes plain INI")
	var other := INI.new()
	expect_equal(other.load(path), OK, "load()")
	expect_equal(other.get_value("slot1", "level"), 3, "load() reads the saved value")
	expect_equal(other.load(ROOT + "missing.ini"), ERR_FILE_NOT_FOUND, "load() of a missing file")
	var saved := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as INI
	expect_equal(saved.get_value("slot1", "level"), 3, "ResourceLoader.load() of a saved INI")


func _test_encrypted() -> void:
	var ini := INI.new()
	ini.set_value("save", "gold", 250)
	var pass_path := ROOT + "pass.ini"
	expect_equal(ini.save_encrypted_pass(pass_path, PASSWORD), OK, "save_encrypted_pass()")
	expect(not FileAccess.get_file_as_string(pass_path).contains("gold"), "the file is encrypted")
	var with_pass := INI.new()
	expect_equal(with_pass.load_encrypted_pass(pass_path, PASSWORD), OK, "load_encrypted_pass()")
	expect_equal(with_pass.get_value("save", "gold"), 250, "load_encrypted_pass() reads the value")

	var key: PackedByteArray = "0123456789abcdef0123456789abcdef".to_utf8_buffer()
	var key_path := ROOT + "key.ini"
	expect_equal(ini.save_encrypted(key_path, key), OK, "save_encrypted()")
	var with_key := INI.new()
	expect_equal(with_key.load_encrypted(key_path, key), OK, "load_encrypted()")
	expect_equal(with_key.get_value("save", "gold"), 250, "load_encrypted() reads the value")


func _test_stringify() -> void:
	var data := {
		"": { "version": 2 },
		"audio": { "volume": 0.5, "device": "Speakers (USB)", "mute": false },
		"video": { "size": Vector2i(1280, 720) },
	}
	var typed: String = INI.stringify(data)
	expect(typed.begins_with("version=2\n"), "stringify(): keys of section \"\" first, without a header")
	expect(typed.contains("[audio]\n"), "stringify(): a section header")
	expect(typed.contains("device=\"Speakers (USB)\""), "stringify(): Godot Variant style quotes strings")
	expect_equal(INI.parse_string(typed), data, "Godot Variant style reads back")
	var plain := SerializerOptions.new()
	plain.ini_value_style = SerializerOptions.INI_VALUE_STYLE_PLAIN
	var plain_text: String = INI.stringify(data, plain)
	expect(plain_text.contains("device=Speakers (USB)"), "stringify() with the Plain style: no quotes")
	expect(plain_text.contains("mute=false"), "stringify() with the Plain style: bool")
	expect_equal(INI.parse_string(plain_text), data, "Plain style reads back")


func _test_value_styles() -> void:
	var settings := INI.new()
	settings.parse("[audio]\ndevice=\"Speakers\"\n")
	expect(settings.encode_to_text().contains("device=\"Speakers\""), "Godot Variant style is the default")
	var plain := SerializerOptions.new()
	plain.ini_value_style = SerializerOptions.INI_VALUE_STYLE_PLAIN
	settings.options = plain
	expect_equal(settings.encode_to_text(), "[audio]\ndevice=Speakers\n", "options.ini_value_style = PLAIN writes device=Speakers")


func _test_serialize() -> void:
	var sword := _make_sword()
	var text: String = INI.serialize(sword)
	expect_equal(text, ResourceTextSerializer.serialize(sword, ResourceTextSerializer.FORMAT_INI), "serialize() is ResourceTextSerializer.serialize(FORMAT_INI)")
	expect(text.begins_with("[root]\n$type=\"Item\"\n$format_version=1\n"), "serialize(): section [root] and $type first")
	var copy := INI.deserialize(text) as Item
	expect_equal(copy.code, "007", "deserialize() restores the resource")
	var shield := INI.deserialize("[root]\nname=\"Shield\"\n", &"Item") as Item
	expect_equal(shield.name, "Shield", "deserialize() with a type hint reads a file without $type")

	var inventory := Inventory.new()
	inventory.main_hand = sword
	inventory.items = [Item.new()]
	var sections: String = INI.serialize(inventory)
	expect(sections.contains("\n[root/main_hand]\n"), "a nested resource is a section [root/main_hand]")
	expect(sections.contains("\n[root/items/0]\n"), "an array element is a section [root/items/0]")

	var plain := SerializerOptions.new()
	plain.ini_value_style = SerializerOptions.INI_VALUE_STYLE_PLAIN
	var plain_text: String = INI.serialize(sword, plain)
	expect(plain_text.contains("\nname=Sword\n"), "Plain style: name=Sword")
	expect(plain_text.contains("\noffset=0.5, -2.0\n"), "Plain style: offset=0.5, -2.0")
	expect(plain_text.contains("\ncode=\"007\"\n"), "Plain style keeps a string that looks like a number quoted")
	var from_plain := INI.deserialize(plain_text, &"", plain) as Item
	expect_equal(from_plain.code, "007", "deserialize() of the Plain style")


## The INI dock is editor UI without own API: these tests cover the API it uses. Sections and keys in file order,
## typed values, an edit as data set to a copy in the same order (rename, undo), empty sections, parse errors.
func _test_dock_api() -> void:
	var ini: INI = load(GAME)
	expect(ini != null, "game.ini loads as INI")
	expect_equal(Array(ini.get_sections()), ["", "player", "audio"], "sections in file order, keys before the first header in \"\"")
	expect_equal(Array(ini.get_section_keys("player")), PLAYER_KEYS, "keys in file order")
	expect_equal(typeof(ini.get_value("player", "lives")), TYPE_INT, "lives is an int")
	expect_equal(typeof(ini.get_value("player", "speed")), TYPE_FLOAT, "speed is a float")
	expect_equal(typeof(ini.get_value("player", "invincible")), TYPE_BOOL, "invincible is a bool")
	expect_equal(ini.get_value("player", "spawn"), Vector2(10, 20), "spawn is a Vector2")

	# Rename a key like the dock: a copy of data with the key replaced at the same position.
	var before: Dictionary = ini.data.duplicate(true)
	var renamed := {}
	var player: Dictionary = before["player"]
	for key: String in player:
		renamed["walk_speed" if key == "speed" else key] = player[key]
	var next: Dictionary = before.duplicate(true)
	next["player"] = renamed
	var changed_count: Array[int] = [0]
	ini.changed.connect(func() -> void: changed_count[0] += 1)
	ini.data = next
	expect_equal(changed_count[0], 1, "setting data emits changed once")
	expect_equal(Array(ini.get_section_keys("player")), ["name", "walk_speed", "lives", "invincible", "spawn"], "a renamed key keeps its position")
	expect(ini.get_parsed_text().is_empty(), "an edit drops the kept text, so the text editor reloads")
	expect(ini.encode_to_text().contains("walk_speed=4.5"), "encode_to_text() shows the renamed key")

	# Undo: the copy taken before the edit.
	ini.data = before
	expect_equal(Array(ini.get_section_keys("player")), PLAYER_KEYS, "undo restores the order")

	# Typed edits and an empty section.
	ini.set_value("player", "lives", 5)
	expect_equal(ini.get_value("player", "lives"), 5, "set_value() keeps the int")
	var with_section: Dictionary = ini.data.duplicate(true)
	with_section["network"] = {}
	ini.data = with_section
	expect(ini.has_section("network"), "an empty section stays in data")
	expect(ini.encode_to_text().contains("[network]"), "an empty section is written with its header")
	ini.set_value("network", "port", 8080)
	ini.erase_section_key("network", "port")
	expect(not ini.has_section("network"), "erase_section_key() removes a section without keys, like ConfigFile")

	# A broken file: the dock does not edit, it shows the error.
	var broken := INI.new()
	broken.parse("[player
name=\"Ann\"
", true)
	expect(not broken.get_error_message().is_empty(), "a broken header reports an error")
	expect(broken.get_error_line() > 0, "the error has a line")
