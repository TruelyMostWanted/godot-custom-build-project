extends FeatureCheck
## GDScript checks of 68-feature-xml-and-unified-serializer. Check the same things as scripts/cs/Issue68FeatureXmlAndUnifiedSerializerCheck.cs.

const ROOT := "user://68-feature-xml-and-unified-serializer/gd/"
const OWNER := "Ada & <Bob> \"the\" 2nd\nline two"


func _get_check_name() -> String:
	return "68-feature-xml-and-unified-serializer"


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	_check_api()
	_check_xml_text()
	_check_xml_round_trip()
	_check_xml_files()
	_check_same_text_as_files()
	_cleanup()


func _make_item(item_name: String, count: int) -> Issue68GdItem:
	var item := Issue68GdItem.new()
	item.name = item_name
	item.count = count
	item.weight = 0.5
	item.rarity = Issue68GdItem.Rarity.EPIC
	item.offset = Vector2(0.5, -2.0)
	item.code = "007"
	return item


func _make_inventory() -> Issue68GdInventory:
	var inv := Issue68GdInventory.new()
	inv.owner_name = OWNER
	inv.gold = 250
	inv.tint = Color(1, 0, 0, 1)
	inv.main_hand = _make_item("Sword", 1)
	inv.items = [_make_item("Potion", 3), _make_item(" ", 2)]
	inv.scores = { "ann": 10, "my key": 20 }
	inv.notes = { "lives": 3, "ratio": 1.0, "empty": {}, "none": null }
	inv.anything = [1, 2.5, "3", true, null]
	return inv


func _same_item(a: Issue68GdItem, b: Issue68GdItem) -> bool:
	return a != null and b != null and a.name == b.name and a.count == b.count and is_equal_approx(a.weight, b.weight) \
			and a.rarity == b.rarity and a.offset.is_equal_approx(b.offset) and a.code == b.code


func _check_api() -> void:
	var formats := ResourceTextSerializer.get_available_formats()
	for f in [ResourceTextSerializer.FORMAT_JSON, ResourceTextSerializer.FORMAT_INI, ResourceTextSerializer.FORMAT_CSV, ResourceTextSerializer.FORMAT_XML]:
		expect(formats.has(f), "get_available_formats() lists %s" % ResourceTextSerializer.get_format_extension(f))
	expect(formats.has(ResourceTextSerializer.FORMAT_TOML) == ClassDB.class_exists("TOML"), "get_available_formats() lists FORMAT_TOML exactly when the TOML module is built")
	expect(ResourceTextSerializer.is_format_available(ResourceTextSerializer.FORMAT_XML) and not ResourceTextSerializer.is_format_available(ResourceTextSerializer.FORMAT_YAML), "is_format_available()")
	expect_equal(ResourceTextSerializer.get_format_from_extension(".JSON"), ResourceTextSerializer.FORMAT_JSON, "get_format_from_extension() ignores case and dot")
	expect(not ClassDB.class_has_method("ResourceTextSerializer", "resource_to_json") and not ClassDB.class_has_method("ResourceTextSerializer", "resource_to_xml"), "per-format methods are replaced by serialize()/deserialize()")
	if ClassDB.class_exists("TOML"):
		expect(ClassDB.class_has_method("TOML", "serialize") and ClassDB.class_has_method("TOML", "deserialize"), "TOML.serialize()/deserialize() exist")


func _check_xml_text() -> void:
	var text := ResourceTextSerializer.serialize(_make_item("Sword", 1), ResourceTextSerializer.FORMAT_XML)
	var expected := "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" \
			+ "<resource type=\"Issue68GdItem\" format_version=\"1\">\n" \
			+ "  <name>Sword</name>\n" \
			+ "  <count>1</count>\n" \
			+ "  <weight>0.5</weight>\n" \
			+ "  <rarity>Epic</rarity>\n" \
			+ "  <offset kind=\"array\">0.5, -2.0</offset>\n" \
			+ "  <code kind=\"string\">007</code>\n" \
			+ "</resource>\n"
	expect_equal(text, expected, "XML text of a flat resource")
	var options := SerializerOptions.new()
	options.indent = ""
	options.enums_as_names = false
	var one_line := ResourceTextSerializer.serialize(_make_item("Sword", 1), ResourceTextSerializer.FORMAT_XML, options)
	expect(not one_line.contains("\n") and one_line.contains("<rarity>5</rarity>"), "XML options indent and enums_as_names")
	options = SerializerOptions.new()
	options.xml_declaration = false
	options.include_type_marker = false
	var bare := ResourceTextSerializer.serialize(_make_item("Sword", 1), ResourceTextSerializer.FORMAT_XML, options)
	expect(bare.begins_with("<resource format_version=\"1\">"), "XML options xml_declaration and include_type_marker")
	expect(ResourceTextSerializer.deserialize(bare, ResourceTextSerializer.FORMAT_XML, &"Issue68GdItem") is Issue68GdItem, "XML without type marker reads with a type hint")


func _check_inventory(loaded: Resource, label: String) -> void:
	var e := _make_inventory()
	expect(loaded is Issue68GdInventory, "%s: loads as Issue68GdInventory" % label)
	if not loaded is Issue68GdInventory:
		return
	var inv: Issue68GdInventory = loaded
	expect(inv.owner_name == OWNER and inv.gold == 250 and inv.tint.is_equal_approx(e.tint), "%s: string with & < > quotes and line break, int, Color" % label)
	expect(_same_item(inv.main_hand, e.main_hand), "%s: nested resource" % label)
	expect(inv.items.size() == 2 and _same_item(inv.items[0], e.items[0]) and _same_item(inv.items[1], e.items[1]), "%s: typed array of resources (one name is a space)" % label)
	expect(inv.scores.is_typed() and inv.scores.size() == 2 and inv.scores.get("ann", 0) == 10 and inv.scores.get("my key", 0) == 20, "%s: typed Dictionary, key that is not an XML name" % label)
	var n := inv.notes
	expect(typeof(n.get("lives")) == TYPE_INT and typeof(n.get("ratio")) == TYPE_FLOAT and n.get("empty") is Dictionary and n.has("none") and n.get("none") == null, "%s: untyped Dictionary keeps int, float, {} and null" % label)
	var a: Array = inv.anything if inv.anything is Array else []
	expect(a.size() == 5 and typeof(a[0]) == TYPE_INT and typeof(a[1]) == TYPE_FLOAT and typeof(a[2]) == TYPE_STRING and typeof(a[3]) == TYPE_BOOL and a[4] == null, "%s: Variant array keeps int, float, String, bool, null" % label)


func _check_xml_round_trip() -> void:
	var text := ResourceTextSerializer.serialize(_make_inventory(), ResourceTextSerializer.FORMAT_XML)
	expect(text.contains("<resource type=\"Issue68GdInventory\" format_version=\"1\">"), "XML root element with type marker")
	expect(text.contains("<entry key=\"my key\">20</entry>") and text.contains("<none kind=\"null\"/>"), "XML entry key and null")
	_check_inventory(ResourceTextSerializer.deserialize(text, ResourceTextSerializer.FORMAT_XML), "deserialize xml")
	_check_inventory(ResourceTextSerializer.deserialize(text, ResourceTextSerializer.FORMAT_XML, &"Issue68GdInventory"), "deserialize xml with type hint")


func _check_xml_files() -> void:
	var path := ROOT + "inventory.xml"
	expect_equal(ResourceSaver.save(_make_inventory(), path), OK, "save as .xml")
	expect_equal(ResourceLoader.get_resource_type(path), "Resource", "get_resource_type() of an .xml resource (native base of the script class)")
	_check_inventory(ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE), ".xml file")

	var other := ROOT + "other.xml"
	var f := FileAccess.open(other, FileAccess.WRITE)
	f.store_string("<?xml version=\"1.0\"?>\n<config><volume>3</volume></config>\n")
	f.close()
	expect_equal(ResourceLoader.get_resource_type(other), "", "XML without the type marker is not a resource file")


func _check_same_text_as_files() -> void:
	var inv := _make_inventory()
	for ext in ["json", "ini", "xml", "toml"]:
		var format := ResourceTextSerializer.get_format_from_extension(ext)
		if not ResourceTextSerializer.is_format_available(format):
			continue
		var path: String = ROOT + "same." + ext
		expect_equal(ResourceSaver.save(inv, path), OK, "save as .%s" % ext)
		expect_equal(ResourceTextSerializer.serialize(inv, format), FileAccess.get_file_as_string(path), "serialize(res, FORMAT for .%s) gives the file text" % ext)

	var list := Issue68GdItemList.new()
	list.entries = [_make_item("Potion", 3), _make_item("Bread, \"fresh\"", 2)]
	expect_equal(ResourceSaver.save(list, ROOT + "same.csv"), OK, "save as .csv")
	expect_equal(ResourceTextSerializer.serialize(list, ResourceTextSerializer.FORMAT_CSV), FileAccess.get_file_as_string(ROOT + "same.csv"), "serialize(res, FORMAT_CSV) gives the file text")
	var csv_options := SerializerOptions.new()
	csv_options.csv_delimiter = ";"
	var semicolons := ResourceTextSerializer.serialize(list, ResourceTextSerializer.FORMAT_CSV, csv_options)
	var back = ResourceTextSerializer.deserialize(semicolons, ResourceTextSerializer.FORMAT_CSV, &"Issue68GdItemList", csv_options)
	expect(back is Issue68GdItemList and back.entries.size() == 2 and _same_item(back.entries[1], list.entries[1]), "csv with type hint and delimiter option")


func _cleanup() -> void:
	for file in DirAccess.get_files_at(ROOT):
		DirAccess.remove_absolute(ROOT + file)
