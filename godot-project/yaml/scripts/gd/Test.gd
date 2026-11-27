extends FeatureTest
## Full API test of YAML: every method of the YAML class, paths, documents, the parser, the writer and the YAML text of
## a resource. Same tests as scripts/cs/Test.cs.

const ROOT := "user://yaml/gd/"
const ENEMIES := "res://yaml/resources/enemies.yaml"
const HAND_WRITTEN := "# Hand-written\n---\ntitle: YAML # comment\nquoted: \"tab\\tend\"\nsingle: 'it''s'\nhex: 0x1F\nbig: 1e3\nup: .inf\nyes: yes\nnone: ~\nlist:\n- one\n- {a: 1, b: [true, null]}\nwindow:\n  width: 1280\n  size: [1280, 720]\ntext: |\n  line 1\n  line 2\n"
# The text of the wiki section "Save your own resource as YAML".
const SETTINGS_YAML := "$type: GameSettings\n$format_version: 1\ntitle: Space Trader\nquality: High\nresolution: [1920, 1080]\nfullscreen: true\naudio:\n  master_volume: 1.0\n  music_volume: 0.5\n  muted: false\nprofiles:\n  - name: Ann\n    level: 12\n"


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	_test_data_file()
	_test_paths()
	_test_parse()
	_test_parse_errors()
	_test_documents()
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
	var enemies := load(ENEMIES) as YAML
	expect(enemies != null, "load() of a plain .yaml returns a YAML")
	expect_equal(enemies.data.keys(), ["enemies", "boss"], "data keeps the key order of the file")
	expect_equal(typeof(enemies.get_value("enemies/0/hp")), TYPE_INT, "hp: 12 is an int")
	expect_equal(typeof(enemies.get_value("enemies/0/speed")), TYPE_FLOAT, "speed: 1.5 is a float")
	expect_equal(enemies.get_value("enemies/1/flying"), true, "flying: true is a bool")
	expect_equal(enemies.get_value("enemies/0/drops"), ["gel"], "a flow sequence")
	expect(enemies.has_value("boss"), "boss: null has a key")
	expect(enemies.get_value("boss", 1) == null, "boss: null is null")
	expect_equal(ResourceLoader.get_resource_type(ENEMIES), "YAML", "get_resource_type() of a plain .yaml")


func _test_paths() -> void:
	var yaml := YAML.new()
	yaml.parse("enemies:\n  - name: Slime\n\"a/b\": 2\n\"c~d\": 3\n")
	expect_equal(yaml.get_value("enemies/0/name"), "Slime", "get_value() with a path of keys and indices")
	expect_equal(yaml.get_value("enemies/5/name", "none"), "none", "get_value() returns the default")
	expect_equal(yaml.get_value("a~1b"), 2, "~1 is a / in a key")
	expect_equal(yaml.get_value("c~0d"), 3, "~0 is a ~ in a key")
	expect(yaml.has_value("enemies/0"), "has_value()")
	expect(not yaml.has_value("enemies/1"), "has_value() of a missing index")
	yaml.set_value("enemies/0/hp", 12)
	expect_equal(yaml.get_value("enemies/0/hp"), 12, "set_value() adds a key")
	yaml.set_value("enemies/1", { "name": "Ghost", "hp": 20 })
	expect_equal(yaml.get_value("enemies/1/name"), "Ghost", "set_value() with an index equal to the size appends")
	yaml.erase_value("a~1b")
	expect(not yaml.has_value("a~1b"), "erase_value()")


func _test_parse() -> void:
	var yaml := YAML.new()
	expect_equal(yaml.parse(HAND_WRITTEN), OK, "parse() of a hand-written document")
	var data: Dictionary = yaml.data
	expect_equal(data["title"], "YAML", "a plain scalar, the comment is dropped")
	expect_equal(data["quoted"], "tab\tend", "a double-quoted scalar with an escape")
	expect_equal(data["single"], "it's", "a single-quoted scalar")
	expect_equal(data["hex"], 31, "a hex integer")
	expect_equal(typeof(data["big"]), TYPE_FLOAT, "1e3 is a float")
	expect(is_inf(data["up"]), ".inf")
	expect_equal(data["yes"], "yes", "yes is a String (YAML 1.2)")
	expect(data.has("none"), "~ has a key")
	expect(data["none"] == null, "~ is null")
	expect_equal(data["list"][1]["b"], [true, null], "a block sequence with a flow mapping")
	expect_equal(data["window"]["size"], [1280, 720], "a nested mapping with a flow sequence")
	expect_equal(data["text"], "line 1\nline 2\n", "a literal block scalar")
	expect_equal(data.keys(), ["title", "quoted", "single", "hex", "big", "up", "yes", "none", "list", "window", "text"], "keys keep the file order")
	var parsed: Variant = YAML.parse_string("- 1\n- 2\n")
	expect_equal(parsed, [1, 2], "parse_string() of a sequence")


func _test_parse_errors() -> void:
	var yaml := YAML.new()
	expect_equal(yaml.parse("root:\n  a: 1\n b: 2\n"), ERR_PARSE_ERROR, "broken indentation fails")
	expect_equal(yaml.get_error_line(), 3, "get_error_line()")
	expect_equal(yaml.get_error_column(), 2, "get_error_column()")
	expect(yaml.get_error_message().contains("indentation"), "get_error_message()")
	expect_equal(yaml.parse("a:\n\t- 1\n"), ERR_PARSE_ERROR, "a tab in the indentation fails")
	expect_equal(yaml.get_error_message(), "Tabs can't be used for indentation.", "error message of the tab")
	expect_equal(yaml.parse("list: [1, 2\nnext: 3\n"), ERR_PARSE_ERROR, "an unclosed flow sequence fails")
	expect_equal(yaml.get_error_line(), 1, "error line where the flow sequence starts")
	expect(yaml.get_error_message().contains("not closed"), "error message of the unclosed flow sequence")
	expect_equal(yaml.parse("a: &x 1\nb: *x\n"), ERR_PARSE_ERROR, "anchors and aliases are rejected")


func _test_documents() -> void:
	expect_equal(YAML.new().get_document_count(), 1, "a new YAML has one document")
	var yaml := YAML.new()
	expect_equal(yaml.parse("a: 1\n---\n- x\n...\n---\nb: 2\n"), OK, "parse() of several documents")
	expect_equal(yaml.get_document_count(), 3, "get_document_count()")
	expect_equal(yaml.get_value("a"), 1, "data is the first document")
	expect_equal(yaml.get_document(1), ["x"], "get_document()")
	yaml.add_document({ "c": 3 })
	expect_equal(yaml.get_document_count(), 4, "add_document()")
	yaml.remove_document(1)
	expect_equal(yaml.encode_to_text(), "a: 1\n---\nb: 2\n---\nc: 3\n", "documents are written with ---")
	var path := ROOT + "documents.yaml"
	expect_equal(yaml.save(path), OK, "save() of several documents")
	var loaded := YAML.new()
	expect_equal(loaded.load(path), OK, "load() of several documents")
	expect_equal(loaded.get_document_count(), 3, "the documents survive save() and load()")
	yaml.remove_document(0)
	expect_equal(yaml.get_value("b"), 2, "remove_document(0) makes the next document data")
	var first: Variant = YAML.parse_string("a: 1\n---\nb: 2\n")
	expect_equal(first, { "a": 1 }, "parse_string() reads the first document only")


func _test_stringify() -> void:
	var data := { "name": "Game", "window": { "width": 1280, "scale": 1.5 }, "tags": ["a", "b: c"], "players": [{ "name": "Ann", "level": 3 }], "nothing": null, "flag": "true" }
	var text: String = YAML.stringify(data)
	expect(text.begins_with("name: Game\nwindow:\n  width: 1280\n  scale: 1.5\ntags:\n  - a\n  - \"b: c\"\nplayers:\n  - name: Ann\n    level: 3\n"), "stringify(): block style in key order")
	expect(text.contains("\nnothing: null\nflag: \"true\"\n"), "stringify(): null, and quotes for a String that looks like a bool")
	expect_equal(YAML.parse_string(text), data, "stringify() reads back")
	var options := SerializerOptions.new()
	options.indent = ""
	expect_equal(YAML.stringify(data["players"], options), "[{name: Ann, level: 3}]\n", "stringify() with an empty indent writes flow style")


func _test_serialize() -> void:
	var settings := _make_settings()
	var text: String = YAML.serialize(settings)
	expect_equal(text, SETTINGS_YAML, "serialize() writes the text of the wiki guide")
	expect_equal(text, ResourceTextSerializer.serialize(settings, ResourceTextSerializer.FORMAT_YAML), "serialize() is ResourceTextSerializer.serialize(FORMAT_YAML)")
	settings.title = "Main \"menu\": first line\nsecond line"
	var block: String = YAML.serialize(settings)
	expect(block.contains("title: |-\n  Main \"menu\": first line\n  second line\n"), "a String with a line break is a | block")
	settings.title = "- Cleo"
	var dash: String = YAML.serialize(settings)
	expect(dash.contains("title: \"- Cleo\""), "a String that YAML would misread is quoted")
	var options := SerializerOptions.new()
	options.enums_as_names = false
	var numbers: String = YAML.serialize(settings, options)
	expect(numbers.contains("quality: 2"), "enums_as_names = false writes numbers")
	options = SerializerOptions.new()
	options.indent = "    "
	var wide: String = YAML.serialize(_make_settings(), options)
	expect(wide.contains("\n    music_volume: 0.5\n"), "indent sets the spaces of a nesting level")


func _test_deserialize() -> void:
	var copy := YAML.deserialize(SETTINGS_YAML) as GameSettings
	expect_equal(copy.quality, GameSettings.Quality.High, "deserialize(): enum by name")
	expect_equal(copy.resolution, Vector2i(1920, 1080), "deserialize(): Vector2i")
	expect_equal(copy.audio.music_volume, 0.5, "deserialize(): nested resource")
	expect_equal(copy.profiles[0].level, 12, "deserialize(): a sequence of mappings")
	var low := YAML.deserialize("title: Low\nquality: 0\n", &"GameSettings") as GameSettings
	expect_equal(low.quality, GameSettings.Quality.Low, "deserialize() with a type hint, enum by number")
	expect_equal(low.resolution, Vector2i(1280, 720), "missing values keep the defaults")
	var options := SerializerOptions.new()
	options.sort_keys = true
	var sorted: String = YAML.serialize(_make_settings(), options)
	var from_sorted := YAML.deserialize(sorted, &"", options) as GameSettings
	expect_equal(from_sorted.title, "Space Trader", "sort_keys reads back")


func _test_files() -> void:
	for extension: String in ["yaml", "yml"]:
		var path: String = ROOT + "settings." + extension
		expect_equal(ResourceSaver.save(_make_settings(), path), OK, "ResourceSaver.save() to .%s" % extension)
		expect_equal(ResourceLoader.get_resource_type(path), "Resource", ".%s: get_resource_type() reads $type" % extension)
		var loaded := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as GameSettings
		expect_equal(loaded.title, "Space Trader", ".%s with $type loads as its class" % extension)
	var plain_path := ROOT + "plain.yml"
	var plain := YAML.new()
	plain.set_value("name", "Short")
	expect_equal(plain.save(plain_path), OK, "save() of a plain .yml")
	var again := ResourceLoader.load(plain_path, "", ResourceLoader.CACHE_MODE_IGNORE) as YAML
	expect_equal(again.get_value("name"), "Short", "a plain .yml loads as YAML")
	expect(ClassDB.class_exists("YAML"), "the yaml module is built")
