extends FeatureCheck
## GDScript checks of 36-feature-toml-module. Check the same things as scripts/cs/Issue36FeatureTomlModuleCheck.cs.

const ROOT := "user://36-feature-toml-module/gd/"
const TITLE := "Main \"menu\"\nsecond line"


func _get_check_name() -> String:
	return "36-feature-toml-module"


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	expect(ClassDB.class_exists("TOML"), "TOML class exists (module enabled)")
	_check_file_round_trip()
	_check_key_order()
	_check_string_api()
	_check_parse()
	_check_errors()
	_check_stringify()
	_cleanup()


func _make_entry(entry_name: String, level: int, mode: int) -> Issue36GdTomlEntry:
	var entry := Issue36GdTomlEntry.new()
	entry.name = entry_name
	entry.level = level
	entry.speed = 0.1 * level
	entry.mode = mode
	entry.offset = Vector2(0.1, -2.5)
	return entry


func _make_settings() -> Issue36GdTomlSettings:
	var s := Issue36GdTomlSettings.new()
	s.volume = 0.8
	s.title = TITLE
	s.main = _make_entry("Ann", 3, Issue36GdTomlEntry.Mode.ANGRY)
	s.main.child = _make_entry("Ann's child", 4, Issue36GdTomlEntry.Mode.HAPPY)
	var cleo := _make_entry("Cleo, the 2nd", 2, Issue36GdTomlEntry.Mode.HAPPY)
	cleo.child = _make_entry("Dan", 7, Issue36GdTomlEntry.Mode.CALM)
	s.entries = [_make_entry("Bob", 1, Issue36GdTomlEntry.Mode.CALM), cleo]
	s.extra = { "difficulty": "hard", "lives": 3, "key with spaces": true }
	s.numbers = PackedInt32Array([4, -7])
	s.tint = Color(1, 0, 0, 1)
	return s


func _same_entry(a: Issue36GdTomlEntry, b: Issue36GdTomlEntry) -> bool:
	return a != null and b != null and a.name == b.name and a.level == b.level and is_equal_approx(a.speed, b.speed) and a.mode == b.mode and a.offset.is_equal_approx(b.offset)


func _check_settings(loaded: Resource, label: String) -> void:
	var e := _make_settings()
	expect(loaded is Issue36GdTomlSettings, "%s: loads as Issue36GdTomlSettings" % label)
	if not loaded is Issue36GdTomlSettings:
		return
	var s: Issue36GdTomlSettings = loaded
	expect(is_equal_approx(s.volume, 0.8) and s.title == TITLE, "%s: float and multi-line string with quotes" % label)
	expect(_same_entry(s.main, e.main) and _same_entry(s.main.child, e.main.child), "%s: nested sub-resources (two levels)" % label)
	expect(s.spare == null, "%s: null sub-resource stays null" % label)
	expect(s.entries.size() == 2 and _same_entry(s.entries[0], e.entries[0]) and _same_entry(s.entries[1], e.entries[1]), "%s: typed array of resources" % label)
	expect(s.entries.size() == 2 and s.entries[0].child == null and _same_entry(s.entries[1].child, e.entries[1].child), "%s: sub-resource inside an array element" % label)
	expect(s.extra.get("difficulty") == "hard" and int(s.extra.get("lives", 0)) == 3 and s.extra.get("key with spaces") == true, "%s: Dictionary with a quoted key" % label)
	expect(s.numbers == PackedInt32Array([4, -7]) and s.tint.is_equal_approx(Color(1, 0, 0, 1)), "%s: PackedInt32Array and Color" % label)


func _check_file_round_trip() -> void:
	var path := ROOT + "settings.toml"
	expect_equal(ResourceSaver.save(_make_settings(), path), OK, "save settings as .toml")
	expect_equal(ResourceLoader.get_resource_type(path), "Resource", "get_resource_type() reads \"$type\" (script class -> native base)")
	_check_settings(ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE), ".toml")
	_check_settings(ResourceLoader.load(path, "Issue36GdTomlSettings", ResourceLoader.CACHE_MODE_IGNORE), ".toml with type hint")


func _check_key_order() -> void:
	var text := TOML.serialize(_make_settings())
	var order := ["\"$type\" = ", "\"$format_version\" = 1", "volume = ", "title = ", "spare = \"\"", "numbers = [4, -7]", "tint = ", "\n[main]\n", "\n[main.child]\n", "\n[[entries]]\n", "\n[entries.child]\n", "\n[extra]\n"]
	var last := -1
	var ordered := true
	for key in order:
		var at := text.find(key)
		if at <= last:
			ordered = false
			print("  out of order or missing: ", key)
		last = at
	expect(ordered, "keys in the file follow the property order (plain keys, then tables)")
	expect(text.count("\n[[entries]]\n") == 2, "array of resources is written as an array of tables")


func _check_string_api() -> void:
	var text := TOML.serialize(_make_settings())
	expect(text.contains("mode = \"Angry\""), "enums are written as names")
	_check_settings(TOML.deserialize(text), "deserialize()")
	var options := SerializerOptions.new()
	options.enums_as_names = false
	var numbers := TOML.serialize(_make_settings(), options)
	expect(numbers.contains("mode = 5"), "option enums_as_names = false writes numbers")
	_check_settings(TOML.deserialize(numbers), "deserialize() with enum numbers")
	var untyped := text.replace("\"$type\" = \"Issue36GdTomlSettings\"\n", "")
	_check_settings(TOML.deserialize(untyped, &"Issue36GdTomlSettings"), "deserialize() with type hint")
	options = SerializerOptions.new()
	options.include_type_marker = false
	var no_marker := TOML.serialize(_make_settings(), options)
	expect(not no_marker.contains("\"$type\""), "option include_type_marker = false leaves out $type")
	_check_settings(TOML.deserialize(no_marker, &"Issue36GdTomlSettings", options), "deserialize() of text without $type, with type hint")
	options = SerializerOptions.new()
	options.sort_keys = true
	_check_settings(TOML.deserialize(TOML.serialize(_make_settings(), options)), "option sort_keys round trip")
	# The module implements FORMAT_TOML of ResourceTextSerializer.
	expect(ResourceTextSerializer.is_format_available(ResourceTextSerializer.FORMAT_TOML), "is_format_available(FORMAT_TOML)")
	expect_equal(ResourceTextSerializer.serialize(_make_settings(), ResourceTextSerializer.FORMAT_TOML), text, "serialize(FORMAT_TOML) is TOML.serialize()")
	expect_equal(ResourceTextSerializer.get_format_from_extension("toml"), ResourceTextSerializer.FORMAT_TOML, "get_format_from_extension(\"toml\")")


func _check_parse() -> void:
	var source := """# Hand-written
title = "TOML"
literal = 'C:\\path'
hex = 0xff
big = 1_000_000
pi = 3.14
up = inf
when = 1979-05-27T07:32:00Z
list = [1, "two", { three = 3 }]

[window]
width = 1280
size.height = 720

[[players]]
name = "Ann"

[[players]]
name = "Bob"
"""
	var toml := TOML.new()
	expect_equal(toml.parse(source), OK, "parse() a hand-written document")
	var data: Dictionary = toml.get_data()
	expect(data.get("title") == "TOML" and data.get("literal") == "C:\\path", "basic and literal strings")
	expect(data.get("hex") == 255 and data.get("big") == 1000000 and is_equal_approx(data.get("pi", 0.0), 3.14) and is_inf(data.get("up", 0.0)), "integers and floats")
	expect(data.get("when") == "1979-05-27T07:32:00Z", "dates are read as strings")
	expect(data.get("list", []).size() == 3 and data["list"][2]["three"] == 3, "mixed array with inline table")
	expect(data.get("window", {}).get("width") == 1280 and data["window"]["size"]["height"] == 720, "tables and dotted keys")
	expect(data.get("players", []).size() == 2 and data["players"][1]["name"] == "Bob", "arrays of tables")
	expect(data.keys() == ["title", "literal", "hex", "big", "pi", "up", "when", "list", "window", "players"], "keys keep the file order")
	expect(TOML.parse_string("a = 1").get("a") == 1, "parse_string()")


func _check_errors() -> void:
	var toml := TOML.new()
	expect_equal(toml.parse("a = 1\nb = 2\na = 3\n"), ERR_PARSE_ERROR, "duplicate key is a parse error")
	expect_equal(toml.get_error_line(), 3, "error line of the duplicate key")
	expect(toml.get_error_column() > 0 and not toml.get_error_message().is_empty(), "error column and message")
	expect_equal(toml.parse("x = { a = 1 }\n[x.b]\n"), ERR_PARSE_ERROR, "inline tables can't be extended")
	expect_equal(toml.parse("x = \"open\n"), ERR_PARSE_ERROR, "unclosed string")


func _check_stringify() -> void:
	var text := TOML.stringify({ "name": "Game", "window": { "width": 1280, "scale": 1.5 }, "volume": 0.1, "tags": PackedStringArray(["a", "b"]) })
	expect(text.begins_with("name = \"Game\"\nvolume = 0.1\ntags = [\"a\", \"b\"]\n"), "stringify(): plain keys first, in order")
	expect(text.contains("\n[window]\nwidth = 1280\nscale = 1.5\n"), "stringify(): nested Dictionary as table")
	var back := TOML.parse_string(text)
	expect(back.get("window", {}).get("width") == 1280 and is_equal_approx(back.get("volume", 0.0), 0.1), "stringify() output reads back")


func _cleanup() -> void:
	for file in DirAccess.get_files_at(ROOT):
		DirAccess.remove_absolute(ROOT + file)
	DirAccess.remove_absolute(ROOT)
