extends FeatureTest
## Full API test of TOML: every method of the TOML class, the parser, the writer and the TOML text of a resource.
## Same tests as scripts/cs/Test.cs.

const ROOT := "user://toml/gd/"
const SERVER := "res://toml/resources/server.toml"
const HAND_WRITTEN := "# Hand-written\ntitle = \"TOML\"\nliteral = 'C:\\path'\nhex = 0xff\nbig = 1_000_000\npi = 3.14\nup = inf\nwhen = 1979-05-27T07:32:00Z\nlist = [1, \"two\", { three = 3 }]\n\n[window]\nwidth = 1280\nsize.height = 720\n\n[[players]]\nname = \"Ann\"\n\n[[players]]\nname = \"Bob\"\n"
# The text of the wiki section "Save your own resource as TOML".
const SETTINGS_TOML := "\"$type\" = \"GameSettings\"\n\"$format_version\" = 1\ntitle = \"Space Trader\"\nquality = \"High\"\nresolution = [1920, 1080]\nfullscreen = true\n\n[audio]\nmaster_volume = 1.0\nmusic_volume = 0.5\nmuted = false\n\n[[profiles]]\nname = \"Ann\"\nlevel = 12\n"


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	_test_data_file()
	_test_parse()
	_test_parse_errors()
	_test_tables()
	_test_copy()
	_test_stringify()
	_test_serialize()
	_test_deserialize()
	_test_files()


func _make_settings() -> GameSettings:
	var settings := GameSettings.new()
	settings.title = "Space Trader"
	settings.quality = GameSettings.Quality.High
	settings.resolution = Vector2i(1920, 1080)
	settings.fullscreen = true
	settings.audio = AudioSettings.new()
	var ann := PlayerProfile.new()
	ann.name = "Ann"
	ann.level = 12
	settings.profiles = [ann]
	return settings


func _test_data_file() -> void:
	var config := load(SERVER) as TOML
	expect(config != null, "load() of a plain .toml returns a TOML")
	expect_equal(config.data.keys(), ["title", "server", "maps"], "data keeps the key order of the file")
	expect_equal(config.get_value("", "title"), "Arena", "get_value() of the root table \"\"")
	expect_equal(config.get_value("server", "port"), 8080, "get_value() of a table")
	expect_equal(config.get_value("server.limits", "players"), 16, "get_value() with a dotted table path")
	expect_equal(config.get_value("server.limits", "tick_rate"), 30.0, "a float keeps its type")
	expect_equal(config.get_value("server", "missing", 7), 7, "get_value() returns the default")
	expect_equal(config.data["maps"][1]["name"], "Desert", "[[maps]] is an Array of Dictionaries")
	expect_equal(config.get_tables(), PackedStringArray(["server", "server.limits"]), "get_tables() lists the tables in file order")
	expect_equal(config.get_table_keys("server"), PackedStringArray(["port", "public", "limits"]), "get_table_keys()")


func _test_parse() -> void:
	var toml := TOML.new()
	expect_equal(toml.parse(HAND_WRITTEN), OK, "parse() of a hand-written document")
	var data: Dictionary = toml.data
	expect_equal(data["title"], "TOML", "a basic string")
	expect_equal(data["literal"], "C:\\path", "a literal string")
	expect_equal(data["hex"], 255, "a hex integer")
	expect_equal(data["big"], 1000000, "an integer with underscores")
	expect(is_equal_approx(data["pi"], 3.14), "a float")
	expect(is_inf(data["up"]), "inf")
	expect_equal(data["when"], "1979-05-27T07:32:00Z", "a date stays a String")
	expect_equal(data["list"][2]["three"], 3, "a mixed array with an inline table")
	expect_equal(data["window"]["size"]["height"], 720, "a dotted key")
	expect_equal(data["players"][1]["name"], "Bob", "an array of tables")
	expect_equal(data.keys(), ["title", "literal", "hex", "big", "pi", "up", "when", "list", "window", "players"], "keys keep the file order")
	var parsed: Dictionary = TOML.parse_string("a = 1")
	expect_equal(parsed, { "a": 1 }, "parse_string()")


func _test_parse_errors() -> void:
	var toml := TOML.new()
	expect_equal(toml.parse("a = 1\nb = 2\na = 3\n"), ERR_PARSE_ERROR, "a duplicate key fails")
	expect_equal(toml.get_error_line(), 3, "get_error_line()")
	expect_equal(toml.get_error_column(), 6, "get_error_column()")
	expect_equal(toml.get_error_message(), "The key 'a' is defined twice.", "get_error_message()")
	expect_equal(toml.parse("x = { a = 1 }\n[x.b]\n"), ERR_PARSE_ERROR, "an inline table can't be extended")
	expect_equal(toml.parse("x = \"open\n"), ERR_PARSE_ERROR, "an unclosed string fails")


func _test_tables() -> void:
	var toml := TOML.new()
	toml.set_value("server.limits", "players", 16)
	expect(toml.has_table("server"), "set_value() creates the parent table")
	expect(toml.has_table("server.limits"), "has_table() with a dotted path")
	expect(toml.has_table_key("server.limits", "players"), "has_table_key()")
	expect(not toml.has_table_key("server", "players"), "has_table_key() of another table")
	toml.set_value(r"a\.b", "k", 2)
	expect_equal(toml.data["a.b"], { "k": 2 }, "a\\.b is the one table a.b")
	toml.set_value("", "title", "Arena")
	expect_equal(toml.data["title"], "Arena", "the empty path is the root table")
	toml.erase_table_key("server.limits", "players")
	expect(not toml.has_table_key("server.limits", "players"), "erase_table_key()")
	toml.erase_table("server")
	expect(not toml.has_table("server.limits"), "erase_table() removes the sub-tables too")


func _test_copy() -> void:
	var config := load(SERVER) as TOML
	var mine := TOML.new()
	mine.parse(config.encode_to_text())
	mine.set_value("server.limits", "players", 32)
	expect_equal(config.get_value("server.limits", "players"), 16, "a parsed copy leaves the cached resource unchanged")
	var path := ROOT + "server.toml"
	expect_equal(mine.save(path), OK, "save()")
	var again := TOML.new()
	expect_equal(again.load(path), OK, "load()")
	expect_equal(again.get_value("server.limits", "players"), 32, "load() reads the saved value")


func _test_stringify() -> void:
	var data := { "name": "Game", "window": { "width": 1280, "scale": 1.5 }, "volume": 0.1, "tags": PackedStringArray(["a", "b"]), "nothing": null }
	var text: String = TOML.stringify(data)
	expect(text.begins_with("name = \"Game\"\nvolume = 0.1\ntags = [\"a\", \"b\"]\n"), "stringify(): plain keys first, in order")
	expect(not text.contains("nothing"), "stringify(): a null key is left out (#402)")
	expect(text.contains("\n[window]\nwidth = 1280\nscale = 1.5\n"), "stringify(): a nested Dictionary is a table")
	var back: Dictionary = TOML.parse_string(text)
	expect_equal(back["window"], { "width": 1280, "scale": 1.5 }, "stringify() reads back")


func _test_serialize() -> void:
	var settings := _make_settings()
	var text: String = TOML.serialize(settings)
	expect_equal(text, SETTINGS_TOML, "serialize() writes the text of the wiki guide")
	expect_equal(text, ResourceTextSerializer.serialize(settings, ResourceTextSerializer.FORMAT_TOML), "serialize() is ResourceTextSerializer.serialize(FORMAT_TOML)")
	var options := SerializerOptions.new()
	options.enums_as_names = false
	var numbers: String = TOML.serialize(settings, options)
	expect(numbers.contains("quality = 2"), "enums_as_names = false writes numbers")
	options = SerializerOptions.new()
	options.include_type_marker = false
	var untyped: String = TOML.serialize(settings, options)
	expect(not untyped.contains("$type"), "include_type_marker = false leaves out \"$type\"")
	settings.profiles.append(PlayerProfile.new())
	var two: String = TOML.serialize(settings)
	expect_equal(two.count("\n[[profiles]]\n"), 2, "an array of resources is an array of tables")


func _test_deserialize() -> void:
	var copy := TOML.deserialize(SETTINGS_TOML) as GameSettings
	expect_equal(copy.quality, GameSettings.Quality.High, "deserialize(): enum by name")
	expect_equal(copy.resolution, Vector2i(1920, 1080), "deserialize(): Vector2i")
	expect_equal(copy.audio.music_volume, 0.5, "deserialize(): nested resource")
	expect_equal(copy.profiles[0].level, 12, "deserialize(): array of tables")
	var hand_written := "title = \"Low\"\nquality = 0\n"
	var low := TOML.deserialize(hand_written, &"GameSettings") as GameSettings
	expect_equal(low.quality, GameSettings.Quality.Low, "deserialize() with a type hint, enum by number")
	expect_equal(low.resolution, Vector2i(1280, 720), "missing values keep the defaults")
	var empty := TOML.deserialize("audio = \"\"\nfullscreen = \"\"\n", &"GameSettings") as GameSettings
	expect(empty.audio == null, "an empty string sets an object property to null")
	expect_equal(empty.fullscreen, false, "an empty string keeps the default of a bool property")
	var options := SerializerOptions.new()
	options.sort_keys = true
	var sorted: String = TOML.serialize(_make_settings(), options)
	var from_sorted := TOML.deserialize(sorted, &"", options) as GameSettings
	expect_equal(from_sorted.title, "Space Trader", "sort_keys reads back")


func _test_files() -> void:
	var path := ROOT + "settings.toml"
	expect_equal(ResourceSaver.save(_make_settings(), path), OK, "ResourceSaver.save() to .toml")
	expect_equal(ResourceLoader.get_resource_type(path), "Resource", "get_resource_type() reads \"$type\" (native base of GameSettings)")
	var loaded := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as GameSettings
	expect_equal(loaded.title, "Space Trader", "a .toml with \"$type\" loads as its class")
	expect_equal(ResourceLoader.get_resource_type(SERVER), "TOML", "get_resource_type() of a plain .toml")
	expect(ClassDB.class_exists("TOML"), "the toml module is built")
