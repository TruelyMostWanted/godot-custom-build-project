extends FeatureCheck
## GDScript checks of 361-feature-data-file-inspector. Check the same things as scripts/cs/Issue361FeatureDataFileInspectorCheck.cs.
##
## The inspector view of data files is editor UI without own API. These checks cover what it shows and promises:
## the sample files of resources/ load as their format class with the counts of the summary line, `data` (and the
## CSV `header`) stay stored, scriptable properties although the view hides their editors, and units.csv has the
## three schema issues the summary reports. scripts/gd/measure_inspector.gd measures the inspector in the editor.

const RES := "res://361-feature-data-file-inspector/resources/"


func _get_check_name() -> String:
	return "361-feature-data-file-inspector"


func _run_checks() -> void:
	_check_csv()
	_check_tree_formats()
	_check_stored_properties()


func _check_csv() -> void:
	var csv := CSV.new()
	expect(csv.load(RES + "units.csv") == OK, "units.csv loads")
	expect(csv.get_row_count() == 4 and csv.get_column_count() == 4, "units.csv: 4 rows, 4 columns")
	expect(csv.header == PackedStringArray(["name", "class", "hp", "ranged"]), "units.csv header")
	var schema := load(RES + "units_schema.tres") as DataSchema
	expect(schema != null, "units_schema.tres loads")
	if schema != null:
		expect(csv.validate(schema).get_issue_count() == 3, "units.csv has 3 schema issues")


func _check_tree_formats() -> void:
	var json := load(RES + "enemies.json") as JSON
	expect(json != null and json.data is Dictionary and json.data.size() == 2, "enemies.json: Dictionary, 2 keys")
	if json != null and json.data is Dictionary:
		expect(json.data["enemies"].size() == 2 and json.data["boss"] == null, "enemies.json: 2 enemies, boss null")

	var yaml := load(RES + "enemies.yaml") as YAML
	expect(yaml != null and yaml.data is Dictionary and yaml.data.size() == 2, "enemies.yaml: Dictionary, 2 keys")
	if yaml != null and yaml.data is Dictionary:
		expect(typeof(yaml.data["enemies"][0]["hp"]) == TYPE_INT, "enemies.yaml: hp is an int")

	var toml := load(RES + "server.toml") as TOML
	expect(toml != null and toml.data is Dictionary and toml.data.size() == 3, "server.toml: Dictionary, 3 keys")
	if toml != null and toml.data is Dictionary:
		expect(toml.data["maps"].size() == 2, "server.toml: 2 maps")

	var ini := load(RES + "settings.ini") as INI
	expect(ini != null and ini.get_sections() == PackedStringArray(["video", "audio"]), "settings.ini: 2 sections")
	if ini != null:
		expect(ini.get_section_keys("video").size() + ini.get_section_keys("audio").size() == 6, "settings.ini: 6 keys")

	var xml := load(RES + "level.xml") as XML
	expect(xml != null and xml.get_root() != null, "level.xml loads")
	if xml != null and xml.get_root() != null:
		var root := xml.get_root()
		expect(root.name == "level" and root.get_children().size() == 3, "level.xml: root <level>, 3 child elements")
		expect(root.get_attributes().size() == 2, "level.xml: 2 attributes")


func _stored(object: Object) -> PackedStringArray:
	var names := PackedStringArray()
	for property in object.get_property_list():
		if property.usage & PROPERTY_USAGE_STORAGE and property.usage & PROPERTY_USAGE_EDITOR:
			names.push_back(property.name)
	return names


func _check_stored_properties() -> void:
	# The inspector view hides only the editors: the properties stay stored and listed.
	for type_name in ["CSV", "JSON", "YAML", "TOML", "INI", "XML"]:
		expect(ClassDB.class_exists(type_name), "%s exists" % type_name)
		if ClassDB.class_exists(type_name):
			expect("data" in _stored(ClassDB.instantiate(type_name)), "%s.data is stored and in the editor" % type_name)
	expect("header" in _stored(CSV.new()), "CSV.header is stored and in the editor")

	var csv := CSV.new()
	csv.data = [PackedStringArray(["a", "b"])]
	csv.header = PackedStringArray(["x", "y"])
	expect(csv.get_cell(0, 1) == "b" and csv.find_column("y") == 1, "data and header stay scriptable")
