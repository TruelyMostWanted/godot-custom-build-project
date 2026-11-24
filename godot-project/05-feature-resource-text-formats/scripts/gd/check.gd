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
	expect(ResourceTextSerializer.can_save_as_csv(list), "a list of flat records can be saved as CSV")
	expect(not ResourceTextSerializer.can_save_as_csv(_make_settings()), "nested settings cannot be saved as CSV")
	var path := ROOT + "buses.csv"
	expect_equal(ResourceSaver.save(list, path), OK, "save list as .csv")
	# A CSV file has no type marker: it is loaded with a type hint.
	var loaded = ResourceLoader.load(path, "Issue05GdBusList", ResourceLoader.CACHE_MODE_IGNORE)
	expect(loaded is Issue05GdBusList, ".csv loads as Issue05GdBusList with a type hint")
	if loaded is Issue05GdBusList:
		expect(loaded.entries.size() == 2 and _same_bus(loaded.entries[0], list.entries[0]) and _same_bus(loaded.entries[1], list.entries[1]), ".csv: rows with comma and quotes round-trip")


func _check_string_api() -> void:
	var json := ResourceTextSerializer.serialize(_make_settings(), &"json")
	expect(json.contains("Cleo"), "serialize(&\"json\") returns text")
	_check_settings(ResourceTextSerializer.deserialize(json, &"json"), "deserialize(&\"json\")")
	_check_settings(ResourceTextSerializer.deserialize(ResourceTextSerializer.serialize(_make_settings(), &"ini"), &"ini"), "deserialize(&\"ini\")")


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
