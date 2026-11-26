extends FeatureCheck
## GDScript checks of 73-feature-format-classes. Check the same things as scripts/cs/Issue73FeatureFormatClassesCheck.cs.

const TITLE := "Ann & <Bob> \"the\" 2nd"


func _get_check_name() -> String:
	return "73-feature-format-classes"


func _run_checks() -> void:
	_check_api()
	_check_xml()
	_check_ini()
	_check_csv()
	_check_serialize()


func _make_item(item_title: String, item_level: int) -> Issue73GdItem:
	var item := Issue73GdItem.new()
	item.title = item_title
	item.level = item_level
	item.offset = Vector2(0.5, -2.0)
	return item


func _same_item(a: Variant, b: Issue73GdItem) -> bool:
	return a is Issue73GdItem and a.title == b.title and a.level == b.level and a.offset.is_equal_approx(b.offset)


func _check_api() -> void:
	for cls in ["XML", "INI", "CSV"]:
		expect(ClassDB.class_exists(cls) and ClassDB.is_parent_class(cls, "Resource"), "%s exists and is a Resource" % cls)
		for method in ["parse", "get_data", "get_error_line", "get_error_message", "parse_string", "stringify", "serialize", "deserialize"]:
			expect(ClassDB.class_has_method(cls, method), "%s.%s()" % [cls, method])
	expect(ClassDB.class_has_method("JSON", "serialize") and ClassDB.class_has_method("JSON", "deserialize"), "JSON.serialize()/deserialize()")


func _check_xml() -> void:
	var text := "<?xml version=\"1.0\"?>\n<!-- comment -->\n<book id=\"7\" lang=\"en\">\n\t<title>Fish &amp; Chips</title>\n\t<p>Hello <b>big</b> world</p>\n\t<code><![CDATA[a < b]]></code>\n\t<empty/>\n</book>\n"
	var xml := XML.new()
	expect_equal(xml.parse(text), OK, "XML.parse() of a document")
	var root: Dictionary = xml.get_data()
	expect_equal(root.get("name"), "book", "XML root name")
	expect_equal(root.get("attributes"), { "id": "7", "lang": "en" }, "XML attributes")
	var children: Array = root.get("children", [])
	expect_equal(children.size(), 4, "XML indentation between elements is dropped")
	if children.size() == 4:
		expect_equal(children[0].get("text"), "Fish & Chips", "XML entities are decoded")
		var mixed: Array = children[1].get("children", [])
		expect(mixed.size() == 3 and mixed[0] == "Hello " and mixed[1].get("name") == "b" and mixed[2] == " world", "XML mixed content keeps text and elements in order")
		expect_equal(children[2].get("text"), "a < b", "XML CDATA becomes text")
		expect(children[3].get("children") == [] and children[3].get("attributes") == {}, "XML empty element")

	var data := XML.parse_string(text)
	for indent in ["", "  "]:
		var options := SerializerOptions.new()
		options.indent = indent
		var written := XML.stringify(data, options)
		expect(written.begins_with("<?xml version=\"1.0\" encoding=\"UTF-8\"?>"), "XML.stringify() writes the declaration (indent \"%s\")" % indent)
		expect_equal(XML.parse_string(written), data, "XML stringify and parse again gives the same data (indent \"%s\")" % indent)
	var bare := SerializerOptions.new()
	bare.xml_declaration = false
	var written_bare := XML.stringify(data, bare)
	expect(written_bare.begins_with("<book") and written_bare.contains("<p>Hello <b>big</b> world</p>"), "XML mixed content stays on one line")

	expect_equal(xml.parse("<a>\n<b>\n</a>"), ERR_PARSE_ERROR, "XML wrong end tag fails")
	expect_equal(xml.get_error_line(), 3, "XML error line of the wrong end tag")
	expect(xml.get_error_message().contains("</a>"), "XML error message names the end tag")
	expect_equal(xml.parse("<a>\n<b/>"), ERR_PARSE_ERROR, "XML unclosed element fails")
	expect_equal(xml.get_error_line(), 1, "XML error line of the unclosed element")
	expect_equal(xml.parse("<a/>\n<b/>"), ERR_PARSE_ERROR, "XML second root fails")
	expect_equal(xml.get_error_line(), 2, "XML error line of the second root")
	expect(xml.get_data().is_empty(), "XML get_data() is empty after an error")
	expect_equal(xml.parse("<a/>\r\n"), OK, "XML trailing line break is no second root")


func _check_ini() -> void:
	var ini := INI.new()
	var text := "; comment\ntop=1\n\n[player]\nname=\"Ann\"\npos=Vector2(1, 2)\nplain=just text\nflags=[1, 2]\n\n[player]\nlevel=3\n"
	expect_equal(ini.parse(text), OK, "INI.parse()")
	var parsed: Dictionary = ini.get_data()
	expect_equal(parsed.get("", {}).get("top"), 1, "INI keys before the first section are in section \"\"")
	var player: Dictionary = parsed.get("player", {})
	expect(player.get("name") == "Ann" and player.get("pos") == Vector2(1, 2) and player.get("plain") == "just text" and player.get("flags") == [1, 2], "INI typed and plain values")
	expect(player.get("level") == 3 and player.size() == 5, "INI repeated sections are merged")

	var data := {
		"": { "version": 2 },
		"audio": { "volume": 0.5, "device": "Speakers (USB)", "mute": false },
		"video": { "size": Vector2i(1280, 720) },
	}
	var typed := INI.stringify(data)
	expect(typed.begins_with("version=2\n") and typed.contains("[audio]\n") and typed.contains("device=\"Speakers (USB)\""), "INI.stringify() typed values")
	expect_equal(INI.parse_string(typed), data, "INI typed stringify and parse again")
	var plain := SerializerOptions.new()
	plain.ini_value_style = SerializerOptions.INI_VALUE_STYLE_PLAIN
	var plain_text := INI.stringify(data, plain)
	expect(plain_text.contains("device=Speakers (USB)") and plain_text.contains("mute=false"), "INI.stringify() plain values")
	expect_equal(INI.parse_string(plain_text), data, "INI plain stringify and parse again")

	expect_equal(ini.parse("a=1\n[broken\n"), ERR_PARSE_ERROR, "INI unclosed section header fails")
	expect_equal(ini.get_error_line(), 2, "INI error line of the section header")
	expect_equal(ini.parse("[s]\nno value here\n"), ERR_PARSE_ERROR, "INI line without '=' fails")
	expect(ini.get_error_line() == 2 and not ini.get_error_message().is_empty(), "INI error line and message of the key line")


func _check_csv() -> void:
	var headers := SerializerOptions.new()
	headers.csv_headers = true
	var csv := CSV.new()
	csv.options = headers
	var text := "name,note\nAnn,\"a, b\"\r\nBob,\"line\nbreak \"\"quoted\"\"\"\nCleo\n"
	expect_equal(csv.parse(text), OK, "CSV.parse() with headers")
	expect_equal(csv.get_row_count(), 3, "CSV records")
	if csv.get_row_count() == 3:
		var records := [csv.get_row_as_dictionary(0), csv.get_row_as_dictionary(1), csv.get_row_as_dictionary(2)]
		expect(records[0].get("note") == "a, b" and records[1].get("note") == "line\nbreak \"quoted\"", "CSV quoted cells")
		expect(records[2].get("name") == "Cleo" and records[2].get("note") == "", "CSV missing cells are empty")

	var rows_only := SerializerOptions.new()
	rows_only.csv_headers = false
	rows_only.csv_delimiter = ";"
	var rows := CSV.parse_string("007;x\n1;2\n", rows_only)
	expect(rows.size() == 2 and rows[0] is PackedStringArray and rows[0][0] == "007" and rows[1][1] == "2", "CSV rows without headers stay strings")

	var data := [{ "name": "Ann", "note": "a, \"b\"" }, { "name": "Bob", "level": 3 }]
	var written := CSV.stringify(data, headers)
	expect(written.begins_with("name,note,level\n") and written.contains("Ann,\"a, \"\"b\"\"\",\n"), "CSV.stringify() header and quoting")
	var back := CSV.parse_string(written, headers)
	expect(back.size() == 2 and back[0].get("note") == "a, \"b\"" and back[1].get("level") == "3", "CSV stringify and parse again")
	var tabs := SerializerOptions.new()
	tabs.csv_headers = false
	tabs.csv_delimiter = "\t"
	var table := [PackedStringArray(["a", "b c", ""]), PackedStringArray(["1", "2", "3"])]
	var tabbed := CSV.stringify(table, tabs)
	expect_equal(tabbed, "a\tb c\t\n1\t2\t3\n", "CSV.stringify() rows with tab delimiter")
	expect_equal(CSV.parse_string(tabbed, tabs), table, "CSV rows stringify and parse again")

	expect_equal(csv.parse("a,b\n1,2\n3,\"open\n"), ERR_PARSE_ERROR, "CSV unclosed quote fails")
	expect_equal(csv.get_error_line(), 3, "CSV error line of the unclosed quote")
	expect_equal(csv.parse("a,b\n1,2,3\n"), ERR_PARSE_ERROR, "CSV too many cells fails")
	expect(csv.get_error_line() == 2 and not csv.get_error_message().is_empty(), "CSV error line and message of the long row")


func _check_serialize() -> void:
	var item := _make_item(TITLE, 3)
	expect_equal(JSON.serialize(item), ResourceTextSerializer.serialize(item, ResourceTextSerializer.FORMAT_JSON), "JSON.serialize() equals ResourceTextSerializer")
	expect_equal(XML.serialize(item), ResourceTextSerializer.serialize(item, ResourceTextSerializer.FORMAT_XML), "XML.serialize() equals ResourceTextSerializer")
	expect_equal(INI.serialize(item), ResourceTextSerializer.serialize(item, ResourceTextSerializer.FORMAT_INI), "INI.serialize() equals ResourceTextSerializer")
	var options := SerializerOptions.new()
	options.indent = ""
	options.sort_keys = true
	expect_equal(JSON.serialize(item, options), ResourceTextSerializer.serialize(item, ResourceTextSerializer.FORMAT_JSON, options), "JSON.serialize() passes the options")
	expect_equal(XML.serialize(item, options), ResourceTextSerializer.serialize(item, ResourceTextSerializer.FORMAT_XML, options), "XML.serialize() passes the options")

	expect(_same_item(JSON.deserialize(JSON.serialize(item)), item), "JSON.deserialize() restores the item")
	expect(_same_item(XML.deserialize(XML.serialize(item)), item), "XML.deserialize() restores the item")
	expect(_same_item(INI.deserialize(INI.serialize(item)), item), "INI.deserialize() restores the item")
	expect(_same_item(XML.deserialize(XML.serialize(item), &"Issue73GdItem"), item), "XML.deserialize() with a type hint")

	var list := Issue73GdItemList.new()
	list.items = [_make_item("Ann", 3), _make_item("Bob, \"the\" 2nd", 7)]
	var csv_text := CSV.serialize(list)
	expect_equal(csv_text, ResourceTextSerializer.serialize(list, ResourceTextSerializer.FORMAT_CSV), "CSV.serialize() equals ResourceTextSerializer")
	var loaded = CSV.deserialize(csv_text, &"Issue73GdItemList")
	expect(loaded is Issue73GdItemList and loaded.items.size() == 2 and _same_item(loaded.items[1], list.items[1]), "CSV.deserialize() restores the list")
