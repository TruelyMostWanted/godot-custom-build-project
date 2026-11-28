extends FeatureTest
## Test of Data File Inspector. The view is editor UI without an API of its own; this tests what it shows and promises,
## from the data: the counts of the summary, the parse status, the schema issues, the types of the tree and that data
## (and the CSV header) stay stored, editable properties. Same tests as scripts/cs/Test.cs.

const UNITS := "res://csv/resources/units.csv"
const UNITS_SCHEMA := "res://csv/resources/units_schema.tres"
const BROKEN := "res://data-file-inspector/resources/broken.json"
# The output of the wiki section "A data check before the game starts".
const ISSUES := [
	"row 3, column 1: \"class\": \"unknown\" is not one of: warrior,ranger,caster.",
	"row 3, column 2: \"hp\": 500 is outside the range 1.0 to 200.0.",
	"row 3, column 3: \"ranged\" must be of type bool, not \"maybe\".",
]


func _run_tests() -> void:
	_test_summaries()
	_test_parse_status()
	_test_schema_status()
	_test_tree_types()
	_test_stored_properties()
	_test_per_format()


func _stored(object: Object) -> PackedStringArray:
	var names := PackedStringArray()
	for property: Dictionary in object.get_property_list():
		if property.usage & PROPERTY_USAGE_STORAGE and property.usage & PROPERTY_USAGE_EDITOR:
			names.append(property.name)
	return names


func _test_summaries() -> void:
	var units := CSV.new()
	units.load(UNITS)
	expect_equal(units.get_row_count(), 4, "CSV: 4 rows")
	expect_equal(units.get_column_count(), 4, "CSV: 4 columns")
	var settings := load("res://ini/resources/settings.ini") as INI
	expect_equal(settings.get_sections().size(), 2, "INI: 2 sections")
	var keys: int = settings.get_section_keys("video").size() + settings.get_section_keys("audio").size()
	expect_equal(keys, 6, "INI: 6 keys")
	var level := load("res://xml/resources/level.xml") as XML
	expect_equal(level.get_root().name, "level", "XML: root element <level>")
	expect_equal(level.get_root().get_children().size(), 3, "XML: 3 child nodes")
	var enemies := load("res://json/resources/enemies.json") as JSON
	expect_equal(enemies.data.size(), 2, "JSON: Dictionary, 2 keys")
	var server := load("res://toml/resources/server.toml") as TOML
	expect_equal(server.data.size(), 3, "TOML: Dictionary, 3 keys")
	var yaml := load("res://yaml/resources/enemies.yaml") as YAML
	expect_equal(yaml.data.size(), 2, "YAML: Dictionary, 2 keys")


func _test_parse_status() -> void:
	var json := JSON.new()
	expect_equal(json.parse(FileAccess.get_file_as_string(BROKEN)), ERR_PARSE_ERROR, "broken.json does not parse")
	expect_equal(json.get_error_line() + 1, 4, "the parse error of broken.json is on line 4 (get_error_line() counts from 0)")
	expect(json.data == null, "broken.json has no data, so the tree stays empty")


func _test_schema_status() -> void:
	var units := CSV.new()
	units.load(UNITS)
	var schema := load(UNITS_SCHEMA) as DataSchema
	expect(UNITS.match(schema.file_patterns[0]), "units_schema.tres lists units.csv in file_patterns")
	var result: DataValidation = units.validate(schema)
	var lines := []
	for i in result.get_issue_count():
		lines.append("row %d, column %d: %s" % [result.get_issue_row(i), result.get_issue_column(i), result.get_issue_message(i)])
	expect_equal(lines, ISSUES, "units.csv: the three issues of the wiki guide")
	var enemies := load("res://json/resources/enemies.json") as JSON
	var slime: Dictionary = enemies.data["enemies"][0]
	expect_equal(typeof(slime["hp"]), TYPE_FLOAT, "a JSON number is a float (Slime 12.0)")


func _test_tree_types() -> void:
	var yaml := load("res://yaml/resources/enemies.yaml") as YAML
	expect_equal(typeof(yaml.get_value("enemies/0/hp")), TYPE_INT, "YAML: hp is an int")
	var settings := load("res://ini/resources/settings.ini") as INI
	expect_equal(typeof(settings.get_value("video", "width")), TYPE_INT, "INI: width is an int")
	expect_equal(typeof(settings.get_value("video", "vsync")), TYPE_BOOL, "INI: vsync is a bool")
	expect_equal(typeof(settings.get_value("audio", "music")), TYPE_FLOAT, "INI: music is a float")
	expect_equal(typeof(settings.get_value("audio", "device")), TYPE_STRING, "INI: device is a String")
	var server := load("res://toml/resources/server.toml") as TOML
	expect_equal(typeof(server.data["maps"]), TYPE_ARRAY, "TOML: [[maps]] is an Array")
	var level := load("res://xml/resources/level.xml") as XML
	expect_equal(level.get_root().get_attributes().size(), 2, "XML: <level> has 2 attributes (@name, @time)")


func _test_stored_properties() -> void:
	for format_class: String in ["CSV", "JSON", "YAML", "TOML", "INI", "XML"]:
		var instance: Object = ClassDB.instantiate(format_class)
		expect(_stored(instance).has("data"), "%s.data is stored and in the editor (Raw Properties)" % format_class)
	expect(_stored(CSV.new()).has("header"), "CSV.header is stored and in the editor")
	var csv := CSV.new()
	csv.data = [PackedStringArray(["a", "b"])]
	csv.header = PackedStringArray(["x", "y"])
	expect_equal(csv.get_cell(0, 1), "b", "data stays scriptable")
	expect_equal(csv.find_column("y"), 1, "header stays scriptable")


const INI_TEXT := "version=2\n\n[player]\nname=\"knight\"\nhp=100\n\n[audio]\nvolume=0.8\n"
const XML_TEXT := "<units version=\"1\"><unit id=\"knight\" hp=\"100\"/><unit id=\"archer\" hp=\"80\"/></units>\n"
const YAML_TEXT := "name: knight\n---\nname: archer\n---\n- 1\n- 2\n"
const TOML_TEXT := "title = \"Game\"\n\n[server]\nport = 8080\n\n[[unit]]\nname = \"knight\"\n\n[[unit]]\nname = \"archer\"\n"
const CSV_TEXT := "name,hp\nknight,100\narcher,80\n"


func _test_per_format() -> void:
	_test_ini()
	_test_xml()
	_test_yaml()
	_test_toml()
	_test_csv()
	_test_text_document()


func _test_ini() -> void:
	var ini := INI.new()
	expect(ini.parse(INI_TEXT) == OK, "INI parses")
	var sections: Dictionary = ini.data
	expect(sections.size() == 3, "INI: 3 sections (one without a name)")
	expect(sections.has(""), "INI: keys without a section are in section \"\"")
	expect(ini.get_value("", "version") == 2, "INI: version without a section")
	expect(typeof(ini.get_value("player", "hp")) == TYPE_INT, "INI: hp is an int")
	expect(typeof(ini.get_value("audio", "volume")) == TYPE_FLOAT, "INI: volume is a float")


func _test_xml() -> void:
	var xml := XML.new()
	expect(xml.parse(XML_TEXT) == OK, "XML parses")
	var root: Dictionary = xml.data
	expect(root["name"] == "units", "XML: root element units")
	expect(root["children"].size() == 2, "XML: 2 child nodes")
	expect(root["children"][1]["attributes"]["id"] == "archer", "XML: attribute id of the second unit")


func _test_yaml() -> void:
	var yaml := YAML.new()
	expect(yaml.parse(YAML_TEXT) == OK, "YAML parses")
	expect(yaml.get_document_count() == 3, "YAML: 3 documents")
	expect(yaml.data["name"] == "knight", "YAML: data is the first document")
	expect(yaml.get_document(1)["name"] == "archer", "YAML: second document")
	expect(yaml.get_document(2).size() == 2, "YAML: third document has 2 items")


func _test_toml() -> void:
	var toml := TOML.new()
	expect(toml.parse(TOML_TEXT) == OK, "TOML parses")
	var root: Dictionary = toml.data
	expect(root["server"] is Dictionary, "TOML: [server] is a table")
	expect(root["unit"] is Array, "TOML: [[unit]] is an array")
	expect(root["unit"].size() == 2, "TOML: 2 units")
	expect(root["unit"][1] is Dictionary, "TOML: each unit is a table")
	expect(root["title"] == "Game", "TOML: title is a key")


func _test_csv() -> void:
	var csv := CSV.new()
	expect(csv.parse(CSV_TEXT) == OK, "CSV parses")
	expect(csv.header == PackedStringArray(["name", "hp"]), "CSV: header stays stored")
	expect(csv.get_row_count() == 2, "CSV: 2 rows")
	expect(csv.get_column_count() == 2, "CSV: 2 columns")


func _test_text_document() -> void:
	var texts := {
		"CSV": CSV_TEXT,
		"INI": INI_TEXT,
		"XML": XML_TEXT,
		"TOML": TOML_TEXT,
		"YAML": YAML_TEXT,
	}
	for cls in texts:
		var format = ClassDB.instantiate(cls)
		expect(format is TextDataFormat, cls + " is a TextDataFormat")
		expect(format.parse(texts[cls], true) == OK, cls + " parses with kept text")
		expect(format.get_parsed_text() == texts[cls], cls + " keeps the parsed text")
		expect(not format.encode_to_text().is_empty(), cls + " encodes its data")
		expect(not format.get_recognized_extensions().is_empty(), cls + " has file extensions")

	var broken := {
		"INI": "[broken\n",
		"XML": "<a>",
		"TOML": "[a\n",
		"YAML": "a: [1,\n",
	}
	for cls in broken:
		var format = ClassDB.instantiate(cls)
		expect(format.parse(broken[cls]) != OK, cls + " reports a parse error")
		expect(not format.get_error_message().is_empty(), cls + " has an error message")
		expect(format.get_error_line() > 0, cls + " has an error line")
