extends FeatureCheck
## GDScript checks of 155-feature-yaml-module. Check the same things as scripts/cs/Issue155FeatureYamlModuleCheck.cs.

const ROOT := "user://155-feature-yaml-module/gd/"
const TITLE := "Main \"menu\": first line\nsecond line"


func _get_check_name() -> String:
	return "155-feature-yaml-module"


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	expect(ClassDB.class_exists("YAML"), "YAML class exists (module enabled)")
	_check_file_round_trip()
	_check_string_api()
	_check_parse()
	_check_errors()
	_check_stringify()
	_cleanup()


func _make_entry(entry_name: String, level: int, mode: int) -> Issue155GdYamlEntry:
	var entry := Issue155GdYamlEntry.new()
	entry.name = entry_name
	entry.level = level
	entry.speed = 0.1 * level
	entry.mode = mode
	entry.offset = Vector2(0.1, -2.5)
	return entry


func _make_settings() -> Issue155GdYamlSettings:
	var s := Issue155GdYamlSettings.new()
	s.volume = 0.8
	s.title = TITLE
	s.main = _make_entry("Ann", 3, Issue155GdYamlEntry.Mode.ANGRY)
	s.main.child = _make_entry("Ann's child", 4, Issue155GdYamlEntry.Mode.HAPPY)
	var cleo := _make_entry("- Cleo, the 2nd", 2, Issue155GdYamlEntry.Mode.HAPPY)
	cleo.child = _make_entry("Dan", 7, Issue155GdYamlEntry.Mode.CALM)
	s.entries = [_make_entry("Bob", 1, Issue155GdYamlEntry.Mode.CALM), cleo]
	s.extra = { "difficulty": "hard", "lives": 3, "key with spaces": true, "looks like a number": "42" }
	s.numbers = PackedInt32Array([4, -7])
	s.tint = Color(1, 0, 0, 1)
	return s


func _same_entry(a: Issue155GdYamlEntry, b: Issue155GdYamlEntry) -> bool:
	return a != null and b != null and a.name == b.name and a.level == b.level and is_equal_approx(a.speed, b.speed) and a.mode == b.mode and a.offset.is_equal_approx(b.offset)


func _check_settings(loaded: Resource, label: String) -> void:
	var e := _make_settings()
	expect(loaded is Issue155GdYamlSettings, "%s: loads as Issue155GdYamlSettings" % label)
	if not loaded is Issue155GdYamlSettings:
		return
	var s: Issue155GdYamlSettings = loaded
	expect(is_equal_approx(s.volume, 0.8) and s.title == TITLE, "%s: float and multi-line string with quotes and ': '" % label)
	expect(_same_entry(s.main, e.main) and _same_entry(s.main.child, e.main.child), "%s: nested sub-resources (two levels)" % label)
	expect(s.spare == null, "%s: null sub-resource stays null" % label)
	expect(s.entries.size() == 2 and _same_entry(s.entries[0], e.entries[0]) and _same_entry(s.entries[1], e.entries[1]), "%s: typed array of resources" % label)
	expect(s.entries.size() == 2 and s.entries[0].child == null and _same_entry(s.entries[1].child, e.entries[1].child), "%s: sub-resource inside an array element" % label)
	expect(s.extra.get("difficulty") == "hard" and int(s.extra.get("lives", 0)) == 3 and s.extra.get("key with spaces") == true, "%s: Dictionary with a key with spaces" % label)
	expect(typeof(s.extra.get("looks like a number")) == TYPE_STRING, "%s: a string that looks like a number stays a string" % label)
	expect(s.numbers == PackedInt32Array([4, -7]) and s.tint.is_equal_approx(Color(1, 0, 0, 1)), "%s: PackedInt32Array and Color" % label)


func _check_file_round_trip() -> void:
	for extension in ["yaml", "yml"]:
		var path: String = ROOT + "settings." + extension
		expect_equal(ResourceSaver.save(_make_settings(), path), OK, "save settings as .%s" % extension)
		expect_equal(ResourceLoader.get_resource_type(path), "Resource", ".%s: get_resource_type() reads \"$type\" (script class -> native base)" % extension)
		_check_settings(ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE), "." + extension)
		_check_settings(ResourceLoader.load(path, "Issue155GdYamlSettings", ResourceLoader.CACHE_MODE_IGNORE), ".%s with type hint" % extension)


func _check_string_api() -> void:
	var text := YAML.serialize(_make_settings())
	expect(text.begins_with("$type: Issue155GdYamlSettings\n$format_version: 1\n"), "type marker first")
	expect(text.find("volume: ") < text.find("title: ") and text.find("title: ") < text.find("\nmain:\n"), "keys follow the property order")
	expect(text.contains("title: |-\n  Main \"menu\": first line\n  second line\n"), "multi-line string as literal block scalar")
	expect(text.contains("\nspare: null\n") and text.contains("\nnumbers: [4, -7]\n"), "null and number arrays")
	expect(text.contains("mode: Angry"), "enums are written as names")
	expect(text.contains("\"- Cleo, the 2nd\"") and text.contains("looks like a number: \"42\""), "strings that YAML would misread are quoted")
	_check_settings(YAML.deserialize(text), "deserialize()")
	# YAML.serialize() is ResourceTextSerializer.serialize(r, FORMAT_YAML).
	expect(ResourceTextSerializer.is_format_available(ResourceTextSerializer.FORMAT_YAML), "is_format_available(FORMAT_YAML)")
	expect_equal(ResourceTextSerializer.serialize(_make_settings(), ResourceTextSerializer.FORMAT_YAML), text, "serialize(FORMAT_YAML) equals YAML.serialize()")
	_check_settings(ResourceTextSerializer.deserialize(text, ResourceTextSerializer.FORMAT_YAML), "ResourceTextSerializer.deserialize(FORMAT_YAML)")
	expect_equal(ResourceTextSerializer.get_format_from_extension("yml"), ResourceTextSerializer.FORMAT_YAML, "get_format_from_extension(\"yml\")")
	var options := SerializerOptions.new()
	options.enums_as_names = false
	var numbers := YAML.serialize(_make_settings(), options)
	expect(numbers.contains("mode: 5"), "option enums_as_names = false writes numbers")
	_check_settings(YAML.deserialize(numbers), "deserialize() with enum numbers")
	var untyped := text.replace("$type: Issue155GdYamlSettings\n", "")
	_check_settings(YAML.deserialize(untyped, &"Issue155GdYamlSettings"), "deserialize() with type hint")
	options = SerializerOptions.new()
	options.sort_keys = true
	options.indent = "    "
	_check_settings(YAML.deserialize(YAML.serialize(_make_settings(), options)), "options sort_keys and indent round trip")


func _check_parse() -> void:
	var source := """# Hand-written
%s
title: YAML # comment
quoted: "tab\\tend"
single: 'it''s'
hex: 0x1F
big: 1e3
up: .inf
yes: yes
none: ~
list:
- one
- {a: 1, b: [true, null]}
window:
  width: 1280
  size: [1280, 720]
text: |
  line 1
  line 2
""" % "---"
	var yaml := YAML.new()
	expect_equal(yaml.parse(source), OK, "parse() a hand-written document")
	var data: Dictionary = yaml.get_data()
	expect(data.get("title") == "YAML" and data.get("quoted") == "tab\tend" and data.get("single") == "it's", "plain, double- and single-quoted scalars")
	expect(data.get("hex") == 31 and typeof(data.get("big")) == TYPE_FLOAT and is_inf(data.get("up", 0.0)), "integers and floats of the core schema")
	expect(data.get("yes") == "yes" and data.get("none") == null and data.has("none"), "yes is a string, ~ is null")
	expect(data.get("list", []).size() == 2 and data["list"][1]["b"] == [true, null], "block sequence with a flow mapping")
	expect(data.get("window", {}).get("size") == [1280, 720], "nested mapping with a flow sequence")
	expect(data.get("text") == "line 1\nline 2\n", "literal block scalar")
	expect(data.keys() == ["title", "quoted", "single", "hex", "big", "up", "yes", "none", "list", "window", "text"], "keys keep the file order")
	expect(YAML.parse_string("a: 1").get("a") == 1, "parse_string()")
	expect(YAML.parse_string("- 1\n- 2\n") == [1, 2], "parse_string() of a sequence")


func _check_errors() -> void:
	var yaml := YAML.new()
	expect_equal(yaml.parse("root:\n  a: 1\n b: 2\n"), ERR_PARSE_ERROR, "broken indentation is a parse error")
	expect_equal(yaml.get_error_line(), 3, "error line of the broken indentation")
	expect(yaml.get_error_column() > 0 and yaml.get_error_message().contains("indentation"), "error column and message of the broken indentation")
	expect_equal(yaml.parse("list: [1, 2\nnext: 3\n"), ERR_PARSE_ERROR, "unclosed flow sequence is a parse error")
	expect_equal(yaml.get_error_line(), 1, "error line of the unclosed flow sequence (where it starts)")
	expect(yaml.get_error_message().contains("not closed"), "error message of the unclosed flow sequence")
	expect_equal(yaml.parse("a: &x 1\nb: *x\n"), ERR_PARSE_ERROR, "anchors and aliases are rejected")
	expect_equal(yaml.parse("a: 1\n---\nb: 2\n"), ERR_PARSE_ERROR, "a second document is rejected")
	expect_equal(yaml.get_error_line(), 2, "error line of the second document")


func _check_stringify() -> void:
	var data := { "name": "Game", "window": { "width": 1280, "scale": 1.5 }, "tags": ["a", "b: c"], "players": [{ "name": "Ann", "level": 3 }], "nothing": null, "flag": "true" }
	var text := YAML.stringify(data)
	expect(text.begins_with("name: Game\nwindow:\n  width: 1280\n  scale: 1.5\ntags:\n  - a\n  - \"b: c\"\nplayers:\n  - name: Ann\n    level: 3\n"), "stringify(): block style in key order")
	expect(text.contains("\nnothing: null\nflag: \"true\"\n"), "stringify(): null and a quoted string that looks like a bool")
	var back = YAML.parse_string(text)
	expect(back == data, "stringify() output reads back as the same data")
	var options := SerializerOptions.new()
	options.indent = ""
	expect_equal(YAML.stringify(data["players"], options), "[{name: Ann, level: 3}]\n", "stringify() with an empty indent writes flow style")


func _cleanup() -> void:
	for file in DirAccess.get_files_at(ROOT):
		DirAccess.remove_absolute(ROOT + file)
	DirAccess.remove_absolute(ROOT)
