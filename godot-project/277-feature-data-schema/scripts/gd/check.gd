extends FeatureCheck
## GDScript checks of 277-feature-data-schema. Check the same things as scripts/cs/Issue277FeatureDataSchemaCheck.cs.
##
## items.csv has a header, valid rows, invalid rows and one quoted multi-line cell. items_schema.tres is a DataSchema made as
## an inspector resource. Rows are 0-based data rows (header excluded), lines are 1-based.

const RES := "res://277-feature-data-schema/resources/"


func _get_check_name() -> String:
	return "277-feature-data-schema"


func _run_checks() -> void:
	_check_api()
	_check_schema_resource()
	_check_csv_file()
	_check_csv_parse_positions()
	_check_dictionary_and_options()
	_check_json()
	_check_ini()
	_check_xml()
	_check_nested_and_yaml()
	_check_typed_csv()
	_check_many_rows()


func _field(field_name: String, type: int) -> DataField:
	var field := DataField.new()
	field.name = field_name
	field.type = type
	return field


## id (int, unique, required), name (not empty), rarity (enum), hp (0..100, default 10), code (pattern).
func _item_schema() -> DataSchema:
	var schema := DataSchema.new()
	var id_field := _field("id", TYPE_INT)
	id_field.required = true
	id_field.unique = true
	schema.add_field(id_field)
	var name_field := _field("name", TYPE_STRING)
	name_field.allow_empty = false
	schema.add_field(name_field)
	var rarity := _field("rarity", TYPE_STRING)
	rarity.set_enum_values(PackedStringArray(["common", "rare"]))
	schema.add_field(rarity)
	var hp := _field("hp", TYPE_INT)
	hp.set_range(0, 100)
	hp.default_value = 10
	schema.add_field(hp)
	var code := _field("code", TYPE_STRING)
	code.pattern = "[A-Z]{2}[0-9]+"
	schema.add_field(code)
	return schema


func _has_issue(validation: DataValidation, kind: int, field: String, row: int = -1) -> bool:
	for i in validation.get_issue_count():
		if validation.get_issue_kind(i) == kind and str(validation.get_issue_field(i)) == field and validation.get_issue_row(i) == row:
			return true
	return false


func _check_api() -> void:
	for cls in ["DataField", "DataSchema", "DataValidationOptions"]:
		expect(ClassDB.class_exists(cls) and ClassDB.is_parent_class(cls, "Resource"), "%s is a Resource" % cls)
	expect(ClassDB.class_exists("DataValidation") and ClassDB.is_parent_class("DataValidation", "RefCounted"), "DataValidation is RefCounted")
	for method in ["set_range", "set_enum_values"]:
		expect(ClassDB.class_has_method("DataField", method), "DataField.%s()" % method)
	for method in ["get_field", "find_field", "add_field", "remove_field", "validate", "convert_value", "create_from_dictionary"]:
		expect(ClassDB.class_has_method("DataSchema", method), "DataSchema.%s()" % method)
	for method in ["is_valid", "get_issue_count", "get_issue_kind", "get_issue_message", "get_issue_field", "get_issue_path", "get_issue_row", "get_issue_column", "get_issue_line", "get_invalid_rows", "add_issue", "resolve_lines"]:
		expect(ClassDB.class_has_method("DataValidation", method), "DataValidation.%s()" % method)
	expect(ClassDB.class_has_method("TextDataFormat", "validate"), "TextDataFormat.validate()")
	for method in ["get_typed_row", "append_typed_row", "get_typed_column"]:
		expect(ClassDB.class_has_method("CSV", method), "CSV.%s()" % method)
	var schema_probe := DataSchema.new()
	for property in ["fields", "extra_fields", "extra_field_template", "file_patterns"]:
		expect(property in schema_probe, "DataSchema.%s" % property)
	var options_probe := DataValidationOptions.new()
	for property in ["keep_positions", "max_issues", "stop_on_first"]:
		expect(property in options_probe, "DataValidationOptions.%s" % property)
	var field := DataField.new()
	expect_equal(field.type, TYPE_STRING, "DataField default type is String")
	expect(not field.required and not field.unique and field.allow_empty, "DataField defaults")
	expect_equal(schema_probe.extra_fields, DataSchema.EXTRA_FIELDS_REJECT, "extra_fields defaults to reject")
	expect_equal(DataValidation.POSITION_NONE, -1, "POSITION_NONE")


func _check_schema_resource() -> void:
	var schema := load(RES + "items_schema.tres") as DataSchema
	expect(schema != null, "items_schema.tres loads as a DataSchema")
	if schema == null:
		return
	expect_equal(schema.fields.size(), 5, "schema resource has five fields")
	expect_equal(schema.find_field("hp"), 3, "find_field() of hp")
	expect_equal(schema.find_field("missing"), -1, "find_field() of a missing field")
	var id_field := schema.get_field("id")
	expect(id_field != null and id_field.required and id_field.unique and id_field.type == TYPE_INT, "id field: int, required, unique")
	var hp := schema.get_field("hp")
	expect(hp != null and hp.hint == PROPERTY_HINT_RANGE and hp.hint_string == "0,100,1" and hp.default_value == 10, "hp field: range and default")
	var rarity := schema.get_field("rarity")
	expect(rarity != null and rarity.hint == PROPERTY_HINT_ENUM and rarity.hint_string == "common,rare", "rarity field: enum")
	var name_field := schema.get_field("name")
	expect(name_field != null and not name_field.allow_empty, "name field: not empty")
	expect_equal(schema.convert_value("hp", "42"), 42, "convert_value() to int")
	expect_equal(schema.convert_value("name", "abc"), "abc", "convert_value() to String")


func _check_csv_file() -> void:
	var schema := load(RES + "items_schema.tres") as DataSchema
	var csv := load(RES + "items.csv") as CSV
	expect(csv != null, "load() of items.csv returns a CSV")
	if csv == null or schema == null:
		return
	expect_equal(csv.get_row_count(), 4, "items.csv has four data rows")
	expect_equal(csv.get_cell(1, 1).replace("\r", ""), "Big\nAxe", "multi-line quoted cell")
	var result := csv.validate(schema)
	expect(not result.is_valid(), "items.csv is not valid")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_DUPLICATE_KEY, "id", 2), "duplicate id in row 2")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_EMPTY_REQUIRED, "name", 2), "empty name in row 2")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_NOT_IN_ENUM, "rarity", 2), "rarity epic in row 2")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_TYPE_MISMATCH, "hp", 2), "hp x in row 2")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_PATTERN_MISMATCH, "code", 2), "code ab in row 2")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_OUT_OF_RANGE, "hp", 3), "hp 500 in row 3")
	expect_equal(result.get_issue_count(), 6, "six issues in items.csv")
	expect(result.get_invalid_rows() == PackedInt32Array([2, 3]), "invalid rows of items.csv")
	for i in result.get_issue_count():
		expect(result.get_issue_message(i) != "", "issue %d has a message" % i)
		expect_equal(result.get_issue_line(i), -1, "no line without keep_positions (issue %d)" % i)


func _check_csv_parse_positions() -> void:
	var schema := _item_schema()
	var text := FileAccess.get_file_as_string(RES + "items.csv").replace("\r\n", "\n")
	expect(text.begins_with("id,name"), "items.csv text is readable")
	var csv := CSV.new()
	expect_equal(csv.parse(text, true), OK, "CSV.parse() keeping the text")
	var options := DataValidationOptions.new()
	options.keep_positions = true
	var result := csv.validate(schema, options)
	expect_equal(result.get_issue_count(), 6, "six issues with positions")
	# Row 1 spans lines 3 and 4, so row 2 is on line 5 and row 3 on line 6.
	for i in result.get_issue_count():
		var row := result.get_issue_row(i)
		var expected_line := 5 if row == 2 else 6
		expect_equal(result.get_issue_line(i), expected_line, "line of the issue in row %d" % row)
		if str(result.get_issue_field(i)) == "hp":
			expect_equal(result.get_issue_column(i), 3, "hp is column 3")

	# An unknown column and a missing required column.
	var extra := CSV.new()
	extra.parse("id,name,colour\n1,Sword,red\n")
	var extra_result := extra.validate(schema)
	expect(_has_issue(extra_result, DataValidation.ISSUE_KIND_UNKNOWN_FIELD, "colour"), "unknown column colour")
	expect_equal(extra_result.get_issue_count(), 1, "only the unknown column is reported")
	var missing := CSV.new()
	missing.parse("name\nSword\n")
	expect(_has_issue(missing.validate(schema), DataValidation.ISSUE_KIND_MISSING_FIELD, "id"), "missing required column id")
	# A parse error is the only issue.
	var broken := CSV.new()
	broken.parse("id\n\"open\n")
	var broken_result := broken.validate(schema)
	expect_equal(broken_result.get_issue_count(), 1, "a parse error is the only issue")
	if broken_result.get_issue_count() == 1:
		expect_equal(broken_result.get_issue_kind(0), DataValidation.ISSUE_KIND_PARSE_ERROR, "parse error kind")

	var valid := CSV.new()
	valid.parse("id,name,rarity,hp,code\n1,Sword,common,10,AB1\n2,Axe,rare,,CD2\n")
	expect(valid.validate(schema).is_valid(), "valid CSV is valid (empty hp is fine)")


func _check_dictionary_and_options() -> void:
	var schema := _item_schema()
	var data := {"name": "", "rarity": "epic", "hp": 150, "code": "x1", "colour": "red"}
	var result := schema.validate(data)
	expect(not result.is_valid(), "invalid dictionary")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_MISSING_FIELD, "id"), "missing field id")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_EMPTY_REQUIRED, "name"), "empty name")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_NOT_IN_ENUM, "rarity"), "rarity not in enum")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_OUT_OF_RANGE, "hp"), "hp out of range")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_PATTERN_MISMATCH, "code"), "code pattern mismatch")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_UNKNOWN_FIELD, "colour"), "unknown field colour")
	expect_equal(result.get_issue_count(), 6, "six issues for the dictionary")

	data.erase("colour")
	data["id"] = "abc"
	result = schema.validate(data)
	expect(_has_issue(result, DataValidation.ISSUE_KIND_TYPE_MISMATCH, "id"), "id is not an int")
	expect(not _has_issue(result, DataValidation.ISSUE_KIND_UNKNOWN_FIELD, "colour"), "colour is gone")

	schema.extra_fields = DataSchema.EXTRA_FIELDS_ALLOW
	data["colour"] = "red"
	expect(not _has_issue(schema.validate(data), DataValidation.ISSUE_KIND_UNKNOWN_FIELD, "colour"), "extra_fields allow")

	# Options: stop_on_first and max_issues.
	var limited := {"hp": 500, "code": "?"}
	var strict := _item_schema()
	var options := DataValidationOptions.new()
	options.stop_on_first = true
	expect_equal(strict.validate(limited, options).get_issue_count(), 1, "stop_on_first gives one issue")
	options.stop_on_first = false
	options.max_issues = 2
	expect_equal(strict.validate(limited, options).get_issue_count(), 2, "max_issues caps the issues")
	expect(strict.validate(limited).get_issue_count() > 2, "without options there are more issues")


func _check_json() -> void:
	var text := FileAccess.get_file_as_string(RES + "items.json").replace("\r\n", "\n")
	var data: Variant = JSON.parse_string(text)
	expect(data is Array, "items.json parses to an Array")
	if not (data is Array):
		return
	var stats := {"atk": TYPE_INT}
	var drop := {"item": TYPE_STRING, "count": TYPE_INT}
	var shape := {"id": TYPE_INT, "name": TYPE_STRING, "stats": stats, "tags": [TYPE_STRING], "drops": [drop]}
	var schema := DataSchema.create_from_dictionary(shape)
	expect_equal(schema.fields.size(), 5, "create_from_dictionary() makes five fields")
	expect(schema.get_field("id").required, "created fields are required")
	schema.get_field("id").unique = true
	var result := schema.validate(data)
	expect(_has_issue(result, DataValidation.ISSUE_KIND_DUPLICATE_KEY, "id", 1), "duplicate id in record 1")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_TYPE_MISMATCH, "atk", 1), "nested atk is not an int")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_TYPE_MISMATCH, "tags", 1), "tags element is not a string")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_TYPE_MISMATCH, "count", 1), "count in drops is not an int")
	expect_equal(result.get_issue_count(), 4, "four issues in items.json")
	expect(result.get_invalid_rows() == PackedInt32Array([1]), "invalid JSON records")
	var paths := PackedStringArray()
	for i in result.get_issue_count():
		paths.append(result.get_issue_path(i))
	expect("1/drops/0/count" in paths, "path of the nested count issue (%s)" % ", ".join(paths))
	result.resolve_lines(text)
	var lines_ok := true
	for i in result.get_issue_count():
		lines_ok = lines_ok and result.get_issue_line(i) == 3
	expect(lines_ok, "resolve_lines() puts every issue on line 3")
	# The first record alone is valid.
	expect(schema.validate((data as Array).slice(0, 1)).is_valid(), "first record is valid")


func _check_ini() -> void:
	var section := {"hp": TYPE_INT, "name": TYPE_STRING}
	var every_section := _field("section", TYPE_DICTIONARY)
	every_section.schema = DataSchema.create_from_dictionary(section)
	var schema := DataSchema.new()
	schema.extra_fields = DataSchema.EXTRA_FIELDS_MATCH_TEMPLATE
	schema.extra_field_template = every_section

	var ini := load(RES + "monsters.ini") as INI
	expect(ini != null, "load() of monsters.ini returns an INI")
	if ini:
		var result := ini.validate(schema)
		expect(_has_issue(result, DataValidation.ISSUE_KIND_MISSING_FIELD, "name"), "orc has no name")
		expect(_has_issue(result, DataValidation.ISSUE_KIND_TYPE_MISMATCH, "hp"), "orc hp is not an int")
		expect_equal(result.get_issue_count(), 2, "two issues in monsters.ini")
		var paths := PackedStringArray()
		for i in result.get_issue_count():
			paths.append(result.get_issue_path(i))
		expect("orc/hp" in paths and "orc" in paths, "paths orc and orc/hp (%s)" % ", ".join(paths))

	var parsed := INI.new()
	var ini_text := FileAccess.get_file_as_string(RES + "monsters.ini").replace("\r\n", "\n")
	expect_equal(parsed.parse(ini_text, true), OK, "INI.parse() keeping the text")
	var options := DataValidationOptions.new()
	options.keep_positions = true
	var with_lines := parsed.validate(schema, options)
	expect_equal(with_lines.get_issue_count(), 2, "two issues with positions")
	for i in with_lines.get_issue_count():
		if with_lines.get_issue_kind(i) == DataValidation.ISSUE_KIND_TYPE_MISMATCH:
			expect_equal(with_lines.get_issue_path(i), "orc/hp", "type mismatch path")
			expect_equal(with_lines.get_issue_line(i), 6, "type mismatch is on line 6")
		else:
			expect_equal(with_lines.get_issue_path(i), "orc", "missing field path")
			expect_equal(with_lines.get_issue_line(i), 5, "missing field is on line 5")
	# Without a template the sections are unknown fields.
	var unknown := parsed.validate(DataSchema.new())
	expect(_has_issue(unknown, DataValidation.ISSUE_KIND_UNKNOWN_FIELD, "goblin") and _has_issue(unknown, DataValidation.ISSUE_KIND_UNKNOWN_FIELD, "orc"), "sections are unknown fields without a template")


func _check_xml() -> void:
	var xml := XML.new()
	expect_equal(xml.parse("<items><item id=\"1\"><name>Sword</name></item><item id=\"x\"/></items>"), OK, "XML.parse()")
	var item := {"id": TYPE_INT}
	var item_schema := DataSchema.create_from_dictionary(item)
	item_schema.add_field(_field("name", TYPE_STRING))
	var items := _field("item", TYPE_ARRAY)
	items.element_schema = item_schema
	var root := DataSchema.new()
	root.add_field(items)
	var result := xml.validate(root)
	expect_equal(result.get_issue_count(), 1, "one XML issue")
	if result.get_issue_count() == 1:
		expect_equal(result.get_issue_kind(0), DataValidation.ISSUE_KIND_TYPE_MISMATCH, "XML attribute is not an int")
		expect_equal(result.get_issue_path(0), "item/1/id", "XML issue path")


func _check_nested_and_yaml() -> void:
	var pos := {"x": TYPE_INT}
	var shape := {"name": TYPE_STRING, "pos": pos, "tags": [TYPE_STRING]}
	var schema := DataSchema.create_from_dictionary(shape)
	expect_equal(schema.fields.size(), 3, "nested schema has three fields")
	expect_equal(schema.find_field("pos"), 1, "pos is the second field")
	expect_equal(schema.get_field("pos").schema.get_field("x").type, TYPE_INT, "nested field type")
	expect_equal(schema.get_field("tags").element_type, TYPE_STRING, "array element type")
	expect(schema.validate({"name": "a", "pos": {"x": 1}, "tags": ["t"]}).is_valid(), "nested dictionary is valid")
	var bad := schema.validate({"name": "a", "pos": {"x": "no"}, "tags": ["t", 2]})
	expect(_has_issue(bad, DataValidation.ISSUE_KIND_TYPE_MISMATCH, "x"), "nested x is not an int")
	expect(_has_issue(bad, DataValidation.ISSUE_KIND_TYPE_MISMATCH, "tags"), "array element is not a string")
	schema.remove_field(0)
	expect_equal(schema.find_field("name"), -1, "remove_field()")

	var yaml := YAML.new()
	expect_equal(yaml.parse("name: Hero\npos:\n  x: 1\ntags:\n  - a\n  - b\n"), OK, "YAML.parse() of a valid document")
	var full := DataSchema.create_from_dictionary(shape)
	expect(yaml.validate(full).is_valid(), "valid YAML document")
	var broken := YAML.new()
	broken.parse("name: Hero\npos:\n  x: nope\ntags:\n  - a\n")
	var result := broken.validate(full)
	expect(not result.is_valid(), "invalid YAML document")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_TYPE_MISMATCH, "x"), "YAML nested x is not an int")
	var missing := YAML.new()
	missing.parse("name: Hero\n")
	var missing_result := missing.validate(full)
	expect(_has_issue(missing_result, DataValidation.ISSUE_KIND_MISSING_FIELD, "pos"), "YAML missing pos")
	expect(_has_issue(missing_result, DataValidation.ISSUE_KIND_MISSING_FIELD, "tags"), "YAML missing tags")


func _check_typed_csv() -> void:
	var schema := _item_schema()
	var csv := CSV.new()
	expect_equal(csv.parse("id,name,hp\n1,Sword,20\n2,Axe,\n"), OK, "CSV.parse() for typed access")
	var row := csv.get_typed_row(1, schema)
	expect_equal(row["id"], 2, "typed id is an int")
	expect_equal(row["name"], "Axe", "typed name")
	expect_equal(row["hp"], 10, "empty cell gives the default value")
	expect_equal(csv.get_typed_row(0, schema)["hp"], 20, "typed hp")
	var ids: Variant = csv.get_typed_column(0, schema)
	expect(ids is PackedInt64Array and ids == PackedInt64Array([1, 2]), "get_typed_column() of an int field is a PackedInt64Array")
	var names: Variant = csv.get_typed_column(1, schema)
	expect(names is PackedStringArray, "get_typed_column() of a String field is a PackedStringArray")

	var values := {"id": 3, "name": "Bow", "hp": 30.0}
	expect_equal(csv.append_typed_row(values, schema), OK, "append_typed_row()")
	expect(csv.get_row(2) == PackedStringArray(["3", "Bow", "30"]), "appended row is written plainly")
	values["hp"] = "lots"
	expect_equal(csv.append_typed_row(values, schema), ERR_INVALID_DATA, "append_typed_row() of an invalid value")
	expect_equal(csv.get_row_count(), 3, "the invalid row is not added")


func _check_many_rows() -> void:
	const COUNT := 100000
	var lines := PackedStringArray()
	lines.resize(COUNT + 1)
	lines[0] = "id,name,hp"
	for i in COUNT:
		lines[i + 1] = "%d,Item %d,%d" % [i, i, i % 100]
	var text := "\n".join(lines) + "\n"
	var csv := CSV.new()
	expect_equal(csv.parse(text), OK, "parse of %d rows" % COUNT)
	expect_equal(csv.get_row_count(), COUNT, "row count of the big CSV")
	var schema := load(RES + "items_schema.tres") as DataSchema
	if schema == null:
		schema = _item_schema()
	var start := Time.get_ticks_msec()
	var result := csv.validate(schema)
	var elapsed := Time.get_ticks_msec() - start
	print("  INFO  %d rows validated in %d ms on %s" % [COUNT, elapsed, OS.get_name()])
	expect(result.is_valid(), "%d valid rows validate (%d issues)" % [COUNT, result.get_issue_count()])
