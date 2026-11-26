extends FeatureCheck
## GDScript checks of 279-feature-csv-table-editor (and 280, translation CSV checks). Check the same things as
## scripts/cs/Issue279FeatureCsvTableEditorCheck.cs.
##
## The CSV Table dock is editor UI; these checks cover the API it is built on: CSV.get_cells()/set_cells() (copy, paste,
## undo of a block), tab-separated clipboard text, the schema issues it marks and CSV.validate_translation() (#280).
## resources/units.csv is a data CSV ("Keep File") with units_schema.tres; resources/ui.csv is a translation CSV.
## Rows are 0-based data rows (header excluded).

const RES := "res://279-feature-csv-table-editor/resources/"


func _get_check_name() -> String:
	return "279-feature-csv-table-editor"


func _run_checks() -> void:
	_check_api()
	_check_blocks()
	_check_tsv_clipboard_text()
	_check_schema_issues()
	_check_translation()
	_check_translation_header()
	_check_many_rows()


func _tsv_options() -> SerializerOptions:
	var options := SerializerOptions.new()
	options.csv_delimiter = "\t"
	options.csv_headers = false
	return options


func _check_api() -> void:
	for method in ["get_cells", "set_cells", "validate_translation"]:
		expect(ClassDB.class_has_method("CSV", method), "CSV.%s()" % method)


func _check_blocks() -> void:
	var csv := CSV.new()
	expect(csv.parse("name,hp,speed\nknight,100,4\narcher,60\n") == OK, "blocks: parse")
	var block: Array = csv.get_cells(0, 1, 5, 2)
	expect(block.size() == 2, "get_cells() leaves out rows past the end")
	expect(block.size() == 2 and block[0] == PackedStringArray(["100", "4"]), "get_cells() row 0")
	expect(block.size() == 2 and block[1] == PackedStringArray(["60", ""]), "get_cells() fills missing cells with empty strings")

	var changes := [0]
	var count := func() -> void: changes[0] += 1
	csv.changed.connect(count)
	csv.set_cells(1, 1, [PackedStringArray(["70", "5"]), PackedStringArray(["50", "3"])])
	csv.changed.disconnect(count)
	expect(changes[0] == 1, "set_cells() emits changed once per block")
	expect(csv.get_row_count() == 3, "set_cells() appends rows")
	expect(csv.get_row(1) == PackedStringArray(["archer", "70", "5"]), "set_cells() extends a short row")
	expect(csv.get_row(2) == PackedStringArray(["", "50", "3"]), "set_cells() appended row")

	# The table editor's undo of a paste: old block back, appended rows removed.
	csv.set_cells(1, 1, [PackedStringArray(["60", ""])])
	csv.remove_row(2)
	expect(csv.get_cell(1, 1) == "60" and csv.get_row_count() == 2, "old block restores the data")


func _check_tsv_clipboard_text() -> void:
	# Spreadsheets copy tab-separated rows with CRLF and quote cells with line breaks.
	var text := "Sword\t10\r\nBow\t\"long\nrange\"\r\n"
	var rows := CSV.parse_string(text, _tsv_options())
	expect(rows.size() == 2, "TSV clipboard text: 2 rows")
	expect(rows.size() == 2 and rows[1] == PackedStringArray(["Bow", "long\nrange"]), "TSV clipboard text: quoted multi-line cell")
	var csv := CSV.new()
	csv.parse("item,value\nAxe,1\n")
	csv.set_cells(0, 0, rows)
	var copied := CSV.stringify(csv.get_cells(0, 0, 2, 2), _tsv_options())
	expect(CSV.parse_string(copied, _tsv_options()) == rows, "copy out and paste back round-trips")


func _check_schema_issues() -> void:
	var csv := CSV.new()
	expect(csv.load(RES + "units.csv") == OK, "units.csv loads")
	var schema: DataSchema = load(RES + "units_schema.tres")
	expect(schema != null, "units_schema.tres loads")
	if schema == null:
		return
	expect(schema.file_patterns.size() == 1 and (RES + "units.csv").matchn(schema.file_patterns[0]), "file_patterns match units.csv")
	var validation := csv.validate(schema)
	expect(validation.get_invalid_rows() == PackedInt32Array([3]), "only the ghost row is invalid")
	var columns := {}
	for i in validation.get_issue_count():
		columns[validation.get_issue_column(i)] = true
	expect(columns.has(1) and columns.has(2) and columns.has(3), "issues have the cell column (class, hp, ranged)")


func _missing_per_locale(validation: DataValidation, csv: CSV) -> Dictionary:
	var missing := {}
	for i in validation.get_issue_count():
		if validation.get_issue_kind(i) == DataValidation.ISSUE_KIND_EMPTY_REQUIRED:
			var locale: String = csv.header[validation.get_issue_column(i)]
			missing[locale] = missing.get(locale, 0) + 1
	return missing


func _check_translation() -> void:
	var csv := CSV.new()
	expect(csv.load(RES + "ui.csv") == OK, "ui.csv loads as CSV")
	var validation := csv.validate_translation()
	expect(validation.get_issue_count() == 3, "ui.csv: 3 missing translations, no other issue")
	expect(_missing_per_locale(validation, csv) == {"de": 1, "fr": 2}, "missing translations per locale")
	for i in validation.get_issue_count():
		expect(validation.get_issue_row(i) >= 1 and validation.get_issue_field(i) != &"_notes", "issue %d has a row and is no ignored column" % i)

	csv.set_cell(1, 3, "Quitter")
	csv.set_cell(2, 2, "Optionen")
	csv.set_cell(2, 3, "Options")
	expect(csv.validate_translation().is_valid(), "complete translation table is valid")


func _check_translation_header() -> void:
	var csv := CSV.new()
	csv.parse("keys,en,,xx,?context,?context\nHI,Hi,,hi,,\nHI,Hello,,hello,,\nHI,Hey,,hey,formal,\n")
	var validation := csv.validate_translation()
	var kinds := {}
	for i in validation.get_issue_count():
		var kind := validation.get_issue_kind(i)
		kinds[kind] = kinds.get(kind, 0) + 1
	expect(kinds.get(DataValidation.ISSUE_KIND_INVALID_LOCALE, 0) == 2, "empty and unknown locale columns")
	expect(kinds.get(DataValidation.ISSUE_KIND_DUPLICATE_KEY, 0) == 2, "second ?context column and repeated key")

	csv.parse("name,health,speed\nknight,100,4\n")
	validation = csv.validate_translation()
	var last := validation.get_issue_count() - 1
	expect(validation.get_issue_kind(last) == DataValidation.ISSUE_KIND_INVALID_LOCALE and validation.get_issue_column(last) == DataValidation.POSITION_NONE, "data CSV: not a translation table")


func _check_many_rows() -> void:
	# The dock shows one page (500 rows) of a large file; reading a page and editing a cell must stay fast.
	var rows := []
	for i in 100000:
		rows.append(PackedStringArray([str(i), "unit_%d" % i, str(i % 100)]))
	var csv := CSV.new()
	csv.parse("id,name,hp\n")
	csv.set_cells(0, 0, rows)
	expect(csv.get_row_count() == 100000, "100k rows")
	var start := Time.get_ticks_usec()
	var page: Array = csv.get_cells(50000, 0, 500, 3)
	csv.set_cell(50000, 2, "99")
	var elapsed_ms := (Time.get_ticks_usec() - start) / 1000.0
	expect(page.size() == 500 and csv.get_cell(50000, 2) == "99", "page read and cell edit")
	expect(elapsed_ms < 50.0, "page read + cell edit in 100k rows under 50 ms (%.2f ms)" % elapsed_ms)
