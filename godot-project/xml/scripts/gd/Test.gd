extends FeatureTest
## Full API test of XML: every method of XML, XMLElement and XMLWriter, and the XML text of a resource.
## Same tests as scripts/cs/Test.cs.

const ROOT := "user://xml/gd/"
const LEVEL := "res://xml/resources/level.xml"
const BOOK := "<?xml version=\"1.0\"?>\n<!-- comment -->\n<book id=\"7\" lang=\"en\">\n\t<title>Fish &amp; Chips</title>\n\t<p>Hello <b>big</b> world</p>\n\t<code><![CDATA[a < b]]></code>\n\t<empty/>\n</book>\n"
const ITEMS := "<items version=\"2\"><item id=\"a\">Sword</item><group><item id=\"b\">Shield</item></group></items>"
const WRITTEN := "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!-- levels -->\n<level name=\"A &amp; &quot;B&quot;\">\n  <music>forest.ogg</music>\n  <note>x &lt; y<![CDATA[a < b]]></note>\n  <empty/>\n</level>\n"


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	_test_data_file()
	_test_parse()
	_test_parse_errors()
	_test_find()
	_test_element()
	_test_create()
	_test_stringify()
	_test_serialize()
	_test_writer()
	_test_writer_errors()


func _test_data_file() -> void:
	var level := load(LEVEL) as XML
	expect(level != null, "load() of a plain .xml returns an XML")
	expect_equal(ResourceLoader.get_resource_type(LEVEL), "XML", "get_resource_type() of a plain .xml")
	var data: Dictionary = level.data
	expect_equal(data.keys(), ["name", "attributes", "children", "text"], "data is the root element: name, attributes, children, text")
	expect_equal(data["attributes"], { "name": "Forest", "time": "120" }, "attribute values are Strings")
	expect_equal(data["children"].size(), 3, "the comment and the indentation are not part of data")


func _test_parse() -> void:
	var xml := XML.new()
	expect_equal(xml.parse(BOOK), OK, "parse() of a document")
	var children: Array = xml.data["children"]
	expect_equal(children.size(), 4, "indentation between elements is dropped")
	expect_equal(children[0]["text"], "Fish & Chips", "entities are decoded")
	var mixed: Array = children[1]["children"]
	expect_equal(mixed[0], "Hello ", "mixed content: text first")
	expect_equal(mixed[1]["name"], "b", "mixed content: the element in the middle")
	expect_equal(mixed[2], " world", "mixed content: text last")
	expect_equal(children[2]["text"], "a < b", "CDATA becomes text")
	expect_equal(children[3]["children"], [], "an empty element has no children")
	expect_equal(children[3]["attributes"], {}, "an empty element has no attributes")
	var data: Dictionary = XML.parse_string("<a x=\"1\"/>")
	expect_equal(data, { "name": "a", "attributes": { "x": "1" }, "children": [], "text": "" }, "parse_string() returns the root element")


func _test_parse_errors() -> void:
	var xml := XML.new()
	expect_equal(xml.parse("<a>\n<b>\n</a>"), ERR_PARSE_ERROR, "a wrong end tag fails")
	expect_equal(xml.get_error_line(), 3, "error line of the wrong end tag")
	expect(xml.get_error_message().contains("</a>"), "the error message names the end tag")
	expect_equal(xml.parse("<a>\n<b/>"), ERR_PARSE_ERROR, "an unclosed element fails")
	expect_equal(xml.get_error_line(), 1, "error line of the unclosed element")
	expect_equal(xml.parse("<a/>\n<b/>"), ERR_PARSE_ERROR, "a second root fails")
	expect_equal(xml.get_error_line(), 2, "error line of the second root")
	expect_equal(xml.data, {}, "data is empty after an error")
	expect_equal(xml.parse("<a/>\r\n"), OK, "a trailing line break is no second root")


func _test_find() -> void:
	var xml := XML.new()
	xml.parse(ITEMS)
	var root: XMLElement = xml.get_root()
	expect_equal(root.name, "items", "get_root()")
	expect_equal(root.get_attribute("version"), "2", "get_attribute()")
	expect_equal(root.get_attribute("missing", "none"), "none", "get_attribute() returns the default")
	expect_equal(root.get_children().size(), 2, "get_children()")
	var shield: XMLElement = xml.find_first("group/item")
	expect_equal(shield.text, "Shield", "find_first() with a path")
	expect(xml.find_first("missing") == null, "find_first() of a missing path is null")
	expect_equal(xml.find_all("item").size(), 1, "find_all() finds direct children of the root")
	var group: XMLElement = root.find_first("group")
	expect_equal(group.find_all("item").size(), 1, "XMLElement.find_all()")
	expect_equal(root.get_children_by_name("group").size(), 1, "get_children_by_name()")


func _test_element() -> void:
	var xml := XML.new()
	xml.parse(ITEMS)
	var root: XMLElement = xml.get_root()
	var sword: XMLElement = root.find_first("item")
	sword.set_attribute("id", "c")
	expect(xml.encode_to_text().contains("<item id=\"c\">Sword</item>"), "set_attribute() writes through to the XML resource")
	expect(sword.has_attribute("id"), "has_attribute()")
	sword.remove_attribute("id")
	expect(not sword.has_attribute("id"), "remove_attribute()")
	expect_equal(root.get_attributes(), { "version": "2" }, "get_attributes()")
	sword.text = "Axe"
	sword.name = "weapon"
	expect(xml.encode_to_text().contains("<weapon>Axe</weapon>"), "name and text write through")
	var bow := XMLElement.new()
	bow.name = "item"
	bow.text = "Bow"
	root.add_child(bow)
	expect_equal(root.get_children().size(), 3, "add_child()")
	root.remove_child(sword)
	expect_equal(root.get_children().size(), 2, "remove_child()")
	expect(not xml.encode_to_text().contains("weapon"), "remove_child() writes through")


func _test_create() -> void:
	var created := XML.new()
	var root: XMLElement = created.create_root("level")
	root.set_attribute("name", "Custom")
	var spawn := XMLElement.new()
	spawn.name = "spawn"
	spawn.set_attribute("enemy", "slime")
	root.add_child(spawn)
	var path := ROOT + "custom_level.xml"
	expect_equal(created.save(path), OK, "save() of a created document")
	expect_equal(FileAccess.get_file_as_string(path), "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<level name=\"Custom\">\n  <spawn enemy=\"slime\"/>\n</level>\n", "the text of the wiki guide")


func _test_stringify() -> void:
	var data: Dictionary = XML.parse_string(BOOK)
	var written: String = XML.stringify(data)
	expect(written.begins_with("<?xml version=\"1.0\" encoding=\"UTF-8\"?>"), "stringify() writes the declaration")
	expect_equal(XML.parse_string(written), data, "stringify() reads back")
	var compact := SerializerOptions.new()
	compact.indent = ""
	expect_equal(XML.parse_string(XML.stringify(data, compact)), data, "stringify() without indent reads back")
	var bare := SerializerOptions.new()
	bare.xml_declaration = false
	var without: String = XML.stringify(data, bare)
	expect(without.begins_with("<book"), "xml_declaration = false leaves out the first line")
	expect(without.contains("<p>Hello <b>big</b> world</p>"), "mixed content stays on one line")


func _test_serialize() -> void:
	var sword := Item.new()
	sword.name = "Sword"
	sword.code = "007"
	var text: String = XML.serialize(sword)
	expect_equal(text, ResourceTextSerializer.serialize(sword, ResourceTextSerializer.FORMAT_XML), "serialize() is ResourceTextSerializer.serialize(FORMAT_XML)")
	expect(text.contains("<resource type=\"Item\" format_version=\"1\">"), "the root element carries the type")
	expect(text.contains("<code kind=\"string\">007</code>"), "kind=\"string\" for a String that would read back as a number")
	var copy := XML.deserialize(text) as Item
	expect_equal(copy.code, "007", "deserialize() restores the resource")

	var inventory := Inventory.new()
	inventory.scores = { "my key": 20 }
	inventory.items = [sword]
	var nested: String = XML.serialize(inventory)
	expect(nested.contains("<entry key=\"my key\">20</entry>"), "a key that is no valid tag becomes <entry key=...>")
	expect(nested.contains("<spare kind=\"null\"/>"), "null is kind=\"null\"")
	expect(nested.contains("<items kind=\"array\">\n    <item>"), "an array is kind=\"array\" with one <item> per element")

	var options := SerializerOptions.new()
	options.indent = ""
	options.enums_as_names = false
	var one_line: String = XML.serialize(sword, options)
	expect(not one_line.contains("\n"), "indent = \"\" writes one line")
	expect(one_line.contains("<rarity>0</rarity>"), "enums_as_names = false writes numbers")
	options = SerializerOptions.new()
	options.xml_declaration = false
	options.include_type_marker = false
	var bare: String = XML.serialize(sword, options)
	expect(bare.begins_with("<resource format_version=\"1\">"), "no declaration and no type")
	var hinted := XML.deserialize(bare, &"Item") as Item
	expect_equal(hinted.code, "007", "deserialize() with a type hint")

	var path := ROOT + "sword.xml"
	expect_equal(ResourceSaver.save(sword, path), OK, "ResourceSaver.save() to .xml")
	expect_equal(ResourceLoader.get_resource_type(path), "Resource", "get_resource_type() reads the type (native base of Item)")
	var loaded := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as Item
	expect_equal(loaded.code, "007", "an .xml with a type loads as its class")


func _test_writer() -> void:
	var writer := XMLWriter.new()
	expect_equal(writer.indent, "  ", "indent defaults to two spaces")
	expect_equal(writer.write_declaration(), OK, "write_declaration()")
	expect_equal(writer.write_comment(" levels "), OK, "write_comment()")
	expect_equal(writer.begin_element("level"), OK, "begin_element()")
	expect_equal(writer.get_depth(), 1, "get_depth() after begin_element()")
	expect_equal(writer.add_attribute("name", "A & \"B\""), OK, "add_attribute()")
	expect_equal(writer.write_element("music", "forest.ogg"), OK, "write_element() with text")
	writer.begin_element("note")
	expect_equal(writer.write_text("x < y"), OK, "write_text()")
	expect_equal(writer.write_cdata("a < b"), OK, "write_cdata()")
	expect_equal(writer.end_element(), OK, "end_element()")
	expect_equal(writer.write_element("empty"), OK, "write_element() without text")
	expect_equal(writer.end_all(), OK, "end_all()")
	expect_equal(writer.get_depth(), 0, "get_depth() after end_all()")
	expect_equal(writer.get_output(), WRITTEN, "get_output()")
	expect_equal(writer.get_output_buffer(), WRITTEN.to_utf8_buffer(), "get_output_buffer() is the UTF-8 output")
	var path := ROOT + "written.xml"
	expect_equal(writer.save(path), OK, "save()")
	expect_equal(FileAccess.get_file_as_string(path), WRITTEN, "save() writes the output")
	writer.clear()
	expect_equal(writer.get_output(), "", "clear()")
	writer.indent = ""
	writer.begin_element("a")
	writer.write_element("b", "1")
	writer.end_all()
	expect_equal(writer.get_output(), "<a><b>1</b></a>", "indent = \"\" writes one line")
	expect(XMLWriter.is_valid_name("ok_1"), "is_valid_name() of a valid name")
	expect(not XMLWriter.is_valid_name("1bad"), "is_valid_name() of a name that starts with a digit")
	expect_equal(XMLWriter.escape_text("a<b&c>\"'"), "a&lt;b&amp;c&gt;\"'", "escape_text()")
	expect_equal(XMLWriter.escape_attribute("a<b&c>\"'\n"), "a&lt;b&amp;c&gt;&quot;&apos;&#10;", "escape_attribute()")


func _test_writer_errors() -> void:
	var writer := XMLWriter.new()
	expect_equal(writer.end_element(), ERR_DOES_NOT_EXIST, "end_element() without an open element")
	expect_equal(writer.begin_element("1bad"), ERR_INVALID_PARAMETER, "begin_element() of an invalid name")
	writer.begin_element("a")
	writer.write_text("text")
	expect_equal(writer.add_attribute("x", "1"), ERR_UNCONFIGURED, "add_attribute() after content")
	expect_equal(writer.write_comment("a -- b"), ERR_INVALID_PARAMETER, "write_comment() with \"--\"")
	expect_equal(writer.write_cdata("x ]]> y"), OK, "write_cdata() splits \"]]>\"")
	expect(writer.get_output().ends_with("<![CDATA[x ]]]]><![CDATA[> y]]>"), "the split CDATA sections")
