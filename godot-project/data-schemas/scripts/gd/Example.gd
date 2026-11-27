extends Node
## Minimal example of Data Schemas: the snippets of the wiki guide, in order, one function per section.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Data-Schemas
## In the editor: the schema in the Inspector, live errors in the script editor and the CSV Table dock (no code).


func _ready() -> void:
	# Using the API
	_using_the_api()
	_build_in_code()
	# Lines, options and the result
	_lines()
	# Validate an INI file
	_validate_ini()
	# Validate JSON
	_validate_json()
	# Validate YAML and TOML
	_validate_yaml()
	# Validate XML
	_validate_xml()
	# Check all data files in one step: an EditorScript, editor only (File > Run in the script editor).
	get_tree().quit()


func _using_the_api() -> void:
	# CSV.new() and load(path) instead of load(): the engine prints a false "Resource file not found" for a Keep File CSV.
	var units := CSV.new()
	units.load("res://csv/resources/units.csv")
	var schema := load("res://csv/resources/units_schema.tres") as DataSchema
	var result := units.validate(schema)
	for i in result.get_issue_count():
		print("Row %d, column %d: %s" % [result.get_issue_row(i), result.get_issue_column(i), result.get_issue_message(i)])


func _build_in_code() -> void:
	var schema := DataSchema.create_from_dictionary({ "name": TYPE_STRING, "hp": TYPE_INT, "ranged": TYPE_BOOL })
	schema.get_field("name").unique = true
	schema.get_field("hp").set_range(1, 200)
	var unit_class := DataField.new()
	unit_class.name = &"class"
	unit_class.set_enum_values(PackedStringArray(["warrior", "ranger", "caster"]))
	schema.add_field(unit_class)
	print("Fields: ", schema.fields.size())


func _lines() -> void:
	var schema := load("res://csv/resources/units_schema.tres") as DataSchema
	var options := DataValidationOptions.new()
	options.keep_positions = true
	var units := CSV.new()
	units.parse(FileAccess.get_file_as_string("res://csv/resources/units.csv"), true)
	var result := units.validate(schema, options)
	print(result.get_issue_line(0))


func _validate_ini() -> void:
	var section := DataField.new()
	section.name = &"section"
	section.type = TYPE_DICTIONARY
	section.schema = DataSchema.create_from_dictionary({ "hp": TYPE_INT, "name": TYPE_STRING })
	var schema := DataSchema.new()
	schema.extra_fields = DataSchema.EXTRA_FIELDS_MATCH_TEMPLATE
	schema.extra_field_template = section
	var result := (load("res://data-schemas/resources/monsters.ini") as INI).validate(schema)
	for i in result.get_issue_count():
		print(result.get_issue_path(i), ": ", result.get_issue_message(i))


func _validate_json() -> void:
	var schema := DataSchema.create_from_dictionary({ "name": TYPE_STRING, "hp": TYPE_INT, "drops": [TYPE_STRING] })
	var text := FileAccess.get_file_as_string("res://json/resources/enemies.json")
	var result := schema.validate(JSON.parse_string(text)["enemies"])
	result.resolve_lines(text)
	for i in result.get_issue_count():
		print("Line ", result.get_issue_line(i), ": ", result.get_issue_message(i))


func _validate_yaml() -> void:
	var schema := DataSchema.create_from_dictionary({ "enemies": [{ "name": TYPE_STRING, "hp": TYPE_INT }] })
	schema.extra_fields = DataSchema.EXTRA_FIELDS_ALLOW
	schema.get_field("enemies").element_schema.extra_fields = DataSchema.EXTRA_FIELDS_ALLOW
	var options := DataValidationOptions.new()
	var result := (load("res://yaml/resources/enemies.yaml") as YAML).validate(schema, options)
	print("enemies.yaml valid: ", result.is_valid())


func _validate_xml() -> void:
	var spawn := DataSchema.create_from_dictionary({ "enemy": TYPE_STRING, "x": TYPE_INT, "y": TYPE_INT })
	var spawns := DataField.new()
	spawns.name = &"spawn"
	spawns.type = TYPE_ARRAY
	spawns.element_schema = spawn
	var level := DataSchema.create_from_dictionary({ "name": TYPE_STRING, "time": TYPE_INT, "music": TYPE_STRING })
	level.add_field(spawns)
	var result := (load("res://xml/resources/level.xml") as XML).validate(level)
	print("level.xml valid: ", result.is_valid())
