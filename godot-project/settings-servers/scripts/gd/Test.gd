extends FeatureTest
## Full API test of Settings Servers: SettingsServer, ListSettingsServer, SettingsServerManager. Same tests as scripts/cs/Test.cs.
## The domains "test_gd" (GDScript) and "test_cs" (C#) are listed in settings/startup/custom_servers and started before the main scene.

const DIR := "user://settings-servers-test-gd"
const DOMAIN_SCRIPT := "res://settings-servers/scripts/gd/test_domain.gd"
const LIST_SCRIPT := "res://settings-servers/scripts/gd/test_list_domain.gd"
const PROBE_SCENE := "res://settings-servers/scenes/restart_probe.tscn"
const PLAYER_FILE := "user://settings/test_gd.ini"
const DEFAULT_VOLUME := 0.5
const DEFAULT_QUALITY := 1
const MAX_LEVEL := 10

var _events: Array = []
var _bound_value: int = 0


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	_test_constants()
	_test_startup()
	_test_domain_basics()
	_test_validation()
	_test_values_and_signals()
	_test_manual_mode()
	_test_float_precision()
	_test_save_and_load()
	_test_load_failures()
	_test_migration()
	await _test_confirmation()
	_test_apply_during_confirmation()
	_test_restart_required()
	_test_bindings()
	_test_session_override()
	_test_manager()
	_test_list_domain()
	_test_list_files()
	_test_list_defaults_and_validation()
	_test_project_paths()
	_test_viewport_target()
	await _test_restart()
	_remove_dir(DIR)


func _new_domain(file_name: String) -> SettingsServer:
	var domain: SettingsServer = load(DOMAIN_SCRIPT).new()
	domain.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	domain.file_path = DIR.path_join(file_name)
	return domain


func _new_list() -> ListSettingsServer:
	var list: ListSettingsServer = load(LIST_SCRIPT).new()
	list.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	return list


func _new_entry(entry_name: String, level: int) -> SettingsServersTestEntry:
	var entry := SettingsServersTestEntry.new()
	entry.entry_name = entry_name
	entry.level = level
	return entry


func _record(name: String) -> Callable:
	return func(...args: Array) -> void: _events.append([name] + args)


func _test_constants() -> void:
	expect_equal(SettingsServer.APPLY_MODE_IMMEDIATE, 0, "APPLY_MODE_IMMEDIATE is 0")
	expect_equal(SettingsServer.APPLY_MODE_MANUAL, 1, "APPLY_MODE_MANUAL is 1")
	expect_equal(SettingsServer.APPLY_PHASE_NONE, -1, "APPLY_PHASE_NONE is -1")
	expect_equal(SettingsServer.APPLY_PHASE_PRE_DISPLAY, 0, "APPLY_PHASE_PRE_DISPLAY is 0")
	expect_equal(SettingsServer.APPLY_PHASE_SERVERS, 1, "APPLY_PHASE_SERVERS is 1")
	expect_equal(SettingsServer.APPLY_PHASE_SCENE, 2, "APPLY_PHASE_SCENE is 2")
	expect_equal(SettingsServer.APPLY_PHASE_MAX, 3, "APPLY_PHASE_MAX is 3")
	expect_equal(SettingsServer.SETTING_FLAG_NONE, 0, "SETTING_FLAG_NONE is 0")
	expect_equal(SettingsServer.SETTING_FLAG_RESTART_REQUIRED, 1, "SETTING_FLAG_RESTART_REQUIRED is 1")
	expect_equal(SettingsServer.SETTING_FLAG_CONFIRM, 2, "SETTING_FLAG_CONFIRM is 2")
	expect_equal(SettingsServer.SETTING_FLAG_NO_ENGINE_TARGET, 4, "SETTING_FLAG_NO_ENGINE_TARGET is 4")
	expect_equal(SettingsServer.VIEWPORT_TARGET_ROOT_ONLY, 0, "VIEWPORT_TARGET_ROOT_ONLY is 0")
	expect_equal(SettingsServer.VIEWPORT_TARGET_GROUP, 1, "VIEWPORT_TARGET_GROUP is 1")
	expect_equal(SettingsServer.VIEWPORT_TARGET_ALL_VIEWPORTS, 2, "VIEWPORT_TARGET_ALL_VIEWPORTS is 2")
	expect(ClassDB.is_parent_class("ListSettingsServer", "SettingsServer"), "ListSettingsServer extends SettingsServer")


func _test_startup() -> void:
	expect(Engine.has_singleton("SettingsServerManager"), "SettingsServerManager is a registered singleton")
	expect_equal(SettingsServerManager.get_startup_phase(), SettingsServer.APPLY_PHASE_SCENE, "all startup phases ran before the main scene")
	expect_equal(SettingsServerManager.get_storage_folder(), "user://settings", "default storage folder")
	expect_equal(SettingsServerManager.get_storage_format(), ResourceTextSerializer.FORMAT_INI, "default storage format is FORMAT_INI")
	expect(not SettingsServerManager.has_dirty_servers(), "has_dirty_servers is false after startup")
	expect(SettingsServerManager.has_server(&"test_cs"), "the C# domain from custom_servers is registered")
	var server: SettingsServer = SettingsServerManager.get_server(&"test_gd")
	expect(server != null, "the GDScript domain from custom_servers is registered")
	expect(server.is_started(), "the domain was started before the main scene")
	expect_equal(server.get_apply_phase(), SettingsServer.APPLY_PHASE_PRE_DISPLAY, "the domain asks for APPLY_PHASE_PRE_DISPLAY (a script domain still starts in APPLY_PHASE_SCENE)")
	expect_equal(server.file_path, PLAYER_FILE, "default file path")


func _test_domain_basics() -> void:
	var domain := _new_domain("basics.ini")
	expect_equal(domain.get_domain(), &"test_gd", "get_domain returns the domain name")
	expect(not domain.is_started(), "a domain that is not registered is not started")
	expect_equal(domain.get_format_version(), 2, "get_format_version returns the version of the domain")
	expect_equal(domain.get_setting_names(), PackedStringArray(["volume", "quality", "label", "hidden"]), "get_setting_names comes from the resource")
	expect(domain.has_setting(&"volume"), "has_setting finds volume")
	expect(not domain.has_setting(&"missing"), "has_setting does not find a missing name")
	expect(domain.is_value_supported(&"volume"), "is_value_supported is true for a supported setting")
	expect(not domain.is_value_supported(&"hidden"), "is_value_supported follows _is_value_supported")
	expect(not domain.is_value_supported(&"missing"), "is_value_supported is false for a missing setting")
	expect_equal(domain.get_default_value(&"volume"), DEFAULT_VOLUME, "get_default_value comes from the resource")
	expect_equal(domain.get_default_value(&"label"), "engine", "the defaults capture _read_from_engine")
	var settings: Resource = domain.get_settings()
	var applied: Resource = domain.get_applied_settings()
	var defaults: Resource = domain.get_default_settings()
	expect(settings is SettingsServersTestSettings, "get_settings returns the settings resource")
	expect(applied != settings, "get_applied_settings is a separate copy")
	expect(defaults != settings, "get_default_settings is a separate copy")
	domain.free()


func _test_validation() -> void:
	var domain := _new_domain("validation.ini")
	domain.set_value(&"volume", 4.0)
	expect_equal(domain.get_value(&"volume"), 1.0, "a range hint clamps the value")
	domain.set_value(&"quality", 9)
	expect_equal(domain.get_value(&"quality"), DEFAULT_QUALITY, "an unknown enum value is rejected (an error is printed)")
	domain.set_value(&"label", "  trimmed  ")
	expect_equal(domain.get_value(&"label"), "trimmed", "_validate_value changes the value")
	domain.free()


func _test_values_and_signals() -> void:
	var domain := _new_domain("values.ini")
	domain.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	_events.clear()
	domain.value_changed.connect(_record("value_changed"))
	domain.settings_changed.connect(_record("settings_changed"))
	domain.applied.connect(_record("applied"))
	domain.reset.connect(_record("reset"))
	domain.set_value(&"volume", 0.8)
	expect_equal(domain.get_value(&"volume"), 0.8, "set_value changes the pending value")
	expect_equal(domain.get_applied_value(&"volume"), 0.8, "immediate mode applies at once")
	expect_equal(_events, [["value_changed", &"volume", 0.8], ["settings_changed", PackedStringArray(["volume"])], ["applied", PackedStringArray(["volume"])]], "set_value emits value_changed, settings_changed, applied")
	_events.clear()
	domain.set_value(&"volume", 0.8)
	expect(_events.is_empty(), "the same value again is no change")
	domain.reset_value(&"volume")
	expect_equal(domain.get_value(&"volume"), DEFAULT_VOLUME, "reset_value goes back to the default")
	domain.set_value(&"quality", 2)
	_events.clear()
	domain.reset_to_defaults()
	expect_equal(domain.get_value(&"quality"), DEFAULT_QUALITY, "reset_to_defaults goes back to the defaults")
	expect(_events.has(["reset"]), "reset_to_defaults emits reset")
	domain.free()


func _test_manual_mode() -> void:
	var domain := _new_domain("manual.ini")
	_events.clear()
	domain.applied.connect(_record("applied"))
	domain.set_value(&"volume", 0.8)
	expect_equal(domain.apply(), OK, "apply in manual mode")
	expect(domain.has_unsaved_changes(), "an applied change is unsaved")
	expect_equal(_events, [["applied", PackedStringArray(["volume"])]], "apply emits applied with the names")
	domain.set_value(&"volume", 0.2)
	domain.set_value(&"quality", 2)
	expect(domain.is_dirty(), "set_value in manual mode makes the domain dirty")
	expect_equal(domain.get_dirty_setting_names(), PackedStringArray(["volume", "quality"]), "get_dirty_setting_names lists the changed settings")
	expect_equal(domain.get_applied_value(&"volume"), 0.8, "the applied value stays until apply")
	domain.revert()
	expect(not domain.is_dirty(), "revert clears the dirty state")
	expect_equal(domain.get_value(&"volume"), 0.8, "revert gives back the last applied volume")
	expect_equal(domain.get_value(&"quality"), DEFAULT_QUALITY, "revert gives back the last applied quality")
	domain.free()


func _test_float_precision() -> void:
	expect_equal(SettingsServer.snap_float(0.1 + 0.2), 0.3, "snap_float(0.1 + 0.2) is 0.3")
	expect_equal(SettingsServer.snap_float(1.23456), 1.235, "three decimals by default")
	expect_equal(SettingsServer.snap_float(-6.0206, 0.1), -6.0, "snapped to the step")
	expect_equal(SettingsServer.snap_float(0.12, 0.1, 0.05), 0.15, "snapped relative to the offset")
	expect_equal(SettingsServer.snap_float(0.0123, 0.0001), 0.012, "a step below 0.001 counts as 0.001")
	expect_equal(str(SettingsServer.snap_float(-0.0000014)), "0.0", "-0.0 becomes 0.0")
	var domain := _new_domain("precision.json")
	domain.set_value(&"volume", 0.123456)
	expect_equal(domain.get_value(&"volume"), 0.12, "set_value snaps to the step of the range hint")
	domain.set_value(&"volume", 0.1 + 0.2)
	domain.apply()
	domain.save()
	var text: String = FileAccess.get_file_as_string(domain.file_path)
	expect(text.contains("0.3"), "the file holds 0.3")
	expect(not text.contains("0000000"), "the file has no float noise")
	expect(not text.contains("9999999"), "the file has no 9999999 noise")
	domain.free()


func _test_save_and_load() -> void:
	for extension: String in ["ini", "json"]:
		var writer := _new_domain("single." + extension)
		writer.set_value(&"volume", 0.3)
		writer.set_value(&"quality", 0)
		writer.apply()
		_events.clear()
		writer.saved.connect(_record("saved"))
		expect_equal(writer.save(), OK, "save as %s" % extension)
		expect_equal(_events, [["saved"]], "save emits saved (%s)" % extension)
		expect(not writer.has_unsaved_changes(), "save clears has_unsaved_changes (%s)" % extension)
		var text: String = FileAccess.get_file_as_string(writer.file_path)
		expect(text.contains("settings_version"), "%s file has the settings version" % extension)
		var reader := _new_domain("single." + extension)
		reader.loaded.connect(_record("loaded"))
		expect_equal(reader.load(), OK, "load as %s" % extension)
		expect_equal(_events.back(), ["loaded"], "load emits loaded (%s)" % extension)
		expect_equal(reader.get_value(&"volume"), 0.3, "%s: volume loaded" % extension)
		expect_equal(reader.get_value(&"quality"), 0, "%s: quality loaded" % extension)
		writer.free()
		reader.free()
	var changed := _new_domain("only_changed.ini")
	changed.set_value(&"quality", 2)
	changed.apply()
	changed.save()
	var changed_text: String = FileAccess.get_file_as_string(changed.file_path)
	expect(changed_text.contains("quality"), "a changed value is written")
	expect(not changed_text.contains("volume"), "a value equal to the default is not written")
	changed.free()
	var extensions: Array[String] = ["json", "ini", "csv", "xml", "toml", "yaml"]
	for format: int in [ResourceTextSerializer.FORMAT_XML, ResourceTextSerializer.FORMAT_TOML, ResourceTextSerializer.FORMAT_YAML]:
		var extension: String = extensions[format]
		if not ResourceTextSerializer.is_format_available(format):
			print("  SKIP  %s (module not in this build)" % extension)
			continue
		var writer := _new_domain("format." + extension)
		writer.set_value(&"quality", 2)
		writer.apply()
		expect_equal(writer.save(), OK, "save as %s" % extension)
		var reader := _new_domain("format." + extension)
		expect_equal(reader.load(), OK, "load as %s" % extension)
		expect_equal(reader.get_value(&"quality"), 2, "%s: quality loaded" % extension)
		writer.free()
		reader.free()


func _test_load_failures() -> void:
	var broken_path: String = DIR.path_join("broken.json")
	var broken: String = "{ \"volume\": 0.25, "
	var file := FileAccess.open(broken_path, FileAccess.WRITE)
	file.store_string(broken)
	file.close()
	var domain := _new_domain("broken.json")
	_events.clear()
	domain.load_failed.connect(_record("load_failed"))
	var error: int = domain.load()
	expect(error != OK, "a broken file is reported (a warning is printed)")
	expect_equal(_events[0], ["load_failed", error, broken_path], "load_failed carries the error and the path")
	expect_equal(domain.get_value(&"volume"), DEFAULT_VOLUME, "a broken file falls back to the defaults")
	expect_equal(FileAccess.get_file_as_string(broken_path), broken, "load does not overwrite a broken file")
	var missing := _new_domain("missing.ini")
	missing.load_failed.connect(_record("load_failed"))
	_events.clear()
	expect_equal(missing.load(), ERR_FILE_NOT_FOUND, "a missing file keeps the defaults")
	expect(_events.is_empty(), "a missing file emits no load_failed")
	var blocker: String = DIR.path_join("blocker.txt")
	file = FileAccess.open(blocker, FileAccess.WRITE)
	file.store_string("not a folder")
	file.close()
	var bad_path: String = blocker.path_join("sub").path_join("signals.ini")
	domain.file_path = bad_path
	domain.save_failed.connect(_record("save_failed"))
	_events.clear()
	var save_error: int = domain.save()
	expect(save_error != OK, "save into a folder that can't be created fails (an error is printed)")
	expect_equal(_events, [["save_failed", save_error, bad_path]], "save_failed carries the error and the path")
	domain.free()
	missing.free()


func _test_migration() -> void:
	var path: String = DIR.path_join("migrate.json")
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_string(JSON.stringify({"legacy_volume": 0.25, "metadata/settings_version": 1}))
	file.close()
	var domain := _new_domain("migrate.json")
	expect_equal(domain.load(), OK, "load of an old file")
	expect_equal(domain.get_value(&"volume"), 0.25, "_migrate renamed legacy_volume to volume")
	domain.free()


func _test_confirmation() -> void:
	var domain := _new_domain("confirm.ini")
	_events.clear()
	domain.confirmation_requested.connect(_record("confirmation_requested"))
	domain.confirmation_finished.connect(_record("confirmation_finished"))
	domain.set_value(&"quality", 2)
	expect_equal(domain.apply_with_confirmation(0.2), OK, "apply_with_confirmation")
	expect(domain.is_confirmation_pending(), "a confirmation is pending")
	expect(domain.get_confirmation_time_left() > 0.0, "get_confirmation_time_left counts down")
	expect_equal(_events, [["confirmation_requested", 0.2]], "confirmation_requested carries the time")
	expect_equal(domain.get_applied_value(&"quality"), 2, "the new value is applied while waiting")
	await get_tree().create_timer(0.6).timeout
	expect(not domain.is_confirmation_pending(), "the confirmation timed out")
	expect_equal(domain.get_value(&"quality"), DEFAULT_QUALITY, "without confirm_changes the old value comes back")
	expect_equal(_events.back(), ["confirmation_finished", false], "a timeout emits confirmation_finished(false)")
	expect_equal(domain.get_confirmation_time_left(), 0.0, "no time left without a confirmation")
	domain.set_value(&"quality", 0)
	domain.apply_with_confirmation(5.0)
	domain.confirm_changes()
	expect_equal(domain.get_applied_value(&"quality"), 0, "confirm_changes keeps the new value")
	expect_equal(_events.back(), ["confirmation_finished", true], "confirm_changes emits confirmation_finished(true)")
	domain.set_value(&"quality", 2)
	domain.apply_with_confirmation(5.0)
	domain.cancel_confirmation()
	expect(not domain.is_confirmation_pending(), "cancel_confirmation ends the confirmation")
	expect_equal(domain.get_applied_value(&"quality"), 0, "cancel_confirmation gives back the confirmed value")
	_events.clear()
	domain.read_from_engine_finished.connect(_record("read_from_engine_finished"))
	domain.read_from_engine()
	expect_equal(_events.back(), ["read_from_engine_finished"], "read_from_engine emits read_from_engine_finished")
	expect_equal(domain.get_value(&"label"), "engine", "read_from_engine calls _read_from_engine")
	domain.free()


func _test_apply_during_confirmation() -> void:
	var domain := _new_domain("confirm_flag.ini")
	domain.set_setting_flags(&"quality", SettingsServer.SETTING_FLAG_CONFIRM)
	_events.clear()
	domain.confirmation_finished.connect(_record("confirmation_finished"))
	domain.set_value(&"quality", 2)
	domain.apply()
	expect(domain.is_confirmation_pending(), "a value with SETTING_FLAG_CONFIRM starts a confirmation")
	domain.set_value(&"volume", 0.3)
	expect_equal(domain.apply(), OK, "apply during a pending confirmation")
	expect(not domain.is_confirmation_pending(), "apply kept the pending confirmation first")
	expect_equal(_events, [["confirmation_finished", true]], "the kept confirmation emits confirmation_finished(true)")
	expect_equal(domain.get_applied_value(&"quality"), 2, "the kept value stays applied")
	domain.set_value(&"quality", 0)
	domain.apply_with_confirmation(5.0)
	domain.set_value(&"quality", 1)
	expect_equal(domain.apply_with_confirmation(5.0), OK, "apply_with_confirmation during a pending one")
	domain.cancel_confirmation()
	expect_equal(domain.get_applied_value(&"quality"), 0, "a cancel only reverts the values of the new confirmation")
	expect_equal(domain.get_applied_value(&"volume"), 0.3, "values applied before stay")
	domain.free()


func _test_restart_required() -> void:
	var domain := _new_domain("restart.ini")
	domain.set_setting_flags(&"quality", SettingsServer.SETTING_FLAG_RESTART_REQUIRED)
	expect_equal(domain.get_setting_flags(&"quality"), SettingsServer.SETTING_FLAG_RESTART_REQUIRED, "get_setting_flags returns the flags")
	expect_equal(domain.get_setting_flags(&"volume"), SettingsServer.SETTING_FLAG_NONE, "a setting without flags has SETTING_FLAG_NONE")
	_events.clear()
	domain.restart_required.connect(_record("restart_required"))
	domain.set_value(&"quality", 2)
	domain.apply()
	expect_equal(domain.get_restart_required_names(), PackedStringArray(["quality"]), "a changed restart value is listed")
	domain.set_value(&"quality", DEFAULT_QUALITY)
	domain.apply()
	expect(domain.get_restart_required_names().is_empty(), "the value set back to the start value is no longer listed")
	expect_equal(_events, [["restart_required", PackedStringArray(["quality"])], ["restart_required", PackedStringArray()]], "restart_required carries the whole list")
	domain.free()


func _set_bound_value(value: int) -> void:
	_bound_value = value


func _get_bound_value() -> int:
	return _bound_value


func _test_bindings() -> void:
	var node := Node.new()
	var property_domain := _new_domain("bind_property.ini")
	property_domain.bind_setting_property(&"quality", node, &"process_priority")
	expect(property_domain.is_setting_bound(&"quality"), "is_setting_bound after bind_setting_property")
	expect(not property_domain.is_setting_bound(&"volume"), "an unbound setting is not bound")
	property_domain.set_value(&"quality", 2)
	property_domain.apply()
	expect_equal(node.process_priority, 2, "bind_setting_property pushes the value to the property")
	node.process_priority = 1
	property_domain.read_from_engine()
	expect_equal(property_domain.get_value(&"quality"), 1, "read_from_engine reads the bound property")
	property_domain.unbind_setting(&"quality")
	expect(not property_domain.is_setting_bound(&"quality"), "unbind_setting removes the binding")
	property_domain.free()
	node.free()

	var singleton_domain := _new_domain("bind_singleton.ini")
	singleton_domain.bind_setting_singleton_property(&"volume", &"Engine", &"time_scale")
	singleton_domain.set_value(&"volume", 0.5)
	singleton_domain.apply()
	expect_equal(Engine.time_scale, 0.5, "bind_setting_singleton_property pushes the value to the singleton")
	Engine.time_scale = 1.0
	singleton_domain.free()

	var callback_domain := _new_domain("bind_callbacks.ini")
	callback_domain.bind_setting_callbacks(&"quality", _set_bound_value, _get_bound_value, SettingsServer.SETTING_FLAG_RESTART_REQUIRED)
	expect_equal(callback_domain.get_setting_flags(&"quality"), SettingsServer.SETTING_FLAG_RESTART_REQUIRED, "bind_setting_callbacks sets the flags")
	callback_domain.set_value(&"quality", 2)
	callback_domain.apply()
	expect_equal(_bound_value, 2, "bind_setting_callbacks calls the setter")
	_bound_value = 0
	callback_domain.read_from_engine()
	expect_equal(callback_domain.get_value(&"quality"), 0, "read_from_engine calls the getter")
	callback_domain.unbind_setting(&"quality")
	expect_equal(callback_domain.get_setting_flags(&"quality"), SettingsServer.SETTING_FLAG_NONE, "unbind_setting removes the flags")
	callback_domain.free()


func _test_session_override() -> void:
	var domain := _new_domain("override.ini")
	_bound_value = DEFAULT_QUALITY
	domain.bind_setting_callbacks(&"quality", _set_bound_value, _get_bound_value)
	domain.set_session_override(&"quality", 2)
	expect(domain.has_session_override(&"quality"), "has_session_override after set_session_override")
	expect_equal(domain.get_value(&"quality"), DEFAULT_QUALITY, "an override does not change get_value")
	expect_equal(_bound_value, 2, "an override is pushed to the engine")
	domain.save()
	expect(not FileAccess.get_file_as_string(domain.file_path).contains("quality"), "an override is never saved")
	domain.clear_session_override(&"quality")
	expect(not domain.has_session_override(&"quality"), "clear_session_override removes the override")
	expect_equal(_bound_value, DEFAULT_QUALITY, "clear_session_override pushes the applied value again")
	domain.set_session_override(&"quality", 2)
	domain.set_value(&"quality", 0)
	expect(not domain.has_session_override(&"quality"), "set_value removes the override")
	domain.free()


func _test_manager() -> void:
	var domains: PackedStringArray = SettingsServerManager.get_domains()
	expect(domains.has("test_gd"), "get_domains lists test_gd")
	var servers: Array[SettingsServer] = SettingsServerManager.get_servers()
	expect_equal(servers.size(), domains.size(), "get_servers has one server per domain")
	expect(not SettingsServerManager.has_server(&"missing"), "has_server is false for a missing domain")
	expect(SettingsServerManager.get_server(&"missing") == null, "get_server returns null for a missing domain")
	var list := _new_list()
	list.file_path = DIR.path_join("manager_list.ini")
	_events.clear()
	var on_registered: Callable = _record("server_registered")
	var on_unregistered: Callable = _record("server_unregistered")
	SettingsServerManager.server_registered.connect(on_registered)
	SettingsServerManager.server_unregistered.connect(on_unregistered)
	expect_equal(SettingsServerManager.register_server(list), OK, "register_server")
	expect(SettingsServerManager.get_server(&"test_list") == list, "get_server returns the registered server")
	expect(list.is_started(), "register_server starts the server at once")
	expect_equal(SettingsServerManager.register_server(null), ERR_INVALID_PARAMETER, "register_server rejects null")
	var duplicate := _new_list()
	expect_equal(SettingsServerManager.register_server(duplicate), ERR_ALREADY_EXISTS, "register_server rejects a taken domain")
	duplicate.free()
	list.add_entry(_new_entry("a", 1))
	expect(SettingsServerManager.has_dirty_servers(), "has_dirty_servers is true with a pending entry")
	SettingsServerManager.revert_all()
	expect(not SettingsServerManager.has_dirty_servers(), "revert_all drops pending changes")
	list.add_entry(_new_entry("a", 1))
	expect_equal(SettingsServerManager.apply_all(), OK, "apply_all")
	expect_equal(SettingsServerManager.save_all(), OK, "save_all")
	expect(FileAccess.file_exists(list.file_path), "save_all wrote the file of the list domain")
	list.clear_entries()
	list.apply()
	expect_equal(SettingsServerManager.load_all(), OK, "load_all")
	SettingsServerManager.apply_all()
	expect_equal(list.get_entry_count(), 1, "load_all read the saved entry")
	SettingsServerManager.reset_all_to_defaults()
	expect_equal(list.get_entry_count(), 0, "reset_all_to_defaults goes back to the defaults")
	SettingsServerManager.unregister_server(&"test_list")
	expect(not SettingsServerManager.has_server(&"test_list"), "unregister_server removes the server")
	expect_equal(_events, [["server_registered", &"test_list"], ["server_unregistered", &"test_list"]], "server_registered and server_unregistered carry the domain")
	SettingsServerManager.server_registered.disconnect(on_registered)
	SettingsServerManager.server_unregistered.disconnect(on_unregistered)
	list.free()


func _test_list_domain() -> void:
	var list := _new_list()
	expect_equal(list.get_entry_key(), &"entry_name", "get_entry_key returns the key property")
	expect_equal(list.get_list_property(), &"entries", "get_list_property is the first Array property")
	list.add_entry(_new_entry("a", 1))
	list.add_entry(_new_entry("b", 2))
	list.add_entry(_new_entry("c", 3))
	list.add_entry(_new_entry("first", 4), 0)
	expect_equal(list.get_entry_count(), 4, "add_entry adds at the end and at an index")
	expect_equal(list.get_entry_value(0, &"entry_name"), "first", "add_entry at index 0 inserts first")
	list.remove_entry(0)
	list.move_entry(2, 0)
	expect_equal(list.get_entry_value(0, &"entry_name"), "c", "move_entry moves an entry")
	list.remove_entry(1)
	expect_equal(list.get_entry_count(), 2, "remove_entry removes an entry")
	expect_equal(list.find_entry("c"), 0, "find_entry finds by key")
	expect_equal(list.find_entry("missing"), -1, "find_entry returns -1 for a missing key")
	list.set_entry_value(1, &"level", 5)
	expect_equal(list.get_entry_value(1, &"level"), 5, "set_entry_value changes a value")
	var entry: Resource = list.get_entry(1)
	expect_equal(entry.get("level"), 5, "get_entry returns the entry")
	expect_equal(list.get_entries().size(), 2, "get_entries returns a copy of the list")
	list.clear_entries()
	expect_equal(list.get_entry_count(), 0, "clear_entries removes all entries")
	list.free()


func _test_list_files() -> void:
	var list := _new_list()
	list.add_entry(_new_entry("c", 3))
	list.add_entry(_new_entry("b", 5))
	list.apply()
	for extension: String in ["csv", "json", "ini"]:
		var path: String = DIR.path_join("list." + extension)
		list.file_path = path
		expect_equal(list.save(), OK, "list: save as %s" % extension)
		var reader: ListSettingsServer = load(LIST_SCRIPT).new()
		reader.file_path = path
		expect_equal(reader.load(), OK, "list: load as %s" % extension)
		expect_equal(reader.get_entry_count(), 2, "list %s: entry count" % extension)
		expect_equal(reader.get_entry_value(0, &"entry_name"), "c", "list %s: first entry" % extension)
		expect_equal(reader.get_entry_value(1, &"level"), 5, "list %s: level of the second entry" % extension)
		reader.free()
	list.free()


func _test_list_defaults_and_validation() -> void:
	var list: ListSettingsServer = load(LIST_SCRIPT).new()
	list.file_path = DIR.path_join("list_defaults.ini")
	var defaults: SettingsServersTestList = list.get_default_settings()
	defaults.entries.append(_new_entry("a", 1))
	list.reset_to_defaults()
	expect_equal(list.get_entry_count(), 1, "the list has the default entry")
	expect(list.get_entry(0) != defaults.entries[0], "the entry is a copy of the default entry")
	expect_equal(list.save(), OK, "save of an unchanged list")
	expect(not FileAccess.get_file_as_string(list.file_path).contains("entries"), "entries equal to the defaults are not written")
	list.set_entry_value(0, &"level", 4)
	list.save()
	expect(FileAccess.get_file_as_string(list.file_path).contains("entries"), "a changed entry writes the list")
	list.set_entry_value(0, &"level", 50)
	expect_equal(list.get_entry_value(0, &"level"), MAX_LEVEL, "set_entry_value clamps to the range hint")
	list.set_entry_value(0, &"level", -3)
	expect_equal(list.get_entry_value(0, &"level"), 0, "set_entry_value clamps the lower end")
	list.set_entry_value(0, &"level", 7)
	list.apply()
	var path: String = DIR.path_join("list_clamp.json")
	list.file_path = path
	list.save()
	var text: String = FileAccess.get_file_as_string(path)
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_string(text.replace("7", "99"))
	file.close()
	var reader: ListSettingsServer = load(LIST_SCRIPT).new()
	reader.file_path = path
	expect_equal(reader.load(), OK, "load of a hand-edited list")
	expect_equal(reader.get_entry_value(0, &"level"), MAX_LEVEL, "hand-edited entry values are clamped on load")
	reader.free()
	list.free()


func _test_project_paths() -> void:
	for domain_name in SettingsServerManager.get_domains():
		for key: String in ["file_name", "override_format", "format"]:
			var setting_name: String = "settings/%s/%s" % [domain_name, key]
			expect(ProjectSettings.has_setting(setting_name), "project setting %s exists" % setting_name)
	for general: String in ["folder", "default_format", "only_changed_values", "autosave", "confirmation_timeout"]:
		expect(ProjectSettings.has_setting("settings/general/" + general), "settings/general/%s exists" % general)
	var file_name_setting: String = "settings/test_gd/file_name"
	var override_setting: String = "settings/test_gd/override_format"
	var format_setting: String = "settings/test_gd/format"
	expect_equal(ProjectSettings.get_setting(file_name_setting), "test_gd", "the file name defaults to the domain name")
	expect_equal(ProjectSettings.get_setting(override_setting), false, "the format is not overridden by default")
	var domain: SettingsServer = load(DOMAIN_SCRIPT).new()
	expect_equal(domain.file_path, PLAYER_FILE, "path: <folder>/<file_name>.<extension of default_format>")
	ProjectSettings.set_setting("settings/general/folder", DIR)
	ProjectSettings.set_setting(file_name_setting, "profiles/test_gd")
	expect_equal(domain.file_path, DIR.path_join("profiles/test_gd.ini"), "a relative subfolder in the file name")
	ProjectSettings.set_setting(format_setting, ResourceTextSerializer.FORMAT_JSON)
	expect_equal(domain.file_path, DIR.path_join("profiles/test_gd.ini"), "format without override_format is ignored")
	ProjectSettings.set_setting(override_setting, true)
	expect_equal(domain.file_path, DIR.path_join("profiles/test_gd.json"), "override_format uses the format of the domain")
	ProjectSettings.set_setting("settings/general/default_format", ResourceTextSerializer.FORMAT_XML)
	expect_equal(domain.file_path, DIR.path_join("profiles/test_gd.json"), "override_format wins over default_format")
	ProjectSettings.set_setting(override_setting, false)
	expect_equal(domain.file_path, DIR.path_join("profiles/test_gd.xml"), "default_format picks the extension")
	ProjectSettings.set_setting("settings/general/default_format", ResourceTextSerializer.FORMAT_INI)
	ProjectSettings.set_setting(override_setting, true)
	domain.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	domain.set_value(&"quality", 2)
	domain.apply()
	expect_equal(domain.save(), OK, "save creates the subfolder")
	var written: String = FileAccess.get_file_as_string(DIR.path_join("profiles/test_gd.json"))
	expect(written.strip_edges().begins_with("{"), "the composed .json path is written as JSON")
	for invalid: String in ["../outside", "/absolute", "res://inside", "a//b"]:
		ProjectSettings.set_setting(file_name_setting, invalid)
		expect_equal(domain.file_path, DIR.path_join("test_gd.json"), "invalid file name %s falls back to the domain name (an error is printed)" % invalid)
	ProjectSettings.set_setting(file_name_setting, "test_gd")
	var runtime_path: String = DIR.path_join("runtime.ini")
	domain.file_path = runtime_path
	expect_equal(domain.file_path, runtime_path, "file_path set at runtime wins over the project settings")
	domain.file_path = ""
	expect_equal(domain.file_path, DIR.path_join("test_gd.json"), "an empty file_path gives the composed path back")
	var unknown: String = DIR.path_join("runtime.cfg")
	domain.file_path = unknown
	expect_equal(domain.save(), OK, "an unknown extension is saved in the format of the domain (an error is printed)")
	expect(FileAccess.get_file_as_string(unknown).strip_edges().begins_with("{"), "an unknown extension is written as JSON")
	ProjectSettings.set_setting("settings/general/folder", "user://settings")
	ProjectSettings.set_setting(override_setting, false)
	ProjectSettings.set_setting(format_setting, ResourceTextSerializer.FORMAT_INI)
	domain.free()
	DirAccess.remove_absolute(DIR.path_join("profiles/test_gd.json"))
	DirAccess.remove_absolute(DIR.path_join("profiles"))


func _test_viewport_target() -> void:
	expect_equal(RenderQualitySettingsServer.viewport_target, SettingsServer.VIEWPORT_TARGET_GROUP, "viewport_target defaults to VIEWPORT_TARGET_GROUP")
	RenderQualitySettingsServer.viewport_target = SettingsServer.VIEWPORT_TARGET_ALL_VIEWPORTS
	expect_equal(RenderQualitySettingsServer.viewport_target, SettingsServer.VIEWPORT_TARGET_ALL_VIEWPORTS, "viewport_target can be changed")
	RenderQualitySettingsServer.viewport_target = SettingsServer.VIEWPORT_TARGET_GROUP


func _test_restart() -> void:
	if not OS.has_feature("pc"):
		print("  SKIP  restart test (needs a desktop platform to start a second process)")
		return
	var server: SettingsServer = SettingsServerManager.get_server(&"test_gd")
	var volume: float = randi_range(10, 90) / 100.0
	server.set_value(&"volume", volume)
	server.set_value(&"quality", 2)
	expect(not server.is_dirty(), "immediate mode applies set_value at once")
	expect_equal(server.save(), OK, "save writes the player file")
	var probe: Dictionary = _run_probe()
	var gd_probe: Dictionary = probe.get("test_gd", {})
	var applied: Dictionary = gd_probe.get("applied", {})
	expect(gd_probe.get("started", false), "restart: the domain was started")
	expect_equal(applied.get("volume", -1.0), volume, "restart: the saved volume was applied")
	expect_equal(int(applied.get("quality", -1)), 2, "restart: the saved quality was applied")
	expect_equal(int(applied.get("frame", -1)), 0, "restart: applied before the first frame")
	expect_equal(int(probe.get("ready_frame", -1)), 0, "restart: the main scene was ready before the first frame")
	server.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(PLAYER_FILE)


func _run_probe() -> Dictionary:
	var output: Array = []
	var args: PackedStringArray = ["--headless", "--path", ProjectSettings.globalize_path("res://"), PROBE_SCENE]
	var code: int = OS.execute(OS.get_executable_path(), args, output, true)
	expect_equal(code, 0, "restart: the probe process ran")
	for chunk in output:
		for line: String in str(chunk).split("\n"):
			if line.begins_with("PROBE "):
				var parsed: Variant = JSON.parse_string(line.trim_prefix("PROBE "))
				return parsed
	expect(false, "restart: the probe printed its result")
	return {}


func _remove_dir(path: String) -> void:
	for file_name: String in DirAccess.get_files_at(path):
		DirAccess.remove_absolute(path.path_join(file_name))
	DirAccess.remove_absolute(path)
