extends FeatureTest
## Test of the runtime API the Dataset dock edits through (#436, #438). Same tests as scripts/cs/Test.cs.
## The dock itself is editor UI (editor playtest scenario).


func _run_tests() -> void:
	_test_csv()
	_test_records()
	_test_sections_and_elements()


func _test_csv() -> void:
	var csv := CSV.new()
	expect_equal(csv.parse("name,hp\nknight,100\narcher,80\n"), OK, "CSV.parse()")
	expect_equal(csv.get_row_count(), 2, "two rows")
	csv.insert_row(1, PackedStringArray(["mage", "60"]))
	expect_equal(csv.get_row(1), PackedStringArray(["mage", "60"]), "insert_row() (Insert Row Above)")
	csv.remove_row(1)
	expect_equal(csv.get_row_count(), 2, "remove_row() (Delete Rows)")
	var before: Array = csv.get_cells(0, 1, 2, 1)
	csv.remove_column(1)
	expect_equal(csv.get_header(), PackedStringArray(["name"]), "remove_column() (Remove Column)")
	csv.insert_column(1, "hp")
	csv.set_cells(0, 1, before)
	expect_equal(csv.get_cell(1, 1), "80", "undo of Remove Column: insert_column() + set_cells()")
	expect(csv.get_parsed_text().is_empty(), "an API change drops the kept text (the dock reloads the text editor)")
	var tsv := CSV.stringify([PackedStringArray(["a", "b"])], _tsv_options())
	expect_equal(tsv.strip_edges(), "a\tb", "copy as tab-separated text")


func _tsv_options() -> SerializerOptions:
	var options := SerializerOptions.new()
	options.csv_delimiter = "\t"
	options.csv_headers = false
	return options


func _test_records() -> void:
	var json := JSON.new()
	var text := "{\"version\": 2, \"items\": [{\"id\": 7, \"name\": \"sword\"}, {\"id\": 8}]}"
	expect_equal(json.parse(text, true), OK, "JSON.parse() with keep_text")
	var items: Array = json.data["items"]
	expect_equal(items.size(), 2, "an array of dictionaries under a top-level key is a table")
	var second: Dictionary = items[1]
	expect(not second.has("name"), "a missing field is an empty cell")
	var next := JSON.stringify(json.data, "\t", false)
	expect(next.find("\"version\"") < next.find("\"items\""), "stringify() without sorting keeps the key order")
	json.parse(next, true)
	json.parse(text, true)
	expect_equal(json.get_parsed_text(), text, "undo parses the old text: the exact text comes back")

	var yaml := YAML.new()
	expect_equal(yaml.parse("# Units\n- name: knight\n  hp: 100\n- name: archer\n  hp: 80\n", true), OK, "YAML.parse()")
	var units: Array = yaml.data
	expect_equal(units.size(), 2, "a YAML list of mappings is a table")
	var archer: Dictionary = units[1]
	expect_equal(archer["hp"], 80, "YAML keeps integer types")
	expect(yaml.get_parsed_text().begins_with("# Units"), "the kept YAML text has its comment")

	var toml := TOML.new()
	expect_equal(toml.parse("[[unit]]\nname = \"knight\"\nhp = 100\n\n[[unit]]\nname = \"archer\"\nhp = 80\n", true), OK, "TOML.parse()")
	var tables: Array = toml.data["unit"]
	expect_equal(tables.size(), 2, "a TOML array of tables is a table")
	var knight: Dictionary = tables[0]
	expect_equal(knight["name"], "knight", "TOML table values")


func _test_sections_and_elements() -> void:
	var ini := INI.new()
	expect_equal(ini.parse("[knight]\nhp=100\n[archer]\nhp=80\n"), OK, "INI.parse()")
	expect_equal(ini.get_sections(), PackedStringArray(["knight", "archer"]), "INI sections are the rows")
	var sections: Dictionary = ini.data.duplicate(true)
	sections["mage"] = {"hp": 60}
	ini.data = sections
	expect(ini.encode_to_text().contains("[mage]"), "a new section is written")

	var xml := XML.new()
	expect_equal(xml.parse("<units><unit id=\"knight\" hp=\"100\"/><unit id=\"archer\"/><note>Text</note></units>"), OK, "XML.parse()")
	var children: Array = xml.data["children"]
	var unit_count := 0
	for child in children:
		if child is Dictionary and child["name"] == "unit":
			unit_count += 1
	expect_equal(unit_count, 2, "the children with one tag are the rows")
	var first: Dictionary = children[0]
	var attributes: Dictionary = first["attributes"]
	expect_equal(attributes["id"], "knight", "attributes are the columns")
