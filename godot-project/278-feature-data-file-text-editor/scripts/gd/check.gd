extends FeatureCheck
## GDScript checks of 278-feature-data-file-text-editor. Check the same things as scripts/cs/Issue278FeatureDataFileTextEditorCheck.cs.
##
## The feature is editor-only (highlighters, live errors). These checks cover what works at runtime: the file patterns of the schema,
## line numbers of issues (CSV with a multi-line cell, JSON via resolve_lines), the issue cap and the project setting.
## Rows are 0-based data rows (header excluded), lines are 1-based.

const DIR := "res://278-feature-data-file-text-editor/data/"


func _get_check_name() -> String:
	return "278-feature-data-file-text-editor"


func _run_checks() -> void:
	_check_schema_patterns()
	_check_csv_ok()
	_check_csv_bad()
	_check_issue_cap()
	_check_json_lines()
	_check_project_setting()


func _read(path: String) -> String:
	return FileAccess.get_file_as_string(path).replace("\r\n", "\n")


func _positions() -> DataValidationOptions:
	var options := DataValidationOptions.new()
	options.keep_positions = true
	options.max_issues = 200
	return options


func _check_schema_patterns() -> void:
	var schema := load(DIR + "items_schema.tres") as DataSchema
	expect(schema != null, "items_schema.tres loads as a DataSchema")
	if schema == null:
		return
	expect_equal(schema.fields.size(), 3, "schema has three fields")
	expect_equal(schema.file_patterns.size(), 1, "schema has one file pattern")
	var matches_bad := false
	var matches_ok := false
	var matches_other := false
	for pattern in schema.file_patterns:
		matches_bad = matches_bad or (DIR + "items_bad.csv").match(pattern)
		matches_ok = matches_ok or (DIR + "items_ok.csv").match(pattern)
		matches_other = matches_other or (DIR + "sample.xml").match(pattern) or ("res://other/items_bad.csv").match(pattern)
	expect(matches_bad, "file_patterns match items_bad.csv")
	expect(matches_ok, "file_patterns match items_ok.csv")
	expect(not matches_other, "file_patterns do not match unrelated paths")


func _check_csv_ok() -> void:
	var schema := load(DIR + "items_schema.tres") as DataSchema
	if schema == null:
		return
	var csv := CSV.new()
	expect_equal(csv.parse(_read(DIR + "items_ok.csv"), true), OK, "items_ok.csv parses")
	var result := csv.validate(schema, _positions())
	expect(result.is_valid(), "items_ok.csv is valid (%d issues)" % result.get_issue_count())


func _check_csv_bad() -> void:
	var schema := load(DIR + "items_schema.tres") as DataSchema
	if schema == null:
		return
	var text := _read(DIR + "items_bad.csv")
	expect(text.begins_with("id,name,hp"), "items_bad.csv is readable")
	var csv := CSV.new()
	expect_equal(csv.parse(text, true), OK, "items_bad.csv parses with kept text")
	var result := csv.validate(schema, _positions())
	expect(not result.is_valid(), "items_bad.csv is not valid")
	expect_equal(result.get_issue_count(), 5, "five issues in items_bad.csv")
	# Row 1 is a multi-line cell on lines 3 and 4, so the rows after it are one line further down.
	var expected_lines := {0: 2, 1: 3, 2: 5, 3: 6, 4: 7, 5: 8}
	var lines_ok := true
	var any_after := false
	for i in result.get_issue_count():
		var line := result.get_issue_line(i)
		var row := result.get_issue_row(i)
		lines_ok = lines_ok and line >= 1
		if expected_lines.has(row):
			lines_ok = lines_ok and line == int(expected_lines[row])
		else:
			lines_ok = false
		if row == 2:
			any_after = true
			expect_equal(line, 5, "the row after the multi-line cell is on line 5")
	expect(lines_ok, "every issue has the expected line")
	expect(any_after, "an issue is reported after the multi-line cell")
	if result.get_issue_count() > 0:
		expect(result.get_issue_message(0) != "", "issue 0 has a message")
	# Without keep_positions there is no line.
	var plain := csv.validate(schema)
	if plain.get_issue_count() > 0:
		expect_equal(plain.get_issue_line(0), DataValidation.POSITION_NONE, "no line without keep_positions")


func _check_issue_cap() -> void:
	var schema := load(DIR + "items_schema.tres") as DataSchema
	if schema == null:
		return
	var lines := PackedStringArray()
	lines.append("id,name,hp")
	for i in 500:
		lines.append("%d,Item %d,500" % [i, i])
	var csv := CSV.new()
	expect_equal(csv.parse("\n".join(lines) + "\n", true), OK, "generated CSV parses")
	var result := csv.validate(schema, _positions())
	expect_equal(result.get_issue_count(), 200, "max_issues caps the issues at 200")
	var all_lines := true
	for i in result.get_issue_count():
		all_lines = all_lines and result.get_issue_line(i) >= 1
	expect(all_lines, "every capped issue has a line")


func _check_json_lines() -> void:
	var schema := DataSchema.new()
	var id_field := DataField.new()
	id_field.name = &"id"
	id_field.type = TYPE_INT
	id_field.required = true
	id_field.unique = true
	schema.add_field(id_field)
	var name_field := DataField.new()
	name_field.name = &"name"
	name_field.type = TYPE_STRING
	schema.add_field(name_field)
	var hp := DataField.new()
	hp.name = &"hp"
	hp.type = TYPE_INT
	hp.set_range(0, 100)
	schema.add_field(hp)
	const COUNT := 3000
	var records := PackedStringArray()
	for i in COUNT:
		records.append('{"id": %d, "name": "Item %d", "hp": %d}' % [i, i, 500 if i % 3 == 0 else 10])
	var text := "[\n" + ",\n".join(records) + "\n]\n"
	var data: Variant = JSON.parse_string(text)
	expect(data is Array, "generated JSON parses to an Array")
	if not (data is Array):
		return
	var options := DataValidationOptions.new()
	options.max_issues = 5000
	var start := Time.get_ticks_msec()
	var result := schema.validate(data, options)
	result.resolve_lines(text)
	var elapsed := Time.get_ticks_msec() - start
	print("  INFO  %d JSON records, %d issues validated and resolved in %d ms on %s" % [COUNT, result.get_issue_count(), elapsed, OS.get_name()])
	expect_equal(result.get_issue_count(), 1000, "1000 issues in the JSON records")
	var lines_ok := true
	for i in result.get_issue_count():
		lines_ok = lines_ok and result.get_issue_line(i) == result.get_issue_row(i) + 2
	expect(lines_ok, "resolve_lines() gives line = row + 2")
	expect(elapsed < 2000, "validate and resolve_lines() finish in under 2000 ms (%d ms)" % elapsed)


func _check_project_setting() -> void:
	if not ProjectSettings.has_setting("filesystem/data/schema_files"):
		print("  SKIP  filesystem/data/schema_files: skipped (editor only)")
		return
	var value: Variant = ProjectSettings.get_setting("filesystem/data/schema_files")
	expect_equal(typeof(value), TYPE_PACKED_STRING_ARRAY, "filesystem/data/schema_files is a PackedStringArray")
