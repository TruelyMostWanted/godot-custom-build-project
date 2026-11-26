extends FeatureCheck
## GDScript checks of 268-refactoring-text-data-format-resources. Check the same things as scripts/cs/Issue268RefactoringTextDataFormatResourcesCheck.cs.
##
## The sample files in resources/ are plain data files (resources/sample.csv has a .import file with the "Keep File"
## importer, so it is not imported as a translation). load() returns the format resources INI, CSV, XML, TOML and YAML.

const RES := "res://268-refactoring-text-data-format-resources/resources/"
const ROOT := "user://268-refactoring-text-data-format-resources/gd/"


func _get_check_name() -> String:
	return "268-refactoring-text-data-format-resources"


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	_check_api()
	_check_plain_files()
	_check_save_plain()
	_check_ini()
	_check_ini_value_style()
	_check_csv()
	_check_xml()
	_check_toml()
	_check_yaml()
	_check_errors()


func _check_api() -> void:
	for cls in ["INI", "CSV", "XML", "TOML", "YAML"]:
		expect(ClassDB.class_exists(cls) and ClassDB.is_parent_class(cls, "TextDataFormat"), "%s is a TextDataFormat" % cls)
	expect(ClassDB.is_parent_class("TextDataFormat", "Resource"), "TextDataFormat is a Resource")
	expect(not ClassDB.can_instantiate("TextDataFormat"), "TextDataFormat is abstract")
	expect(ClassDB.is_parent_class("XMLElement", "RefCounted"), "XMLElement is RefCounted")
	for method in ["parse", "encode_to_text", "clear", "load", "save", "get_parsed_text", "get_error_message", "get_error_line", "get_error_column", "get_recognized_extensions"]:
		expect(ClassDB.class_has_method("TextDataFormat", method), "TextDataFormat.%s()" % method)
	expect(INI.new().get_recognized_extensions() == PackedStringArray(["ini"]), "INI extensions")
	expect(YAML.new().get_recognized_extensions() == PackedStringArray(["yaml", "yml"]), "YAML extensions include yml")


func _check_plain_files() -> void:
	var ini := load(RES + "sample.ini") as INI
	expect(ini != null, "load() of a plain .ini returns an INI")
	if ini:
		expect_equal(ini.get_value("", "version"), 2, "plain INI value outside a section")
		expect_equal(ini.get_value("video", "size"), Vector2i(1280, 720), "plain INI Godot syntax value")
		expect_equal(ini.get_value("video", "title"), "Demo", "plain INI string")
		expect(ini.get_sections() == PackedStringArray(["", "video", "audio"]), "plain INI sections")

	var csv := load(RES + "sample.csv") as CSV
	expect(csv != null, "load() of a kept .csv returns a CSV")
	if csv:
		expect_equal(csv.get_row_count(), 3, "plain CSV row count")
		expect_equal(csv.get_cell(0, 1), "Ann", "plain CSV cell")

	var xml := load(RES + "sample.xml") as XML
	expect(xml != null, "load() of a plain .xml returns an XML")
	if xml:
		expect_equal(xml.get_root().name, "items", "plain XML root name")

	var toml := load(RES + "sample.toml") as TOML
	expect(toml != null, "load() of a plain .toml returns a TOML")
	if toml:
		expect_equal(toml.get_value("", "title"), "Demo", "plain TOML root key")
		expect_equal(toml.get_value("server", "port"), 8080, "plain TOML table key")

	var yaml := load(RES + "sample.yaml") as YAML
	expect(yaml != null, "load() of a plain .yaml returns a YAML")
	if yaml:
		expect_equal(yaml.get_value("name"), "Demo", "plain YAML value")
	var yml := load(RES + "sample.yml") as YAML
	expect(yml != null, "load() of a plain .yml returns a YAML")
	if yml:
		expect_equal(yml.get_value("name"), "Short", "plain .yml value")


func _save_and_read(resource: Resource, file: String) -> String:
	var path := ROOT + file
	expect_equal(ResourceSaver.save(resource, path), OK, "ResourceSaver.save() to " + file)
	return FileAccess.get_file_as_string(path)


func _check_save_plain() -> void:
	var ini := load(RES + "sample.ini") as INI
	if ini:
		ini.set_value("video", "fps", 60)
		var ini_text := _save_and_read(ini, "saved.ini")
		expect(not ini_text.contains("$type") and ini_text.contains("fps=60"), "saved INI is plain text without $type")
		var again := load(ROOT + "saved.ini") as INI
		expect(again != null and again.get_value("video", "fps") == 60, "saved INI loads as INI again")

	var csv := load(RES + "sample.csv") as CSV
	if csv:
		csv.append_row(PackedStringArray(["4", "Dan", "blue"]))
		var csv_text := _save_and_read(csv, "saved.csv")
		expect(not csv_text.contains("$type") and csv_text.ends_with("4,Dan,blue\n"), "saved CSV is plain text")

	var xml := load(RES + "sample.xml") as XML
	if xml:
		xml.get_root().set_attribute("version", "3")
		var xml_text := _save_and_read(xml, "saved.xml")
		expect(not xml_text.contains("$type") and xml_text.contains("version=\"3\""), "saved XML is plain text")

	var toml := load(RES + "sample.toml") as TOML
	if toml:
		toml.set_value("server", "port", 9090)
		var toml_text := _save_and_read(toml, "saved.toml")
		expect(not toml_text.contains("$type") and toml_text.contains("port = 9090"), "saved TOML is plain text")

	var yaml := load(RES + "sample.yaml") as YAML
	if yaml:
		yaml.set_value("name", "Changed")
		var yaml_text := _save_and_read(yaml, "saved.yaml")
		expect(not yaml_text.contains("$type") and yaml_text.contains("Changed"), "saved YAML is plain text")


func _check_ini() -> void:
	var ini := INI.new()
	ini.set_value("player", "name", "Ann")
	ini.set_value("player", "pos", Vector2(1, 2))
	var config := ini.to_config_file()
	expect(config != null and config.get_value("player", "name") == "Ann", "to_config_file() copies values")
	config.set_value("audio", "volume", 0.5)
	var back := INI.new()
	expect_equal(back.from_config_file(config), OK, "from_config_file()")
	expect_equal(back.get_value("player", "pos"), Vector2(1, 2), "ConfigFile round trip keeps Vector2")
	expect_equal(back.get_value("audio", "volume"), 0.5, "ConfigFile round trip keeps float")
	expect_equal(back.get_value("audio", "missing", 7), 7, "get_value() default")

	# Instance save() and load() like ConfigFile.
	expect_equal(back.save(ROOT + "instance.ini"), OK, "INI.save()")
	var other := INI.new()
	expect_equal(other.load(ROOT + "instance.ini"), OK, "INI.load()")
	expect_equal(other.get_value("player", "name"), "Ann", "INI.load() reads the saved value")

	# Changing data drops the kept text (and with it the comments).
	var commented := INI.new()
	var text := "; comment\n[a]\nb=1\n"
	expect_equal(commented.parse(text, true), OK, "parse() with keep_text")
	expect_equal(commented.get_parsed_text(), text, "keep_text keeps the text")
	commented.set_value("a", "b", 2)
	expect(commented.get_parsed_text().is_empty(), "set_value() drops the kept text")
	expect(not commented.encode_to_text().contains("comment"), "encode_to_text() has no comments after a change")


func _check_ini_value_style() -> void:
	var ini := INI.new()
	ini.set_value("s", "name", "Ann")
	var options := SerializerOptions.new()
	expect_equal(options.ini_value_style, SerializerOptions.INI_VALUE_STYLE_GODOT_VARIANT, "ini_value_style defaults to GODOT_VARIANT")
	ini.options = options
	expect(ini.encode_to_text().contains("name=\"Ann\""), "GODOT_VARIANT quotes strings")
	options.ini_value_style = SerializerOptions.INI_VALUE_STYLE_PLAIN
	expect(ini.encode_to_text().contains("name=Ann\n"), "PLAIN writes strings without quotes")


func _check_csv() -> void:
	var csv := CSV.new()
	expect_equal(csv.parse("id,name,team\n1,Ann,red\n2,Bob,blue\n3,Cleo,red\n"), OK, "CSV.parse()")
	expect(csv.header == PackedStringArray(["id", "name", "team"]), "CSV header")
	expect_equal(csv.get_column_count(), 3, "CSV column count")
	var team := csv.find_column("team")
	expect_equal(team, 2, "find_column()")
	expect_equal(csv.find_column("missing"), -1, "find_column() of a missing name")
	expect(csv.find_rows(team, "red") == PackedInt32Array([0, 2]), "find_rows()")
	expect_equal(csv.get_cell(1, 1), "Bob", "get_cell()")
	expect(csv.get_column(1) == PackedStringArray(["Ann", "Bob", "Cleo"]), "get_column()")
	expect_equal(csv.get_row_as_dictionary(0)["name"], "Ann", "get_row_as_dictionary()")
	csv.set_cell(0, 1, "Anna")
	expect(csv.encode_to_text().contains("1,Anna,red"), "set_cell() is written")


func _check_xml() -> void:
	var xml := XML.new()
	expect_equal(xml.parse("<items version=\"2\"><item id=\"a\">Sword</item><group><item id=\"b\">Shield</item></group></items>"), OK, "XML.parse()")
	var root := xml.get_root()
	expect(root != null and root.name == "items", "get_root() returns the root element")
	expect_equal(root.get_attribute("version"), "2", "get_attribute()")
	expect_equal(root.get_attribute("missing", "none"), "none", "get_attribute() default")
	expect_equal(root.get_children().size(), 2, "get_children()")
	var shield := xml.find_first("group/item")
	expect(shield != null and shield.text == "Shield", "find_first() with a path")
	expect_equal(xml.find_all("item").size(), 1, "find_all() of direct children")
	# Writes through an element change the tree of the XML resource.
	shield.set_attribute("id", "c")
	expect(xml.encode_to_text().contains("id=\"c\""), "attribute written, then encode_to_text()")
	var created := XML.new()
	created.create_root("config").set_attribute("name", "test")
	expect(created.encode_to_text().contains("<config name=\"test\""), "create_root()")


func _check_toml() -> void:
	var toml := TOML.new()
	expect_equal(toml.parse("title = \"Demo\"\n[server]\nport = 8080\n[server.limits]\nmax = 64\n"), OK, "TOML.parse()")
	expect_equal(toml.get_value("server.limits", "max"), 64, "get_value() with a dotted table path")
	expect(toml.has_table("server.limits") and toml.has_table_key("server", "port"), "has_table(), has_table_key()")
	toml.set_value("server.limits", "max", 128)
	expect(toml.encode_to_text().contains("max = 128"), "set_value() with a dotted table path")


func _check_yaml() -> void:
	var yaml := YAML.new()
	expect_equal(yaml.parse("a:\n  - b: 1\n  - b: 2\n\"x/y\": 3\n"), OK, "YAML.parse()")
	expect_equal(yaml.get_value("a/0/b"), 1, "get_value(\"a/0/b\")")
	expect_equal(yaml.get_value("a/1/b"), 2, "get_value(\"a/1/b\")")
	expect_equal(yaml.get_value("x~1y"), 3, "~1 escapes a slash in a key")
	expect(yaml.has_value("a/1") and not yaml.has_value("a/2"), "has_value()")
	yaml.set_value("a/0/b", 10)
	expect_equal(yaml.get_value("a/0/b"), 10, "set_value()")


func _check_errors() -> void:
	var ini := INI.new()
	expect_equal(ini.parse("[broken\n"), ERR_PARSE_ERROR, "INI.parse() of broken text")
	expect(ini.get_error_line() > 0, "INI error line is set")
	expect(not ini.get_error_message().is_empty(), "INI error message is set")
	var toml := TOML.new()
	expect(toml.parse("a = 1\nb = = 2\n") != OK, "TOML.parse() of broken text")
	expect(toml.get_error_line() > 0, "TOML error line is set")
	var xml := XML.new()
	expect(xml.parse("<a><b></a>") != OK and xml.get_error_line() > 0, "XML error line is set")
