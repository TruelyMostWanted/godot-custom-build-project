extends FeatureCheck
## GDScript checks of 05-feature-resource-text-formats. Check the same things as scripts/cs/Issue05FeatureResourceTextFormatsCheck.cs.

const ROOT := "user://05-feature-resource-text-formats/gd/"


func _get_check_name() -> String:
	return "05-feature-resource-text-formats"


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(ROOT)
	expect(Engine.has_singleton("ResourceTextSerializer"), "ResourceTextSerializer singleton exists")
	expect(ClassDB.class_exists("XMLWriter"), "XMLWriter class exists")
	for ext in ["json", "ini"]:
		_check_settings_file(ext)
	_check_csv()
	_check_string_api()
	_check_serializer_options()
	_check_plain_json()
	_cleanup()


func _make_bus(bus_name: String, volume: float, mode: int) -> Issue05GdAudioBus:
	var bus := Issue05GdAudioBus.new()
	bus.name = bus_name
	bus.volume_db = volume
	bus.muted = volume < -10.0
	bus.mode = mode
	bus.pan = Vector2(0.25, -0.5)
	return bus


func _make_settings() -> Issue05GdAudioSettings:
	var s := Issue05GdAudioSettings.new()
	s.master_volume = 0.75
	s.title = "Cleo, \"the\" 2nd = ok"
	s.quality = Issue05GdAudioSettings.Quality.HIGH
	s.tint = Color(0.5, 0.25, 1.0, 0.5)
	s.main_bus = _make_bus("Master", -3.5, Issue05GdAudioBus.Mode.MONO)
	s.buses = [_make_bus("Music", -12.0, Issue05GdAudioBus.Mode.SURROUND), _make_bus("Sfx", 0.0, Issue05GdAudioBus.Mode.STEREO)]
	s.tags = PackedStringArray(["a", "b c"])
	s.cell = Vector3i(1, -2, 3)
	return s


func _same_bus(a: Issue05GdAudioBus, b: Issue05GdAudioBus) -> bool:
	return a != null and b != null and a.name == b.name and is_equal_approx(a.volume_db, b.volume_db) and a.muted == b.muted and a.mode == b.mode and a.pan.is_equal_approx(b.pan)


func _check_settings(loaded: Resource, label: String) -> void:
	var expected := _make_settings()
	expect(loaded is Issue05GdAudioSettings, "%s: loads as Issue05GdAudioSettings" % label)
	if not loaded is Issue05GdAudioSettings:
		return
	var s: Issue05GdAudioSettings = loaded
	expect(is_equal_approx(s.master_volume, 0.75) and s.title == expected.title and s.quality == expected.quality, "%s: float, string with quotes and enum" % label)
	expect(s.tint.is_equal_approx(expected.tint) and s.cell == expected.cell and s.tags == expected.tags, "%s: Color, Vector3i, PackedStringArray" % label)
	expect(_same_bus(s.main_bus, expected.main_bus), "%s: nested resource" % label)
	expect(s.buses.size() == 2 and _same_bus(s.buses[0], expected.buses[0]) and _same_bus(s.buses[1], expected.buses[1]), "%s: typed array of resources" % label)


func _check_settings_file(ext: String) -> void:
	var path := ROOT + "settings." + ext
	expect_equal(ResourceSaver.save(_make_settings(), path), OK, "save settings as .%s" % ext)
	expect(FileAccess.get_file_as_string(path).length() > 0, ".%s file is not empty" % ext)
	_check_settings(ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE), "." + ext)


func _check_csv() -> void:
	var list := Issue05GdBusList.new()
	list.entries = [_make_bus("Music", -12.0, Issue05GdAudioBus.Mode.SURROUND), _make_bus("Voice, \"main\"", 2.0, Issue05GdAudioBus.Mode.MONO)]
	expect(ResourceTextSerializer.can_serialize(list, ResourceTextSerializer.FORMAT_CSV), "a list of flat records can be saved as CSV")
	expect(not ResourceTextSerializer.can_serialize(_make_settings(), ResourceTextSerializer.FORMAT_CSV), "nested settings cannot be saved as CSV")
	var path := ROOT + "buses.csv"
	expect_equal(ResourceSaver.save(list, path), OK, "save list as .csv")
	# A CSV file has no type marker: it is loaded with a type hint.
	var loaded = ResourceLoader.load(path, "Issue05GdBusList", ResourceLoader.CACHE_MODE_IGNORE)
	expect(loaded is Issue05GdBusList, ".csv loads as Issue05GdBusList with a type hint")
	if loaded is Issue05GdBusList:
		expect(loaded.entries.size() == 2 and _same_bus(loaded.entries[0], list.entries[0]) and _same_bus(loaded.entries[1], list.entries[1]), ".csv: rows with comma and quotes round-trip")


func _check_string_api() -> void:
	var json := ResourceTextSerializer.serialize(_make_settings(), ResourceTextSerializer.FORMAT_JSON)
	expect(json.contains("Cleo"), "serialize(FORMAT_JSON) returns text")
	_check_settings(ResourceTextSerializer.deserialize(json, ResourceTextSerializer.FORMAT_JSON), "deserialize(FORMAT_JSON)")
	_check_settings(ResourceTextSerializer.deserialize(ResourceTextSerializer.serialize(_make_settings(), ResourceTextSerializer.FORMAT_INI), ResourceTextSerializer.FORMAT_INI), "deserialize(FORMAT_INI)")
	expect(ResourceTextSerializer.is_format_available(ResourceTextSerializer.FORMAT_JSON) and not ResourceTextSerializer.is_format_available(ResourceTextSerializer.FORMAT_YAML), "is_format_available(): JSON yes, YAML no")
	expect(ResourceTextSerializer.get_available_formats().has(ResourceTextSerializer.FORMAT_CSV), "get_available_formats() lists FORMAT_CSV")
	expect_equal(ResourceTextSerializer.get_format_from_extension(".INI"), ResourceTextSerializer.FORMAT_INI, "get_format_from_extension(\".INI\")")
	expect_equal(ResourceTextSerializer.get_format_extension(ResourceTextSerializer.FORMAT_CSV), "csv", "get_format_extension(FORMAT_CSV)")
	expect_equal(ResourceTextSerializer.TEXT_LAYOUT_VERSION, 1, "TEXT_LAYOUT_VERSION is 1")
	expect(not ClassDB.class_has_method("ResourceTextSerializer", "get_formats") and not ClassDB.class_has_method("ResourceTextSerializer", "can_save_as_csv"), "old string API is gone")


func _options() -> SerializerOptions:
	return SerializerOptions.new()


func _check_serializer_options() -> void:
	var settings := _make_settings()
	var json := ResourceTextSerializer.FORMAT_JSON
	var defaults := _options()
	expect(defaults.indent == "  " and defaults.enums_as_names and defaults.defaults == null and defaults.include_type_marker and not defaults.sort_keys and not defaults.full_precision_floats, "SerializerOptions.new(): common defaults")
	expect(defaults.csv_delimiter == "," and defaults.csv_headers and not defaults.ini_plain_values and defaults.xml_declaration, "SerializerOptions.new(): format defaults")
	expect_equal(ResourceTextSerializer.serialize(settings, json, defaults), ResourceTextSerializer.serialize(settings, json), "new() gives the same text as null (project settings)")

	var options := _options()
	options.indent = "\t"
	expect(ResourceTextSerializer.serialize(settings, json, options).contains("\n\t\""), "indent")

	options = _options()
	options.enums_as_names = false
	var numbers := ResourceTextSerializer.serialize(settings, json, options)
	expect(numbers.contains("\"quality\": 2"), "enums_as_names = false writes numbers")
	_check_settings(ResourceTextSerializer.deserialize(numbers, json), "enums_as_names = false")

	options = _options()
	options.defaults = _make_settings()
	var changed := ResourceTextSerializer.serialize(settings, json, options)
	expect(not changed.contains("master_volume") and changed.contains("$type"), "defaults: unchanged values are not written")

	options = _options()
	options.include_type_marker = false
	var untyped := ResourceTextSerializer.serialize(settings, json, options)
	expect(not untyped.contains("\"$type\""), "include_type_marker = false")
	_check_settings(ResourceTextSerializer.deserialize(untyped, json, &"Issue05GdAudioSettings"), "include_type_marker = false with type hint")

	options = _options()
	options.sort_keys = true
	var plain := ResourceTextSerializer.serialize(settings, json)
	var sorted := ResourceTextSerializer.serialize(settings, json, options)
	expect(plain.find("\"title\"") < plain.find("\"quality\"") and sorted.find("\"quality\"") < sorted.find("\"title\""), "sort_keys")
	_check_settings(ResourceTextSerializer.deserialize(sorted, json), "sort_keys")

	options = _options()
	options.full_precision_floats = true
	var bus := _make_bus("Pan", 0.0, Issue05GdAudioBus.Mode.STEREO)
	bus.pan = Vector2(0.1, 0.5)
	expect(ResourceTextSerializer.serialize(bus, json, options).contains("0.10000000149011612") and not ResourceTextSerializer.serialize(bus, json).contains("0.10000000149011612"), "full_precision_floats")

	var list := Issue05GdBusList.new()
	list.entries = [_make_bus("Music", -12.0, Issue05GdAudioBus.Mode.SURROUND)]
	options = _options()
	options.csv_delimiter = ";"
	expect(ResourceTextSerializer.serialize(list, ResourceTextSerializer.FORMAT_CSV, options).contains("Music;"), "csv_delimiter")
	options.csv_headers = false
	var rows := ResourceTextSerializer.serialize(list, ResourceTextSerializer.FORMAT_CSV, options)
	expect(rows.begins_with("Music;"), "csv_headers = false: no header row")
	var back = ResourceTextSerializer.deserialize(rows, ResourceTextSerializer.FORMAT_CSV, &"Issue05GdBusList", options)
	expect(back is Issue05GdBusList and back.entries.size() == 1 and _same_bus(back.entries[0], list.entries[0]), "csv_headers = false round trip")

	options = _options()
	options.ini_plain_values = true
	var ini := ResourceTextSerializer.serialize(settings, ResourceTextSerializer.FORMAT_INI, options)
	expect(ini.contains("cell=1, -2, 3"), "ini_plain_values")
	_check_settings(ResourceTextSerializer.deserialize(ini, ResourceTextSerializer.FORMAT_INI), "ini_plain_values")

	options = _options()
	options.xml_declaration = false
	var xml := ResourceTextSerializer.serialize(settings, ResourceTextSerializer.FORMAT_XML, options)
	expect(xml.begins_with("<resource") and ResourceTextSerializer.serialize(settings, ResourceTextSerializer.FORMAT_XML).begins_with("<?xml"), "xml_declaration")
	_check_settings(ResourceTextSerializer.deserialize(xml, ResourceTextSerializer.FORMAT_XML), "xml_declaration = false")

	# A preset saved as .tres gives the same output.
	options = _options()
	options.indent = "\t"
	options.sort_keys = true
	options.enums_as_names = false
	var preset_path := ROOT + "options.tres"
	expect_equal(ResourceSaver.save(options, preset_path), OK, "save SerializerOptions as .tres")
	var preset = ResourceLoader.load(preset_path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(preset is SerializerOptions and ResourceTextSerializer.serialize(settings, json, preset) == ResourceTextSerializer.serialize(settings, json, options), "SerializerOptions .tres gives the same output")


func _check_plain_json() -> void:
	var path := ROOT + "plain.json"
	var f := FileAccess.open(path, FileAccess.WRITE)
	f.store_string("{\"answer\": 42}")
	f.close()
	var loaded = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(loaded is JSON, "plain JSON without type marker still loads as JSON")
	expect(loaded is JSON and int(loaded.data.get("answer", 0)) == 42, "plain JSON data is intact")


func _cleanup() -> void:
	for file in DirAccess.get_files_at(ROOT):
		DirAccess.remove_absolute(ROOT + file)
	DirAccess.remove_absolute(ROOT)
