extends FeatureTest
## Full API test of Data Schemas: every method, property and constant of DataSchema, DataField, DataValidation and
## DataValidationOptions, validate() of every text format, line numbers and big data. Same tests as scripts/cs/Test.cs.
## items.csv has a header, valid rows, invalid rows and one quoted cell over two lines. Rows are 0-based data rows
## (header excluded), lines are 1-based.

const RES := "res://data-schemas/resources/"
const UNITS := "res://csv/resources/units.csv"
const UNITS_SCHEMA := "res://csv/resources/units_schema.tres"
const MANY_ROWS := 100000
const JSON_RECORDS := 3000
const JSON_MS := 2000
const ISSUE_CAP := 200


func _run_tests() -> void:
	_test_defaults()
	_test_constants()
	_test_field_setters()
	_test_enum_values()
	_test_schema_resource()
	_test_csv_file()
	_test_csv_positions()
	_test_csv_columns()
	_test_dictionary()
	_test_options()
	_test_json()
	_test_ini()
	_test_xml()
	_test_nested_and_yaml()
	_test_add_issue()
	_test_file_patterns()
	_test_issue_cap()
	_test_json_lines()
	_test_many_rows()
	_test_create_from_resource()


## id (int, unique, required), name (not empty), rarity (enum), hp (0..100, default 10), code (pattern): items_schema.tres in code.
func _item_schema() -> DataSchema:
	var schema := DataSchema.create_from_dictionary({ "id": TYPE_INT, "name": TYPE_STRING, "rarity": TYPE_STRING, "hp": TYPE_INT, "code": TYPE_STRING })
	for field: DataField in schema.fields:
		field.required = field.name == &"id"
	schema.get_field("id").unique = true
	schema.get_field("name").allow_empty = false
	schema.get_field("rarity").set_enum_values(PackedStringArray(["common", "rare"]))
	schema.get_field("hp").set_range(0, 100)
	schema.get_field("hp").default_value = 10
	schema.get_field("code").pattern = "[A-Z]{2}[0-9]+"
	return schema


func _has_issue(result: DataValidation, kind: int, field: String, row: int = -1) -> bool:
	for i in result.get_issue_count():
		if result.get_issue_kind(i) == kind and result.get_issue_field(i) == field and result.get_issue_row(i) == row:
			return true
	return false


func _paths(result: DataValidation) -> PackedStringArray:
	var paths := PackedStringArray()
	for i in result.get_issue_count():
		paths.append(result.get_issue_path(i))
	return paths


func _test_defaults() -> void:
	var field := DataField.new()
	expect_equal(field.name, &"", "DataField.name defaults to empty")
	expect_equal(field.type, TYPE_STRING, "DataField.type defaults to String")
	expect_equal(field.hint, PROPERTY_HINT_NONE, "DataField.hint defaults to none")
	expect_equal(field.hint_string, "", "DataField.hint_string defaults to empty")
	expect(not field.required, "DataField.required defaults to false")
	expect(not field.unique, "DataField.unique defaults to false")
	expect(field.allow_empty, "DataField.allow_empty defaults to true")
	expect(field.default_value == null, "DataField.default_value defaults to null")
	expect_equal(field.description, "", "DataField.description defaults to empty")
	expect_equal(field.pattern, "", "DataField.pattern defaults to empty")
	expect(field.schema == null, "DataField.schema defaults to null")
	expect(field.element_schema == null, "DataField.element_schema defaults to null")
	expect_equal(field.element_type, TYPE_NIL, "DataField.element_type defaults to Nil")
	var schema := DataSchema.new()
	expect_equal(schema.fields, [], "DataSchema.fields defaults to empty")
	expect_equal(schema.extra_fields, DataSchema.EXTRA_FIELDS_REJECT, "DataSchema.extra_fields defaults to REJECT")
	expect(schema.extra_field_template == null, "DataSchema.extra_field_template defaults to null")
	expect_equal(schema.file_patterns, PackedStringArray(), "DataSchema.file_patterns defaults to empty")
	var options := DataValidationOptions.new()
	expect_equal(options.max_issues, 1000, "DataValidationOptions.max_issues defaults to 1000")
	expect(not options.stop_on_first, "DataValidationOptions.stop_on_first defaults to false")
	expect(not options.keep_positions, "DataValidationOptions.keep_positions defaults to false")
	expect(DataValidation.new().is_valid(), "a new DataValidation is valid")


func _test_constants() -> void:
	expect_equal(DataSchema.EXTRA_FIELDS_ALLOW, 1, "EXTRA_FIELDS_ALLOW")
	expect_equal(DataSchema.EXTRA_FIELDS_MATCH_TEMPLATE, 2, "EXTRA_FIELDS_MATCH_TEMPLATE")
	expect_equal(DataSchema.EXTRA_FIELDS_MAX, 3, "EXTRA_FIELDS_MAX")
	var kinds := [DataValidation.ISSUE_KIND_MISSING_FIELD, DataValidation.ISSUE_KIND_UNKNOWN_FIELD, DataValidation.ISSUE_KIND_TYPE_MISMATCH, DataValidation.ISSUE_KIND_OUT_OF_RANGE, DataValidation.ISSUE_KIND_NOT_IN_ENUM, DataValidation.ISSUE_KIND_PATTERN_MISMATCH, DataValidation.ISSUE_KIND_EMPTY_REQUIRED, DataValidation.ISSUE_KIND_DUPLICATE_KEY, DataValidation.ISSUE_KIND_PARSE_ERROR, DataValidation.ISSUE_KIND_INVALID_LOCALE, DataValidation.ISSUE_KIND_MAX]
	expect_equal(kinds, [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10], "ISSUE_KIND_* are 0 to 9, ISSUE_KIND_MAX is 10")
	expect_equal(DataValidation.POSITION_NONE, -1, "POSITION_NONE is -1")


func _test_field_setters() -> void:
	var field := DataField.new()
	field.description = "Hit points."
	expect_equal(field.description, "Hit points.", "description")
	field.set_range(1, 200)
	expect_equal(field.hint, PROPERTY_HINT_RANGE, "set_range() sets the hint RANGE")
	expect_equal(field.hint_string, "1.0,200.0,1.0", "set_range() writes min, max and step")
	field.set_enum_values(PackedStringArray(["warrior", "ranger"]))
	expect_equal(field.hint, PROPERTY_HINT_ENUM, "set_enum_values() sets the hint ENUM")
	expect_equal(field.hint_string, "warrior,ranger", "set_enum_values() writes the names")


func _test_enum_values() -> void:
	var quality := DataField.new()
	quality.name = &"quality"
	quality.type = TYPE_INT
	quality.hint = PROPERTY_HINT_ENUM
	quality.hint_string = "Low,High:5"
	var schema := DataSchema.new()
	schema.add_field(quality)
	expect(schema.validate({ "quality": 0 }).is_valid(), "an int enum \"Low,High:5\": Low is 0")
	expect(schema.validate({ "quality": 5 }).is_valid(), "an int enum \"Low,High:5\": High is 5")
	expect(not schema.validate({ "quality": 1 }).is_valid(), "an int enum \"Low,High:5\": 1 is not a value")


func _test_schema_resource() -> void:
	var schema := load(RES + "items_schema.tres") as DataSchema
	expect_equal(schema.fields.size(), 5, "items_schema.tres has five fields")
	expect_equal(schema.find_field("hp"), 3, "find_field()")
	expect_equal(schema.find_field("missing"), -1, "find_field() of a missing field")
	expect(schema.get_field("missing") == null, "get_field() of a missing field is null")
	var id_field: DataField = schema.get_field("id")
	expect(id_field.required, "id is required")
	expect(id_field.unique, "id is unique")
	var hp: DataField = schema.get_field("hp")
	expect_equal(hp.hint_string, "0,100,1", "hp has the range 0 to 100")
	expect_equal(hp.default_value, 10, "hp has the default 10")
	expect_equal(schema.get_field("rarity").hint_string, "common,rare", "rarity is an enum")
	expect(not schema.get_field("name").allow_empty, "name must not be empty")
	expect_equal(schema.convert_value("hp", "42"), 42, "convert_value() to int")
	expect_equal(schema.convert_value("name", "abc"), "abc", "convert_value() to String")


func _test_csv_file() -> void:
	var schema := load(RES + "items_schema.tres") as DataSchema
	var csv := CSV.new()
	expect_equal(csv.load(RES + "items.csv"), OK, "items.csv loads")
	expect_equal(csv.get_row_count(), 4, "items.csv has four data rows")
	var result := csv.validate(schema)
	expect(not result.is_valid(), "items.csv is not valid")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_DUPLICATE_KEY, "id", 2), "duplicate id in row 2")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_EMPTY_REQUIRED, "name", 2), "empty name in row 2")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_NOT_IN_ENUM, "rarity", 2), "rarity epic in row 2")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_TYPE_MISMATCH, "hp", 2), "hp x in row 2")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_PATTERN_MISMATCH, "code", 2), "code ab in row 2")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_OUT_OF_RANGE, "hp", 3), "hp 500 in row 3")
	expect_equal(result.get_issue_count(), 6, "six issues in items.csv")
	expect_equal(result.get_invalid_rows(), PackedInt32Array([2, 3]), "get_invalid_rows()")
	expect_equal(result.get_issue_message(0), "\"id\": the value \"2\" is already used by entry 1.", "get_issue_message()")
	expect_equal(result.get_issue_line(0), DataValidation.POSITION_NONE, "no line without keep_positions")


func _test_csv_positions() -> void:
	var text: String = FileAccess.get_file_as_string(RES + "items.csv")
	var csv := CSV.new()
	expect_equal(csv.parse(text, true), OK, "parse() with keep_text")
	var options := DataValidationOptions.new()
	options.keep_positions = true
	var result := csv.validate(_item_schema(), options)
	expect_equal(result.get_issue_count(), 6, "six issues with positions")
	# Row 1 spans lines 3 and 4, so row 2 is on line 5 and row 3 on line 6.
	for i in result.get_issue_count():
		var row: int = result.get_issue_row(i)
		var line: int = result.get_issue_line(i)
		expect_equal(line, row + 3, "the issue of row %d is on line %d" % [row, row + 3])
	expect_equal(result.get_issue_column(5), 3, "hp is column 3")

	var extra := CSV.new()
	extra.parse("id,name,colour\n1,Sword,red\n")
	var extra_result := extra.validate(_item_schema())
	expect_equal(extra_result.get_issue_count(), 1, "an unknown column is the only issue")
	expect(_has_issue(extra_result, DataValidation.ISSUE_KIND_UNKNOWN_FIELD, "colour"), "unknown column colour")
	var missing := CSV.new()
	missing.parse("name\nSword\n")
	expect(_has_issue(missing.validate(_item_schema()), DataValidation.ISSUE_KIND_MISSING_FIELD, "id"), "missing required column id")
	var broken := CSV.new()
	broken.parse("id\n\"open\n")
	var broken_result := broken.validate(_item_schema())
	expect_equal(broken_result.get_issue_count(), 1, "a parse error is the only issue")
	expect_equal(broken_result.get_issue_kind(0), DataValidation.ISSUE_KIND_PARSE_ERROR, "ISSUE_KIND_PARSE_ERROR")
	var valid := CSV.new()
	valid.parse("id,name,rarity,hp,code\n1,Sword,common,10,AB1\n2,Axe,rare,,CD2\n")
	expect(valid.validate(_item_schema()).is_valid(), "a valid CSV is valid (an empty hp is fine)")


func _test_csv_columns() -> void:
	var units := CSV.new()
	units.load(UNITS)
	var schema := load(UNITS_SCHEMA) as DataSchema
	var result := units.validate(schema)
	expect_equal(result.get_invalid_rows(), PackedInt32Array([3]), "units.csv: only the ghost row is invalid")
	expect_equal(result.get_issue_column(0), 1, "class is column 1")
	expect_equal(result.get_issue_column(1), 2, "hp is column 2")
	expect_equal(result.get_issue_column(2), 3, "ranged is column 3")
	var reordered := CSV.new()
	reordered.parse("ranged,hp,class,name\nmaybe,60,ranger,archer\n")
	var reordered_result := reordered.validate(schema)
	expect_equal(reordered_result.get_issue_column(0), 0, "columns map to fields by header name, not order")


func _test_dictionary() -> void:
	var schema := _item_schema()
	var data := { "name": "", "rarity": "epic", "hp": 150, "code": "x1", "colour": "red" }
	var result := schema.validate(data)
	expect(_has_issue(result, DataValidation.ISSUE_KIND_MISSING_FIELD, "id"), "missing field id")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_EMPTY_REQUIRED, "name"), "empty name")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_NOT_IN_ENUM, "rarity"), "rarity not in enum")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_OUT_OF_RANGE, "hp"), "hp out of range")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_PATTERN_MISMATCH, "code"), "code pattern mismatch")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_UNKNOWN_FIELD, "colour"), "unknown field colour")
	expect_equal(result.get_issue_count(), 6, "six issues for the Dictionary")
	data["id"] = "abc"
	var typed := schema.validate(data)
	expect(_has_issue(typed, DataValidation.ISSUE_KIND_TYPE_MISMATCH, "id"), "id is not an int")
	schema.extra_fields = DataSchema.EXTRA_FIELDS_ALLOW
	var allowed := schema.validate(data)
	expect(not _has_issue(allowed, DataValidation.ISSUE_KIND_UNKNOWN_FIELD, "colour"), "EXTRA_FIELDS_ALLOW accepts unknown fields")


func _test_options() -> void:
	var schema := _item_schema()
	var data := { "hp": 500, "code": "?" }
	var options := DataValidationOptions.new()
	options.stop_on_first = true
	expect_equal(schema.validate(data, options).get_issue_count(), 1, "stop_on_first gives one issue")
	options.stop_on_first = false
	options.max_issues = 2
	expect_equal(schema.validate(data, options).get_issue_count(), 2, "max_issues caps the issues")
	expect_equal(schema.validate(data).get_issue_count(), 3, "without options all three issues")
	var records := []
	for i in 1500:
		records.append({ "id": "x" })
	var ids := DataSchema.create_from_dictionary({ "id": TYPE_INT })
	expect_equal(ids.validate(records).get_issue_count(), 1000, "the default max_issues is 1000")
	options.max_issues = -1
	expect_equal(ids.validate(records, options).get_issue_count(), 1500, "max_issues = -1 keeps all issues")


func _test_json() -> void:
	var text: String = FileAccess.get_file_as_string(RES + "items.json")
	var data: Array = JSON.parse_string(text)
	var schema := DataSchema.create_from_dictionary({ "id": TYPE_INT, "name": TYPE_STRING, "stats": { "atk": TYPE_INT }, "tags": [TYPE_STRING], "drops": [{ "item": TYPE_STRING, "count": TYPE_INT }] })
	expect_equal(schema.fields.size(), 5, "create_from_dictionary() makes five fields")
	expect(schema.get_field("id").required, "create_from_dictionary() fields are required")
	expect_equal(schema.get_field("stats").type, TYPE_DICTIONARY, "a Dictionary value is a nested schema")
	expect_equal(schema.get_field("tags").element_type, TYPE_STRING, "an Array of a type is a list")
	expect(schema.get_field("drops").element_schema != null, "an Array of a Dictionary is a list of records")
	schema.get_field("id").unique = true
	var result := schema.validate(data)
	expect(_has_issue(result, DataValidation.ISSUE_KIND_DUPLICATE_KEY, "id", 1), "duplicate id in record 1")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_TYPE_MISMATCH, "atk", 1), "the nested atk is not an int")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_TYPE_MISMATCH, "tags", 1), "a tags element is not a String")
	expect(_has_issue(result, DataValidation.ISSUE_KIND_TYPE_MISMATCH, "count", 1), "count in drops is not an int")
	expect_equal(result.get_issue_count(), 4, "four issues in items.json")
	expect_equal(result.get_invalid_rows(), PackedInt32Array([1]), "the index of a JSON array is the row")
	expect(_paths(result).has("1/drops/0/count"), "get_issue_path() of the nested count")
	result.resolve_lines(text)
	for i in result.get_issue_count():
		expect_equal(result.get_issue_line(i), 3, "resolve_lines(): issue %d is on line 3" % i)
	expect(schema.validate(data.slice(0, 1)).is_valid(), "the first record alone is valid")


func _test_ini() -> void:
	var section := DataField.new()
	section.name = &"section"
	section.type = TYPE_DICTIONARY
	section.schema = DataSchema.create_from_dictionary({ "hp": TYPE_INT, "name": TYPE_STRING })
	var schema := DataSchema.new()
	schema.extra_fields = DataSchema.EXTRA_FIELDS_MATCH_TEMPLATE
	schema.extra_field_template = section
	var monsters := load(RES + "monsters.ini") as INI
	var result := monsters.validate(schema)
	expect_equal(result.get_issue_count(), 2, "two issues in monsters.ini")
	expect_equal(_paths(result), PackedStringArray(["orc/hp", "orc"]), "the paths orc/hp (type) and orc (missing name)")

	var text: String = FileAccess.get_file_as_string(RES + "monsters.ini")
	var parsed := INI.new()
	parsed.parse(text, true)
	var options := DataValidationOptions.new()
	options.keep_positions = true
	var with_lines := parsed.validate(schema, options)
	expect_equal(with_lines.get_issue_line(0), 6, "the type mismatch of orc/hp is on line 6")
	expect_equal(with_lines.get_issue_line(1), 5, "the missing name of orc is on the header line 5")
	var unknown := parsed.validate(DataSchema.new())
	expect(_has_issue(unknown, DataValidation.ISSUE_KIND_UNKNOWN_FIELD, "goblin"), "without a template the sections are unknown fields")


func _test_xml() -> void:
	var xml := XML.new()
	xml.parse("<items><item id=\"1\"><name>Sword</name></item><item id=\"x\"/></items>")
	var item := DataSchema.create_from_dictionary({ "id": TYPE_INT })
	var item_name := DataField.new()
	item_name.name = &"name"
	item.add_field(item_name)
	var items := DataField.new()
	items.name = &"item"
	items.type = TYPE_ARRAY
	items.element_schema = item
	var root := DataSchema.new()
	root.add_field(items)
	var result := xml.validate(root)
	expect_equal(result.get_issue_count(), 1, "one XML issue")
	expect_equal(result.get_issue_kind(0), DataValidation.ISSUE_KIND_TYPE_MISMATCH, "an attribute that is not an int")
	expect_equal(result.get_issue_path(0), "item/1/id", "repeated children are an Array: path item/1/id")


func _test_nested_and_yaml() -> void:
	var shape := { "name": TYPE_STRING, "pos": { "x": TYPE_INT }, "tags": [TYPE_STRING] }
	var schema := DataSchema.create_from_dictionary(shape)
	expect_equal(schema.get_field("pos").schema.get_field("x").type, TYPE_INT, "a nested field type")
	expect(schema.validate({ "name": "a", "pos": { "x": 1 }, "tags": ["t"] }).is_valid(), "a valid nested Dictionary")
	var bad := schema.validate({ "name": "a", "pos": { "x": "no" }, "tags": ["t", 2] })
	expect(_has_issue(bad, DataValidation.ISSUE_KIND_TYPE_MISMATCH, "x"), "the nested x is not an int")
	expect(_has_issue(bad, DataValidation.ISSUE_KIND_TYPE_MISMATCH, "tags"), "an array element is not a String")
	schema.remove_field(0)
	expect_equal(schema.find_field("name"), -1, "remove_field()")

	var full := DataSchema.create_from_dictionary(shape)
	var yaml := YAML.new()
	yaml.parse("name: Hero\npos:\n  x: 1\ntags:\n  - a\n  - b\n")
	expect(yaml.validate(full).is_valid(), "a valid YAML document")
	yaml.parse("name: Hero\npos:\n  x: nope\ntags:\n  - a\n")
	expect(_has_issue(yaml.validate(full), DataValidation.ISSUE_KIND_TYPE_MISMATCH, "x"), "YAML: the nested x is not an int")
	yaml.parse("name: Hero\n")
	var missing := yaml.validate(full)
	expect(_has_issue(missing, DataValidation.ISSUE_KIND_MISSING_FIELD, "pos"), "YAML: missing pos")
	expect(_has_issue(missing, DataValidation.ISSUE_KIND_MISSING_FIELD, "tags"), "YAML: missing tags")
	var toml := TOML.new()
	toml.parse("name = \"Hero\"\ntags = [\"a\"]\n[pos]\nx = 1\n")
	expect(toml.validate(full).is_valid(), "a valid TOML document")


func _test_add_issue() -> void:
	var result := DataValidation.new()
	result.add_issue(DataValidation.ISSUE_KIND_NOT_IN_ENUM, "\"gem\" is no item.", &"drop", "3/drop", 3)
	result.add_issue(DataValidation.ISSUE_KIND_NOT_IN_ENUM, "\"ore\" is no item.", &"drop", "1/drop", 1, 2, 7)
	result.add_issue(DataValidation.ISSUE_KIND_NOT_IN_ENUM, "\"gem\" is no item.", &"drop", "3/drop", 3)
	expect(not result.is_valid(), "add_issue() makes the result invalid")
	expect_equal(result.get_issue_count(), 3, "add_issue() adds an issue")
	expect_equal(result.get_issue_message(1), "\"ore\" is no item.", "add_issue(): message")
	expect_equal(result.get_issue_field(1), &"drop", "add_issue(): field")
	expect_equal(result.get_issue_path(1), "1/drop", "add_issue(): path")
	expect_equal(result.get_issue_row(1), 1, "add_issue(): row")
	expect_equal(result.get_issue_column(1), 2, "add_issue(): column")
	expect_equal(result.get_issue_line(1), 7, "add_issue(): line")
	expect_equal(result.get_issue_column(0), DataValidation.POSITION_NONE, "add_issue() without a column")
	expect_equal(result.get_invalid_rows(), PackedInt32Array([1, 3]), "get_invalid_rows() lists each row once, sorted")


func _test_file_patterns() -> void:
	var schema := load(RES + "items_schema.tres") as DataSchema
	expect_equal(schema.file_patterns, PackedStringArray(["res://data-schemas/resources/items.csv"]), "file_patterns")
	var units := load(UNITS_SCHEMA) as DataSchema
	expect(UNITS.match(units.file_patterns[0]), "the pattern of units_schema.tres matches units.csv")
	expect(not "res://other/units.csv".match(units.file_patterns[0]), "the pattern does not match another path")
	expect_equal(typeof(ProjectSettings.get_setting("filesystem/data/schema_files")), TYPE_PACKED_STRING_ARRAY, "the project setting filesystem/data/schema_files")


func _test_issue_cap() -> void:
	var lines := PackedStringArray(["id,name,hp"])
	for i in 500:
		lines.append("%d,Item %d,500" % [i, i])
	var csv := CSV.new()
	csv.parse("\n".join(lines) + "\n", true)
	var options := DataValidationOptions.new()
	options.keep_positions = true
	options.max_issues = ISSUE_CAP
	var result := csv.validate(_item_schema(), options)
	expect_equal(result.get_issue_count(), ISSUE_CAP, "max_issues caps the issues of 500 rows at 200")
	expect_equal(result.get_issue_line(ISSUE_CAP - 1), ISSUE_CAP + 1, "the capped issues have their lines")


func _test_json_lines() -> void:
	var schema := _item_schema()
	var records := PackedStringArray()
	for i in JSON_RECORDS:
		records.append("{\"id\": %d, \"name\": \"Item %d\", \"hp\": %d}" % [i, i, 500 if i % 3 == 0 else 10])
	var text := "[\n" + ",\n".join(records) + "\n]\n"
	var data: Array = JSON.parse_string(text)
	var options := DataValidationOptions.new()
	options.max_issues = -1
	var start: int = Time.get_ticks_msec()
	var result := schema.validate(data, options)
	result.resolve_lines(text)
	var elapsed: int = Time.get_ticks_msec() - start
	expect_equal(result.get_issue_count(), JSON_RECORDS / 3, "1000 issues in 3000 JSON records")
	expect_equal(result.get_issue_line(999), result.get_issue_row(999) + 2, "resolve_lines(): line = row + 2")
	expect(elapsed < JSON_MS, "validate() and resolve_lines() of 3000 records under %d ms (%d ms)" % [JSON_MS, elapsed])


func _test_many_rows() -> void:
	var lines := PackedStringArray(["id,name,hp"])
	for i in MANY_ROWS:
		lines.append("%d,Item %d,%d" % [i, i, i % 100])
	var csv := CSV.new()
	csv.parse("\n".join(lines) + "\n")
	var start: int = Time.get_ticks_msec()
	var result := csv.validate(_item_schema())
	print("  INFO  %d rows validated in %d ms" % [MANY_ROWS, Time.get_ticks_msec() - start])
	expect(result.is_valid(), "100 000 valid rows validate")


## The sample resource is a DataSchema: it has an enum, a typed array of resources, a nested resource and a packed array.
func _test_create_from_resource() -> void:
	var sample := DataSchema.new()
	sample.add_field(DataField.new())
	var schema: DataSchema = DataSchema.create_from_resource(sample)
	expect(schema != null, "create_from_resource returns a schema")
	expect_equal(schema.extra_fields, DataSchema.EXTRA_FIELDS_ALLOW, "create_from_resource allows extra fields")
	var names := PackedStringArray()
	for field: DataField in schema.fields:
		names.append(field.name)
	expect(names.has("fields") and names.has("extra_fields") and names.has("file_patterns"), "one field per stored property")
	expect_equal(schema.get_field("extra_fields").type, TYPE_STRING, "an enum is a String field (names)")
	expect_equal(schema.get_field("extra_fields").hint, PROPERTY_HINT_ENUM, "the enum hint is kept")
	expect_equal(schema.get_field("fields").type, TYPE_ARRAY, "an array is an Array field")
	expect(schema.get_field("fields").element_schema != null, "an array of resources has an element schema")
	expect(schema.get_field("fields").element_schema.get_field("name") != null, "the element schema describes DataField")
	expect_equal(schema.get_field("extra_field_template").type, TYPE_NIL, "an unset resource stays untyped")
	expect(schema.get_field("resource_name") == null, "Resource base properties are left out")
	expect_equal(schema.get_field("file_patterns").type, TYPE_PACKED_STRING_ARRAY, "a packed array keeps its type")
	expect(not schema.get_field("fields").required, "generated fields are optional")
	var options := SerializerOptions.new()
	options.enums_as_names = false
	expect_equal(DataSchema.create_from_resource(sample, options).get_field("extra_fields").type, TYPE_INT, "an enum stays a number without enums_as_names")
	var data := { "type": "DataSchema", "extra_fields": "Allow" }
	expect(schema.validate(data).is_valid(), "data as the text formats write it validates")
	data["extra_fields"] = "Maybe"
	expect(not schema.validate(data).is_valid(), "an unknown enum name is an issue")
