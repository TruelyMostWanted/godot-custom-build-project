extends FeatureTest
## Full API test of CSV: every method and property of the CSV class, the import as data or translation, the checks of
## translation tables and the CSV text of a resource. Same tests as scripts/cs/Test.cs.
## Rows are 0-based data rows (header excluded).

const RES := "res://csv/resources/"
const QUOTED := "name,note\nAnn,\"a, b\"\r\nBob,\"line\nbreak \"\"quoted\"\"\"\nCleo\n"
const TEAMS := "id,name,team\n1,Ann,red\n2,Bob,blue\n3,Cleo,red\n"
const MANY_ROWS := 100000
const PAGE_MS := 50.0

var _changes: int = 0


func _run_tests() -> void:
	_test_data_file()
	_test_import()
	_test_parse()
	_test_parse_errors()
	_test_read()
	_test_edit()
	_test_blocks()
	_test_clipboard()
	_test_typed()
	_test_stringify()
	_test_translation()
	_test_translation_header()
	_test_serialize()
	_test_many_rows()


func _tab_options() -> SerializerOptions:
	var options := SerializerOptions.new()
	options.csv_delimiter = "\t"
	options.csv_headers = false
	return options


func _on_changed() -> void:
	_changes += 1


func _test_data_file() -> void:
	var units := load(RES + "units.csv") as CSV
	expect(units != null, "load() of a .csv imported with Keep File returns a CSV")
	expect_equal(units.header, PackedStringArray(["name", "class", "hp", "ranged"]), "header is the first line")
	expect_equal(units.get_row_count(), 4, "get_row_count() without the header")
	expect_equal(units.get_column_count(), 4, "get_column_count()")
	expect_equal(units.get_row_as_dictionary(1), { "name": "archer", "class": "ranger", "hp": "60", "ranged": "true" }, "get_row_as_dictionary(): cells are Strings")
	expect_equal(units.data[0], PackedStringArray(["knight", "warrior", "100", "false"]), "data is an Array of PackedStringArray rows")


func _test_import() -> void:
	expect(not ResourceLoader.exists(RES + "stats.health.translation"), "a data CSV with the translation importer creates no 'health' translation")
	expect(not ResourceLoader.exists(RES + "stats.speed.translation"), "a data CSV with the translation importer creates no 'speed' translation")
	expect(ResourceLoader.exists(RES + "greetings.en.translation"), "a translation CSV creates the 'en' translation")
	var german := load(RES + "greetings.de.translation") as Translation
	expect_equal(german.locale, "de", "the 'de' translation has the locale 'de'")
	expect_equal(german.get_message("HELLO"), "Hallo", "the 'de' translation contains HELLO")


func _test_parse() -> void:
	var csv := CSV.new()
	expect_equal(csv.parse(QUOTED), OK, "parse() of quoted cells")
	expect_equal(csv.get_row_count(), 3, "a line break inside quotes is no new row")
	expect_equal(csv.get_cell(0, 1), "a, b", "a quoted cell with the delimiter")
	expect_equal(csv.get_cell(1, 1), "line\nbreak \"quoted\"", "a quoted cell with a line break and doubled quotes")
	expect_equal(csv.get_cell(2, 1), "", "a missing cell is empty")
	var semicolons := SerializerOptions.new()
	semicolons.csv_delimiter = ";"
	semicolons.csv_headers = false
	var rows: Array = CSV.parse_string("007;x\n1;2\n", semicolons)
	expect_equal(rows, [PackedStringArray(["007", "x"]), PackedStringArray(["1", "2"])], "parse_string() without headers: rows of Strings, 007 stays \"007\"")
	var records: Array = CSV.parse_string("name,hp\nknight,100\n")
	expect_equal(records, [{ "name": "knight", "hp": "100" }], "parse_string() with headers: Dictionaries keyed by column name")


func _test_parse_errors() -> void:
	var csv := CSV.new()
	expect_equal(csv.parse("a,b\n1,\"open"), ERR_PARSE_ERROR, "an unclosed quote fails")
	expect_equal(csv.get_error_message(), "Line 2: the quoted cell is not closed.", "error message of the unclosed quote")
	expect_equal(csv.parse("a,b\n1,2\n3,\"open\n"), ERR_PARSE_ERROR, "an unclosed quote in line 3 fails")
	expect_equal(csv.get_error_line(), 3, "error line of the unclosed quote")
	expect_equal(csv.parse("a,b\n1,2,3\n"), ERR_PARSE_ERROR, "a row with more cells than the header fails")
	expect_equal(csv.get_error_line(), 2, "error line of the long row")


func _test_read() -> void:
	var csv := CSV.new()
	csv.parse(TEAMS)
	var team: int = csv.find_column("team")
	expect_equal(team, 2, "find_column()")
	expect_equal(csv.find_column("missing"), -1, "find_column() of a missing name")
	expect_equal(csv.find_rows(team, "red"), PackedInt32Array([0, 2]), "find_rows()")
	expect_equal(csv.get_cell(1, 1), "Bob", "get_cell()")
	expect_equal(csv.get_row(2), PackedStringArray(["3", "Cleo", "red"]), "get_row()")
	expect_equal(csv.get_column(1), PackedStringArray(["Ann", "Bob", "Cleo"]), "get_column()")


func _test_edit() -> void:
	var csv := CSV.new()
	csv.parse(TEAMS)
	csv.set_cell(0, 1, "Anna")
	expect_equal(csv.get_cell(0, 1), "Anna", "set_cell()")
	csv.set_row(1, PackedStringArray(["2", "Ben", "blue"]))
	expect_equal(csv.get_cell(1, 1), "Ben", "set_row()")
	csv.append_row(PackedStringArray(["4", "Dan", "blue"]))
	expect_equal(csv.get_row_count(), 4, "append_row()")
	csv.insert_row(0, PackedStringArray(["0", "Zoe", "red"]))
	expect_equal(csv.get_cell(0, 1), "Zoe", "insert_row()")
	csv.remove_row(0)
	expect_equal(csv.get_cell(0, 1), "Anna", "remove_row()")
	expect_equal(csv.encode_to_text(), "id,name,team\n1,Anna,red\n2,Ben,blue\n3,Cleo,red\n4,Dan,blue\n", "encode_to_text() writes the changes")

	var scripted := CSV.new()
	scripted.header = PackedStringArray(["x", "y"])
	scripted.data = [PackedStringArray(["a", "b"])]
	expect_equal(scripted.find_column("y"), 1, "header is scriptable")
	expect_equal(scripted.get_cell(0, 1), "b", "data is scriptable")
	scripted.clear()
	expect_equal(scripted.header, PackedStringArray(), "clear() empties the header too")


func _test_blocks() -> void:
	var csv := CSV.new()
	csv.parse("name,hp,speed\nknight,100,4\narcher,60\n")
	var block: Array = csv.get_cells(0, 1, 5, 2)
	expect_equal(block.size(), 2, "get_cells() leaves out rows past the end")
	expect_equal(block[0], PackedStringArray(["100", "4"]), "get_cells() row 0")
	expect_equal(block[1], PackedStringArray(["60", ""]), "get_cells() fills missing cells with empty strings")

	csv.changed.connect(_on_changed)
	csv.set_cells(1, 1, [PackedStringArray(["70", "5"]), PackedStringArray(["50", "3"])])
	csv.changed.disconnect(_on_changed)
	expect_equal(_changes, 1, "set_cells() emits changed once per block")
	expect_equal(csv.get_row_count(), 3, "set_cells() appends rows")
	expect_equal(csv.get_row(1), PackedStringArray(["archer", "70", "5"]), "set_cells() extends a short row")
	expect_equal(csv.get_row(2), PackedStringArray(["", "50", "3"]), "set_cells() appended row")

	# The undo of a paste in the CSV Table dock: the old block back, the appended row removed.
	csv.set_cells(1, 1, [PackedStringArray(["60", ""])])
	csv.remove_row(2)
	expect_equal(csv.get_cell(1, 1), "60", "the old block restores the cell")
	expect_equal(csv.get_row_count(), 2, "the appended row is removed")


func _test_clipboard() -> void:
	# Spreadsheets copy tab-separated rows with CRLF and quote cells with line breaks.
	var text := "Sword\t10\r\nBow\t\"long\nrange\"\r\n"
	var rows: Array = CSV.parse_string(text, _tab_options())
	expect_equal(rows, [PackedStringArray(["Sword", "10"]), PackedStringArray(["Bow", "long\nrange"])], "tab-separated clipboard text")
	var csv := CSV.new()
	csv.parse("item,value\nAxe,1\n")
	csv.set_cells(0, 0, rows)
	var copied: String = CSV.stringify(csv.get_cells(0, 0, 2, 2), _tab_options())
	var pasted: Array = CSV.parse_string(copied, _tab_options())
	expect_equal(pasted, rows, "copy out and paste back gives the same block")


func _test_typed() -> void:
	var schema := DataSchema.create_from_dictionary({ "id": TYPE_INT, "name": TYPE_STRING, "hp": TYPE_INT })
	var hp_field: DataField = schema.get_field("hp")
	hp_field.default_value = 10
	var csv := CSV.new()
	csv.parse("id,name,hp\n1,Sword,20\n2,Axe,\n")
	var row: Dictionary = csv.get_typed_row(1, schema)
	expect_equal(row, { "id": 2, "name": "Axe", "hp": 10 }, "get_typed_row(): typed values, an empty cell gives the default")
	var ids: Variant = csv.get_typed_column(0, schema)
	expect_equal(typeof(ids), TYPE_PACKED_INT64_ARRAY, "get_typed_column() of an int field is a PackedInt64Array")
	expect_equal(ids, PackedInt64Array([1, 2]), "get_typed_column() values")
	var names: Variant = csv.get_typed_column(1, schema)
	expect_equal(typeof(names), TYPE_PACKED_STRING_ARRAY, "get_typed_column() of a String field is a PackedStringArray")
	expect_equal(csv.append_typed_row({ "id": 3, "name": "Bow", "hp": 30.0 }, schema), OK, "append_typed_row()")
	expect_equal(csv.get_row(2), PackedStringArray(["3", "Bow", "30"]), "append_typed_row() writes plain text")
	expect_equal(csv.append_typed_row({ "id": 4, "name": "Staff", "hp": "lots" }, schema), ERR_INVALID_DATA, "append_typed_row() of an invalid value")
	expect_equal(csv.get_row_count(), 3, "the invalid row is not added")


func _test_stringify() -> void:
	var headers := SerializerOptions.new()
	var data := [{ "name": "Ann", "note": "a, \"b\"" }, { "name": "Bob", "level": 3 }]
	var written: String = CSV.stringify(data, headers)
	expect(written.begins_with("name,note,level\n"), "stringify(): the header has every key")
	expect(written.contains("Ann,\"a, \"\"b\"\"\",\n"), "stringify(): quotes and doubled quotes")
	var back: Array = CSV.parse_string(written, headers)
	expect_equal(back[1]["level"], "3", "stringify() reads back, cells are Strings")
	var table := [PackedStringArray(["a", "b c", ""]), PackedStringArray(["1", "2", "3"])]
	var tabbed: String = CSV.stringify(table, _tab_options())
	expect_equal(tabbed, "a\tb c\t\n1\t2\t3\n", "stringify() of rows with a tab delimiter")


func _test_translation() -> void:
	var ui := CSV.new()
	expect_equal(ui.load(RES + "ui.csv"), OK, "load() of a translation CSV")
	var check: DataValidation = ui.validate_translation()
	expect_equal(check.get_issue_count(), 3, "ui.csv: three missing translations, no other issue")
	var missing := {}
	for i in check.get_issue_count():
		expect_equal(check.get_issue_kind(i), DataValidation.ISSUE_KIND_EMPTY_REQUIRED, "issue %d is a missing translation" % i)
		var locale: String = check.get_issue_field(i)
		missing[locale] = missing.get(locale, 0) + 1
	expect_equal(missing, { "fr": 2, "de": 1 }, "missing translations per locale, the _notes column is ignored")
	ui.set_cell(1, 3, "Quitter")
	ui.set_cell(2, 2, "Optionen")
	ui.set_cell(2, 3, "Options")
	var complete: DataValidation = ui.validate_translation()
	expect(complete.is_valid(), "a complete translation table is valid")
	var options := DataValidationOptions.new()
	options.stop_on_first = true
	var first: DataValidation = CSV.new().validate_translation(options)
	expect(first.get_issue_count() <= 1, "validate_translation() takes DataValidationOptions")


func _test_translation_header() -> void:
	var csv := CSV.new()
	csv.parse("keys,en,,xx,?context,?context\nHI,Hi,,hi,,\nHI,Hello,,hello,,\nHI,Hey,,hey,formal,\n")
	var check: DataValidation = csv.validate_translation()
	var kinds := {}
	for i in check.get_issue_count():
		var kind: int = check.get_issue_kind(i)
		kinds[kind] = kinds.get(kind, 0) + 1
	expect_equal(kinds.get(DataValidation.ISSUE_KIND_INVALID_LOCALE, 0), 2, "an empty and an unknown locale column")
	expect_equal(kinds.get(DataValidation.ISSUE_KIND_DUPLICATE_KEY, 0), 2, "a second ?context column and a repeated key")

	csv.parse("name,health,speed\nknight,100,4\n")
	var data_check: DataValidation = csv.validate_translation()
	expect_equal(data_check.get_issue_count(), 3, "a data CSV: one issue per unknown locale column and one for the table")
	expect_equal(data_check.get_invalid_rows(), PackedInt32Array(), "a data CSV gets no row checks")
	expect_equal(data_check.get_issue_kind(2), DataValidation.ISSUE_KIND_INVALID_LOCALE, "the table issue is ISSUE_KIND_INVALID_LOCALE")
	expect_equal(data_check.get_issue_column(2), DataValidation.POSITION_NONE, "the table issue has no column")


func _test_serialize() -> void:
	var sword := Item.new()
	sword.name = "Bob, \"the\" 2nd"
	sword.code = "007"
	var table := ItemTable.new()
	table.entries = [sword]
	var text: String = CSV.serialize(table)
	expect_equal(text, ResourceTextSerializer.serialize(table, ResourceTextSerializer.FORMAT_CSV), "serialize() is ResourceTextSerializer.serialize(FORMAT_CSV)")
	expect(text.contains("\"Bob, \"\"the\"\" 2nd\""), "serialize() quotes a cell with the delimiter and quotes")
	var copy := CSV.deserialize(text, &"ItemTable") as ItemTable
	expect_equal(copy.entries[0].name, sword.name, "deserialize() with the type hint")
	var options := SerializerOptions.new()
	options.csv_delimiter = ";"
	var semicolons: String = CSV.serialize(table, options)
	var from_semicolons := CSV.deserialize(semicolons, &"ItemTable", options) as ItemTable
	expect_equal(from_semicolons.entries[0].code, "007", "serialize() and deserialize() with a delimiter option")


func _test_many_rows() -> void:
	var rows := []
	for i in MANY_ROWS:
		rows.append(PackedStringArray([str(i), "unit_%d" % i, str(i % 100)]))
	var csv := CSV.new()
	csv.parse("id,name,hp\n")
	csv.set_cells(0, 0, rows)
	expect_equal(csv.get_row_count(), MANY_ROWS, "set_cells() appends 100 000 rows")
	var start: int = Time.get_ticks_usec()
	var page: Array = csv.get_cells(50000, 0, 500, 3)
	csv.set_cell(50000, 2, "99")
	var elapsed_ms: float = (Time.get_ticks_usec() - start) / 1000.0
	expect_equal(page.size(), 500, "get_cells() reads a page of 500 rows")
	expect(elapsed_ms < PAGE_MS, "page read and cell edit in 100 000 rows under %d ms (%.2f ms)" % [PAGE_MS, elapsed_ms])
