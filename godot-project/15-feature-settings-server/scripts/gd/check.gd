extends FeatureCheck
## GDScript checks of 15-feature-settings-server. Check the same things as scripts/cs/Issue15FeatureSettingsServerCheck.cs.
##
## The domains "check15_gd" (GDScript) and "check15_cs" (C#) are listed in the project setting
## settings/startup/custom_servers. The restart check starts the project a second time as its own
## process (desktop only) and reads what was applied before the first frame.

const DIR := "user://15-feature-settings-server"
const DOMAIN_SCRIPT := "res://15-feature-settings-server/scripts/gd/check_domain.gd"
const LIST_SCRIPT := "res://15-feature-settings-server/scripts/gd/check_list_domain.gd"
const PROBE_SCENE := "res://15-feature-settings-server/scenes/restart_probe.tscn"
const PLAYER_FILE := "user://settings/check15_gd.ini"


func _get_check_name() -> String:
	return "15-feature-settings-server"


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	expect(Engine.has_singleton("SettingsServerManager"), "SettingsServerManager is a registered singleton")
	expect(ClassDB.class_exists("SettingsServer") and ClassDB.can_instantiate("SettingsServer"), "SettingsServer is a virtual class that scripts extend")
	expect(ClassDB.is_parent_class("ListSettingsServer", "SettingsServer"), "ListSettingsServer extends SettingsServer")
	expect_equal(SettingsServerManager.get_startup_phase(), SettingsServer.APPLY_PHASE_SCENE, "all startup phases ran before the main scene")
	expect_equal(SettingsServerManager.get_storage_folder(), "user://settings", "default storage folder")
	expect_equal(SettingsServerManager.get_storage_format(), ResourceTextSerializer.FORMAT_INI, "default storage format is the enum value FORMAT_INI")
	expect(not SettingsServerManager.has_dirty_servers(), "has_dirty_servers() is false after startup")
	expect(SettingsServerManager.has_server(&"check15_cs"), "the C# domain from settings/startup/custom_servers is registered")

	var server: SettingsServer = SettingsServerManager.get_server(&"check15_gd")
	if not expect(server != null, "the GDScript domain from settings/startup/custom_servers is registered"):
		return
	expect(server.is_started(), "the GDScript domain was started before the main scene")
	expect_equal(server.get_apply_phase(), SettingsServer.APPLY_PHASE_PRE_DISPLAY, "#71: the script domain asks for APPLY_PHASE_PRE_DISPLAY and was still started in APPLY_PHASE_SCENE")
	expect_equal(server.get_file_path(), PLAYER_FILE, "default file path")
	expect_equal(server.get_setting_names(), PackedStringArray(["volume", "quality"]), "setting names come from the resource")

	_check_validation()
	_check_revert()
	_check_formats()
	_check_broken_file()
	_check_list_domain()
	await _check_confirmation()
	_check_apply_during_confirmation()
	_check_restart_values()
	_check_list_defaults()
	_check_entry_validation()
	_check_project_paths()
	_check_failure_signals()
	await _check_restart(server)

	_remove_dir(DIR)


func _new_domain(path: String) -> SettingsServer:
	var domain: SettingsServer = load(DOMAIN_SCRIPT).new()
	domain.set_apply_mode(SettingsServer.APPLY_MODE_MANUAL)
	domain.set_file_path(path)
	return domain


func _check_validation() -> void:
	var domain := _new_domain(DIR.path_join("validation.ini"))
	domain.set_value(&"volume", 4.0)
	expect(is_equal_approx(domain.get_value(&"volume"), 1.0), "a range hint clamps the value")
	domain.set_value(&"quality", 9)
	expect_equal(domain.get_value(&"quality"), 1, "an unknown enum value is rejected (an error is printed)")
	expect(is_equal_approx(domain.get_default_value(&"volume"), 0.5), "default value from the resource")
	domain.free()


func _check_revert() -> void:
	var domain := _new_domain(DIR.path_join("revert.ini"))
	domain.set_value(&"volume", 0.8)
	expect_equal(domain.apply(), OK, "apply() in manual mode")
	domain.set_value(&"volume", 0.2)
	domain.set_value(&"quality", 2)
	expect(domain.is_dirty(), "set_value() in manual mode makes the domain dirty")
	expect_equal(domain.get_dirty_setting_names(), PackedStringArray(["volume", "quality"]), "get_dirty_setting_names() lists the changed settings")
	domain.revert()
	expect(not domain.is_dirty(), "revert() clears the dirty state")
	expect(is_equal_approx(domain.get_value(&"volume"), 0.8), "revert() gives back the last applied volume")
	expect_equal(domain.get_value(&"quality"), 1, "revert() gives back the last applied quality")
	domain.reset_to_defaults()
	expect(is_equal_approx(domain.get_value(&"volume"), 0.5), "reset_to_defaults()")
	domain.free()


func _check_formats() -> void:
	for extension in ["ini", "json"]:
		var path := DIR.path_join("single." + extension)
		var writer := _new_domain(path)
		writer.set_value(&"volume", 0.3)
		writer.set_value(&"quality", 0)
		writer.apply()
		expect_equal(writer.save(), OK, "save() as %s" % extension)
		writer.free()
		expect(FileAccess.get_file_as_string(path).contains("settings_version"), "%s file has the settings version" % extension)

		var reader := _new_domain(path)
		expect_equal(reader.load(), OK, "load() as %s" % extension)
		expect(is_equal_approx(reader.get_value(&"volume"), 0.3), "%s: volume loaded" % extension)
		expect_equal(reader.get_value(&"quality"), 0, "%s: quality loaded" % extension)
		reader.free()

	var only_path := DIR.path_join("only_changed.ini")
	var only_changed := _new_domain(only_path)
	only_changed.set_value(&"quality", 2)
	only_changed.apply()
	only_changed.save()
	var only_text := FileAccess.get_file_as_string(only_path)
	expect(only_text.contains("quality") and not only_text.contains("volume"), "only values that differ from the defaults are written")
	only_changed.free()


func _check_broken_file() -> void:
	var path := DIR.path_join("broken.json")
	var broken := "{ \"volume\": 0.25, "
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_string(broken)
	file.close()
	var domain := _new_domain(path)
	expect(domain.load() != OK, "a broken file is reported (a warning is printed)")
	expect(is_equal_approx(domain.get_value(&"volume"), 0.5), "a broken file falls back to the defaults")
	expect_equal(FileAccess.get_file_as_string(path), broken, "a broken file is not overwritten by load()")
	domain.free()

	var missing := _new_domain(DIR.path_join("missing.ini"))
	expect_equal(missing.load(), ERR_FILE_NOT_FOUND, "a missing file keeps the defaults")
	missing.free()


func _make_entry(entry_name: String, level: int) -> Issue15CheckEntry:
	var entry := Issue15CheckEntry.new()
	entry.entry_name = entry_name
	entry.level = level
	return entry


func _check_list_domain() -> void:
	var list: ListSettingsServer = load(LIST_SCRIPT).new()
	list.set_apply_mode(SettingsServer.APPLY_MODE_MANUAL)
	list.add_entry(_make_entry("a", 1))
	list.add_entry(_make_entry("b", 2))
	list.add_entry(_make_entry("c", 3))
	list.move_entry(2, 0)
	list.remove_entry(1)
	list.set_entry_value(1, &"level", 5)
	expect_equal(list.get_entry_count(), 2, "list: add, move, remove")
	expect_equal(list.find_entry("c"), 0, "list: find_entry() by key")
	list.apply()
	for extension in ["csv", "json", "ini"]:
		var path := DIR.path_join("list." + extension)
		list.set_file_path(path)
		expect_equal(list.save(), OK, "list: save() as %s" % extension)
		var reader: ListSettingsServer = load(LIST_SCRIPT).new()
		reader.set_file_path(path)
		expect_equal(reader.load(), OK, "list: load() as %s" % extension)
		expect_equal(reader.get_entry_count(), 2, "list %s: entry count" % extension)
		if reader.get_entry_count() == 2:
			expect_equal(reader.get_entry_value(0, &"entry_name"), "c", "list %s: first entry" % extension)
			expect_equal(reader.get_entry_value(1, &"level"), 5, "list %s: changed level" % extension)
		reader.free()
	list.free()


func _check_confirmation() -> void:
	var domain := _new_domain(DIR.path_join("confirm.ini"))
	domain.set_value(&"quality", 2)
	expect_equal(domain.apply_with_confirmation(0.2), OK, "apply_with_confirmation()")
	expect(domain.is_confirmation_pending(), "a confirmation is pending")
	expect_equal(domain.get_applied_value(&"quality"), 2, "the new value is applied while waiting")
	await get_tree().create_timer(0.6).timeout
	expect(not domain.is_confirmation_pending(), "the confirmation timed out")
	expect_equal(domain.get_value(&"quality"), 1, "without confirm_changes() the old value comes back")

	domain.set_value(&"quality", 0)
	domain.apply_with_confirmation(5.0)
	domain.confirm_changes()
	expect_equal(domain.get_applied_value(&"quality"), 0, "confirm_changes() keeps the new value")

	domain.set_value(&"quality", 2)
	domain.apply_with_confirmation(5.0)
	domain.cancel_confirmation()
	expect(not domain.is_confirmation_pending(), "cancel_confirmation() ends the confirmation")
	expect_equal(domain.get_applied_value(&"quality"), 0, "cancel_confirmation() gives back the confirmed value")

	var finished := [false]
	domain.read_from_engine_finished.connect(func() -> void: finished[0] = true)
	domain.read_from_engine()
	expect(finished[0], "read_from_engine() emits read_from_engine_finished")
	domain.free()


func _check_apply_during_confirmation() -> void:
	var domain := _new_domain(DIR.path_join("confirm71.ini"))
	domain.set_setting_flags(&"quality", SettingsServer.SETTING_FLAG_CONFIRM)
	var finished := []
	domain.confirmation_finished.connect(func(kept: bool) -> void: finished.append(kept))
	domain.set_value(&"quality", 2)
	domain.apply()
	expect(domain.is_confirmation_pending(), "#71: a flagged value starts a confirmation")
	domain.set_value(&"volume", 0.3)
	expect_equal(domain.apply(), OK, "#71: apply() during a pending confirmation")
	expect(not domain.is_confirmation_pending(), "#71: apply() kept the pending confirmation first")
	expect_equal(finished, [true], "#71: confirmation_finished(true) for the kept confirmation")
	expect_equal(domain.get_applied_value(&"quality"), 2, "#71: the kept value stays applied")

	domain.set_value(&"quality", 0)
	domain.apply_with_confirmation(5.0)
	domain.set_value(&"quality", 1)
	expect_equal(domain.apply_with_confirmation(5.0), OK, "#71: apply_with_confirmation() during a pending one (no ERR_BUSY)")
	domain.cancel_confirmation()
	expect_equal(domain.get_applied_value(&"quality"), 0, "#71: a cancel only reverts the values of the new confirmation")
	expect(is_equal_approx(domain.get_applied_value(&"volume"), 0.3), "#71: values applied before stay")
	domain.free()


func _check_restart_values() -> void:
	var domain := _new_domain(DIR.path_join("restart71.ini"))
	domain.set_setting_flags(&"quality", SettingsServer.SETTING_FLAG_RESTART_REQUIRED)
	var lists := []
	domain.restart_required.connect(func(names: PackedStringArray) -> void: lists.append(names))
	domain.set_value(&"quality", 2)
	domain.apply()
	expect_equal(domain.get_restart_required_names(), PackedStringArray(["quality"]), "#71: a changed restart value is listed")
	domain.set_value(&"quality", 1)
	domain.apply()
	expect(domain.get_restart_required_names().is_empty(), "#71: the value set back to the start value is no longer listed")
	expect_equal(lists, [PackedStringArray(["quality"]), PackedStringArray()], "#71: restart_required carries the whole list, empty when cleared")
	domain.free()


func _check_list_defaults() -> void:
	var list: ListSettingsServer = load(LIST_SCRIPT).new()
	var path := DIR.path_join("list_defaults.ini")
	list.set_file_path(path)
	var list_defaults: Issue15CheckList = list.get_default_settings()
	list_defaults.entries.append(_make_entry("a", 1))
	list.reset_to_defaults()
	expect_equal(list.get_entry_count(), 1, "#71: the list has the default entry")
	expect(list.get_entry(0) != list_defaults.entries[0], "#71: the entry is a copy of the default entry")
	expect_equal(list.save(), OK, "#71: save() of an unchanged list")
	expect(not FileAccess.get_file_as_string(path).contains("entries"), "#71: entries equal to the defaults are not written")
	list.set_entry_value(0, &"level", 4)
	list.save()
	expect(FileAccess.get_file_as_string(path).contains("entries"), "#71: a changed entry writes the list")
	list.free()


func _check_entry_validation() -> void:
	var list: ListSettingsServer = load(LIST_SCRIPT).new()
	list.set_apply_mode(SettingsServer.APPLY_MODE_MANUAL)
	list.add_entry(_make_entry("a", 1))
	list.set_entry_value(0, &"level", 50)
	expect_equal(list.get_entry_value(0, &"level"), 10, "#71: set_entry_value() clamps to the range hint of the entry")
	list.set_entry_value(0, &"level", -3)
	expect_equal(list.get_entry_value(0, &"level"), 0, "#71: set_entry_value() clamps the lower end")

	var path := DIR.path_join("list_clamp.json")
	list.set_entry_value(0, &"level", 7)
	list.apply()
	list.set_file_path(path)
	list.save()
	var text := FileAccess.get_file_as_string(path)
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_string(text.replace("7", "99"))
	file.close()
	var reader: ListSettingsServer = load(LIST_SCRIPT).new()
	reader.set_file_path(path)
	expect_equal(reader.load(), OK, "#71: load() of a hand-edited list")
	if reader.get_entry_count() == 1:
		expect_equal(reader.get_entry_value(0, &"level"), 10, "#71: hand-edited entry values are clamped on load")
	reader.free()
	list.free()


func _check_project_paths() -> void:
	# Every registered domain (engine and script domains) gets the project setting of its file path.
	for domain_name in SettingsServerManager.get_domains():
		expect(ProjectSettings.has_setting("settings/paths/%s_settings_path" % domain_name), "project setting settings/paths/%s_settings_path exists" % domain_name)
	var setting := "settings/paths/check15_gd_settings_path"
	expect_equal(ProjectSettings.get_setting(setting), "", "the path setting is empty by default")

	for extension in ["json", "ini", "xml", "toml", "yaml"]:
		if not ResourceTextSerializer.is_format_available(ResourceTextSerializer.get_format_from_extension(extension)):
			print("  SKIP  project path .%s (module not in this build)" % extension)
			continue
		var path := DIR.path_join("project_path." + extension)
		ProjectSettings.set_setting(setting, path)
		var writer: SettingsServer = load(DOMAIN_SCRIPT).new()
		writer.set_apply_mode(SettingsServer.APPLY_MODE_MANUAL)
		expect_equal(writer.get_file_path(), path, "%s: get_file_path() returns the project setting" % extension)
		writer.set_value(&"volume", 0.3)
		writer.set_value(&"quality", 2)
		writer.apply() # save() writes the applied values.
		expect_equal(writer.save(), OK, "%s: save() to the project path" % extension)
		writer.free()
		var reader: SettingsServer = load(DOMAIN_SCRIPT).new()
		expect_equal(reader.load(), OK, "%s: load() from the project path" % extension)
		expect(is_equal_approx(reader.get_value(&"volume"), 0.3), "%s: volume loaded" % extension)
		expect_equal(reader.get_value(&"quality"), 2, "%s: quality loaded" % extension)
		reader.free()
	expect(FileAccess.get_file_as_string(DIR.path_join("project_path.json")).strip_edges().begins_with("{"), "the .json path is written as JSON")
	expect(FileAccess.get_file_as_string(DIR.path_join("project_path.xml")).strip_edges().begins_with("<"), "the .xml path is written as XML")

	# A path set at runtime wins over the project setting.
	ProjectSettings.set_setting(setting, DIR.path_join("project_path.json"))
	var domain: SettingsServer = load(DOMAIN_SCRIPT).new()
	domain.set_file_path(DIR.path_join("runtime.ini"))
	expect_equal(domain.get_file_path(), DIR.path_join("runtime.ini"), "set_file_path() wins over the project setting")
	domain.set_file_path("")
	expect_equal(domain.get_file_path(), DIR.path_join("project_path.json"), "an empty runtime path gives the project setting back")

	# An unknown extension prints an error and uses settings/storage/format (INI).
	var unknown := DIR.path_join("project_path.cfg")
	ProjectSettings.set_setting(setting, unknown)
	domain.set_apply_mode(SettingsServer.APPLY_MODE_MANUAL)
	domain.set_value(&"quality", 0)
	domain.apply()
	expect_equal(domain.save(), OK, "an unknown extension is saved in the global format (an error is printed)")
	var text := FileAccess.get_file_as_string(unknown).strip_edges()
	expect(not text.begins_with("{") and not text.begins_with("<"), "an unknown extension is written as INI")
	domain.free()

	ProjectSettings.set_setting(setting, "")
	var fallback: SettingsServer = load(DOMAIN_SCRIPT).new()
	expect_equal(fallback.get_file_path(), PLAYER_FILE, "an empty project setting gives the folder and format path")
	fallback.free()


func _check_failure_signals() -> void:
	var events := []
	var path := DIR.path_join("signals.json")
	var domain := _new_domain(path)
	domain.load_failed.connect(func(error: Error, file_path: String): events.append(["load_failed", error, file_path]))
	domain.save_failed.connect(func(error: Error, file_path: String): events.append(["save_failed", error, file_path]))
	domain.settings_changed.connect(func(names: PackedStringArray): events.append(["settings_changed", names]))

	expect_equal(domain.load(), ERR_FILE_NOT_FOUND, "load() without a file")
	expect(events.is_empty(), "a missing file emits no load_failed")

	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_string("{ \"volume\": ")
	file.close()
	var load_error := domain.load()
	expect(load_error != OK, "a broken file is reported (a warning is printed)")
	expect(events.size() == 1 and events[0] == ["load_failed", load_error, path], "load_failed(error, path) for a broken file")
	events.clear()

	# A folder that can't be created: its parent is a file.
	var blocker := DIR.path_join("blocker.txt")
	file = FileAccess.open(blocker, FileAccess.WRITE)
	file.store_string("not a folder")
	file.close()
	var bad_path := blocker.path_join("sub").path_join("signals.ini")
	domain.set_file_path(bad_path)
	var save_error := domain.save()
	expect(save_error != OK, "save() into a folder that can't be created fails (an error is printed)")
	expect(events.size() == 1 and events[0] == ["save_failed", save_error, bad_path], "save_failed(error, path)")
	events.clear()

	domain.set_value(&"volume", 0.7)
	expect(events.size() == 1 and events[0] == ["settings_changed", PackedStringArray(["volume"])], "settings_changed(names) after set_value()")
	events.clear()
	domain.revert()
	expect(events.size() == 1 and events[0] == ["settings_changed", PackedStringArray(["volume"])], "settings_changed(names) after revert()")
	domain.free()


func _check_restart(server: SettingsServer) -> void:
	if not OS.has_feature("pc"):
		print("  SKIP  restart check (needs a desktop platform to start a second process)")
		return
	var volume := snappedf(randf_range(0.1, 0.9), 0.01)
	server.set_value(&"volume", volume)
	server.set_value(&"quality", 2)
	expect(not server.is_dirty(), "immediate mode applies set_value() at once")
	expect_equal(server.save(), OK, "save() writes the player file")
	expect(FileAccess.file_exists(PLAYER_FILE), "the player file is in user://settings/")

	var probe := _run_probe()
	expect(probe.get("gd_started", false), "restart: the domain was started")
	var applied: Dictionary = probe.get("gd_applied", {})
	expect(is_equal_approx(applied.get("volume", -1.0), volume), "restart: the saved volume was applied")
	expect_equal(int(applied.get("quality", -1)), 2, "restart: the saved quality was applied")
	expect_equal(int(applied.get("frame", -1)), 0, "restart: applied before the first frame")
	expect_equal(int(probe.get("ready_frame", -1)), 0, "restart: the main scene was ready before the first frame")

	# Leave no player file behind for later runs.
	server.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(PLAYER_FILE)


func _run_probe() -> Dictionary:
	var output := []
	var args := PackedStringArray(["--headless", "--path", ProjectSettings.globalize_path("res://"), PROBE_SCENE])
	var code := OS.execute(OS.get_executable_path(), args, output, true)
	expect_equal(code, 0, "restart: the probe process ran")
	for chunk in output:
		for line in str(chunk).split("\n"):
			if line.begins_with("CHECK15_PROBE "):
				var parsed = JSON.parse_string(line.trim_prefix("CHECK15_PROBE "))
				if parsed is Dictionary:
					return parsed
	expect(false, "restart: the probe printed its result")
	return {}


func _remove_dir(path: String) -> void:
	for file_name in DirAccess.get_files_at(path):
		DirAccess.remove_absolute(path.path_join(file_name))
	DirAccess.remove_absolute(path)
