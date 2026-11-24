extends FeatureCheck
## GDScript checks of 30-feature-xr-settings. Check the same things as scripts/cs/Issue30FeatureXrSettingsCheck.cs.
##
## The XR values go to the OpenXR interface of XRServer. The project runs without a headset, so the real interface
## is not initialized: values are stored and deferred. A fake interface (fake_openxr_interface.gd) with the same API
## shows the deferred application, the session handling and the unsupported values.

const XR_FILE := "user://settings/xr.ini"
const NAMES := ["foveation_level", "foveation_dynamic", "foveation_with_subsampled_images", "render_target_size_multiplier", "display_refresh_rate", "vrs_min_radius", "vrs_strength", "cpu_level", "gpu_level", "left_hand_motion_range", "right_hand_motion_range", "hand_tracking_enabled"]
const FakeInterface := preload("res://30-feature-xr-settings/scripts/gd/fake_openxr_interface.gd")

var server = null # Untyped: the class only exists with the openxr module.


func _get_check_name() -> String:
	return "30-feature-xr-settings"


func _run_checks() -> void:
	expect(Engine.has_singleton("XRSettingsServer"), "XRSettingsServer is a registered singleton (the editor build has the openxr module)")
	if not Engine.has_singleton("XRSettingsServer"):
		return
	server = Engine.get_singleton("XRSettingsServer")
	expect(server is SettingsServer, "XRSettingsServer is a SettingsServer")
	expect_equal(SettingsServerManager.get_server(&"xr"), server, "the domain is registered as \"xr\"")
	expect(server.is_started(), "the domain was started before the main scene")
	expect_equal(server.get_apply_phase(), SettingsServer.APPLY_PHASE_SCENE, "applied after the SceneTree exists")
	expect_equal(server.get_file_path(), XR_FILE, "file path")
	expect(server.get_settings() is XRSettings, "the settings resource is an XRSettings")
	expect_equal(server.get_interface_name(), &"OpenXR", "get_interface_name()")
	var names: PackedStringArray = server.get_setting_names()
	expect_equal(names.size(), NAMES.size(), "12 values")
	for setting in NAMES:
		expect(names.has(setting), "setting \"%s\" exists" % setting)
		expect(server.has_signal(StringName(setting + "_changed")), "typed signal %s_changed exists" % setting)
	expect((server.get_setting_flags(&"hand_tracking_enabled") & SettingsServer.SETTING_FLAG_NO_ENGINE_TARGET) != 0, "hand_tracking_enabled is game-side")
	expect((server.get_setting_flags(&"foveation_level") & SettingsServer.SETTING_FLAG_NO_ENGINE_TARGET) == 0, "foveation_level goes to the engine")

	server.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	server.reset_to_defaults()
	_check_defaults()
	_check_validation()
	_check_without_headset()
	_check_deferred_until_session()
	_check_refresh_rate_matching()
	_check_unsupported()
	_check_typed_signals()
	_check_storage()

	# Leave no player file behind.
	server.clear_interface_override()
	server.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(XR_FILE)


func _check_defaults() -> void:
	expect_equal(server.get_default_value(&"foveation_level"), ProjectSettings.get_setting("xr/openxr/foveation_level", 0), "default foveation level is the project setting")
	expect_equal(server.get_default_value(&"foveation_dynamic"), ProjectSettings.get_setting("xr/openxr/foveation_dynamic", false), "default dynamic foveation is the project setting")
	expect(is_equal_approx(server.get_render_target_size_multiplier(), 1.0), "default render scale 1")
	expect(is_equal_approx(server.get_display_refresh_rate(), 0.0), "default refresh rate 0 (runtime default)")
	expect(is_equal_approx(server.get_vrs_min_radius(), 20.0), "default VRS radius 20")
	expect(is_equal_approx(server.get_vrs_strength(), 1.0), "default VRS strength 1")
	expect_equal(server.get_cpu_level(), XRSettings.PERFORMANCE_LEVEL_RUNTIME_DEFAULT, "default CPU level: runtime default")
	expect_equal(server.get_gpu_level(), XRSettings.PERFORMANCE_LEVEL_RUNTIME_DEFAULT, "default GPU level: runtime default")
	expect_equal(server.get_left_hand_motion_range(), XRSettings.HAND_MOTION_RANGE_UNOBSTRUCTED, "default left motion range")
	expect(server.is_hand_tracking_enabled(), "hand tracking on by default")


func _check_validation() -> void:
	server.set_render_target_size_multiplier(10.0)
	expect(is_equal_approx(server.get_render_target_size_multiplier(), 3.0), "render scale clamped to 3")
	server.set_render_target_size_multiplier(0.0)
	expect(is_equal_approx(server.get_render_target_size_multiplier(), 0.1), "render scale clamped to 0.1")
	server.set_display_refresh_rate(-60.0)
	expect(is_equal_approx(server.get_display_refresh_rate(), 0.0), "negative refresh rate clamped to 0")
	server.set_vrs_strength(50.0)
	expect(is_equal_approx(server.get_vrs_strength(), 10.0), "VRS strength clamped to 10")
	server.reset_to_defaults()


func _check_without_headset() -> void:
	# The real OpenXR interface exists but is not initialized (no xr/openxr/enabled, no headset).
	expect(not server.is_interface_ready(), "no initialized OpenXR interface in the test project")
	expect(not server.is_session_running(), "no XR session")
	server.set_foveation_level(XRSettings.FOVEATION_LEVEL_HIGH)
	server.set_gpu_level(XRSettings.PERFORMANCE_LEVEL_BOOST)
	expect_equal(server.get_foveation_level(), XRSettings.FOVEATION_LEVEL_HIGH, "the value is stored without a headset")
	expect_equal(server.get_applied_value(&"foveation_level"), XRSettings.FOVEATION_LEVEL_HIGH, "the value counts as applied")
	var deferred: PackedStringArray = server.get_deferred_setting_names()
	expect(deferred.has("foveation_level"), "foveation_level waits for the headset")
	expect(deferred.has("gpu_level"), "gpu_level waits for the headset")
	expect(server.get_unsupported_setting_names().is_empty(), "nothing is unsupported before the runtime is known")
	expect(server.get_available_display_refresh_rates().is_empty(), "no refresh rates without a runtime")
	server.reset_to_defaults()


func _check_deferred_until_session() -> void:
	var fake = FakeInterface.new()
	fake.refresh_rates = [72.0, 90.0, 120.0]
	server.set_interface_override(fake)
	expect_equal(server.get_interface(), fake, "set_interface_override()")

	server.set_render_target_size_multiplier(1.5)
	server.set_foveation_level(XRSettings.FOVEATION_LEVEL_LOW)
	server.set_foveation_dynamic(true)
	server.set_display_refresh_rate(90.0)
	server.set_gpu_level(XRSettings.PERFORMANCE_LEVEL_BOOST)
	server.set_left_hand_motion_range(XRSettings.HAND_MOTION_RANGE_CONFORM_TO_CONTROLLER)
	expect(is_equal_approx(fake.render_target_size_multiplier, 1.5), "the render scale goes to the interface before it is initialized")
	expect_equal(fake.foveation_level, 0, "foveation waits for the session")
	expect_equal(fake.gpu_level, -1, "the GPU level waits for the session")
	var deferred: PackedStringArray = server.get_deferred_setting_names()
	expect(not deferred.has("render_target_size_multiplier"), "the render scale is not deferred")
	expect(deferred.has("foveation_level") and deferred.has("display_refresh_rate"), "foveation and refresh rate are deferred")

	fake.begin_session()
	expect(server.is_interface_ready(), "is_interface_ready() after initialize")
	expect(server.is_session_running(), "is_session_running() after session_begun")
	expect(server.get_deferred_setting_names().is_empty(), "nothing deferred after the session began")
	expect_equal(fake.foveation_level, 1, "foveation level pushed when the session begins")
	expect(fake.foveation_dynamic, "dynamic foveation pushed")
	expect(is_equal_approx(fake.display_refresh_rate, 90.0), "refresh rate pushed")
	expect_equal(fake.gpu_level, 3, "GPU level pushed as OpenXRInterface.PERF_SETTINGS_LEVEL_BOOST")
	expect_equal(fake.cpu_level, -1, "the runtime default CPU level is never pushed")
	expect_equal(fake.motion_ranges[0], 1, "left motion range pushed")

	server.set_foveation_level(XRSettings.FOVEATION_LEVEL_HIGH)
	expect_equal(fake.foveation_level, 3, "changes during the session reach the interface at once")

	fake.stop_session()
	expect(not server.is_session_running(), "session_stopping ends the session")
	fake.foveation_level = 0
	fake.begin_session()
	expect_equal(fake.foveation_level, 3, "a new session gets the values again")

	server.clear_interface_override()
	fake.free()
	server.reset_to_defaults()


func _check_refresh_rate_matching() -> void:
	var fake = FakeInterface.new()
	fake.initialized = true
	fake.refresh_rates = [120.0, 72.0, 90.0]
	server.set_interface_override(fake)
	var rates: PackedFloat64Array = server.get_available_display_refresh_rates()
	expect_equal(rates, PackedFloat64Array([72.0, 90.0, 120.0]), "the rates of the runtime, sorted")
	server.set_display_refresh_rate(100.0)
	expect(is_equal_approx(fake.display_refresh_rate, 90.0), "100 Hz goes to the nearest rate, 90 Hz")
	expect(is_equal_approx(server.get_display_refresh_rate(), 100.0), "the player's choice stays stored")
	server.set_display_refresh_rate(0.0)
	expect(is_equal_approx(fake.display_refresh_rate, 90.0), "0 (runtime default) is not pushed")
	server.clear_interface_override()
	fake.free()
	server.reset_to_defaults()


func _check_unsupported() -> void:
	var fake = FakeInterface.new()
	fake.initialized = true
	fake.foveation_supported = false
	fake.hand_tracking_supported = false
	server.set_interface_override(fake)
	var unsupported: PackedStringArray = server.get_unsupported_setting_names()
	for setting in ["foveation_level", "foveation_dynamic", "foveation_with_subsampled_images", "display_refresh_rate", "left_hand_motion_range", "right_hand_motion_range", "hand_tracking_enabled"]:
		expect(unsupported.has(setting), "\"%s\" is unsupported by this runtime" % setting)
	expect(not unsupported.has("render_target_size_multiplier"), "the render scale works with every runtime")
	expect(server.is_value_supported(&"vrs_strength"), "is_value_supported(\"vrs_strength\")")
	expect(not server.is_value_supported(&"foveation_level"), "is_value_supported(\"foveation_level\") is false")

	server.set_foveation_level(XRSettings.FOVEATION_LEVEL_HIGH)
	expect_equal(server.get_foveation_level(), XRSettings.FOVEATION_LEVEL_HIGH, "an unsupported value is stored")
	expect_equal(fake.foveation_level, 0, "an unsupported value is not pushed")
	expect(not server.get_deferred_setting_names().has("foveation_level"), "an unsupported value is not deferred")
	server.set_vrs_min_radius(40.0)
	expect(is_equal_approx(fake.vrs_min_radius, 40.0), "supported values still reach the interface")
	server.clear_interface_override()
	fake.free()
	server.reset_to_defaults()


func _check_typed_signals() -> void:
	var arg_info: Dictionary = {}
	for info: Dictionary in server.get_signal_list():
		if info.name == "foveation_level_changed":
			arg_info = info.args[0]
	expect_equal(arg_info.get("type"), TYPE_INT, "foveation_level_changed has a typed argument")
	expect_equal(arg_info.get("class_name"), &"XRSettings.FoveationLevel", "foveation_level_changed passes an XRSettings.FoveationLevel")

	var levels: Array = []
	var on_level := func(level: int) -> void: levels.append(level)
	var hands: Array = []
	var on_hands := func(enabled: bool) -> void: hands.append(enabled)
	server.connect(&"foveation_level_changed", on_level)
	server.connect(&"hand_tracking_enabled_changed", on_hands)

	server.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	server.set_foveation_level(XRSettings.FOVEATION_LEVEL_MEDIUM)
	expect(levels.is_empty(), "foveation_level_changed does not fire for a pending change in manual mode")
	server.apply()
	expect_equal(levels, [XRSettings.FOVEATION_LEVEL_MEDIUM], "foveation_level_changed fires once on apply(), also without a headset")
	server.apply()
	expect_equal(levels.size(), 1, "no signal when nothing changed")
	server.set_hand_tracking_enabled(false)
	server.apply()
	expect_equal(hands, [false], "hand_tracking_enabled_changed fires for the game-side value")
	server.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE

	server.disconnect(&"foveation_level_changed", on_level)
	server.disconnect(&"hand_tracking_enabled_changed", on_hands)
	server.reset_to_defaults()


func _check_storage() -> void:
	server.set_foveation_level(XRSettings.FOVEATION_LEVEL_LOW)
	server.set_render_target_size_multiplier(1.25)
	server.set_cpu_level(XRSettings.PERFORMANCE_LEVEL_SUSTAINED_HIGH)
	server.set_right_hand_motion_range(XRSettings.HAND_MOTION_RANGE_CONFORM_TO_CONTROLLER)
	server.set_hand_tracking_enabled(false)
	expect_equal(server.save(), OK, "save() without a headset")
	expect(FileAccess.file_exists(XR_FILE), "the file exists")

	server.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	server.reset_to_defaults()
	expect_equal(server.load(), OK, "load()")
	expect_equal(server.get_foveation_level(), XRSettings.FOVEATION_LEVEL_LOW, "foveation level loaded")
	expect(is_equal_approx(server.get_render_target_size_multiplier(), 1.25), "render scale loaded")
	expect_equal(server.get_cpu_level(), XRSettings.PERFORMANCE_LEVEL_SUSTAINED_HIGH, "CPU level loaded")
	expect_equal(server.get_right_hand_motion_range(), XRSettings.HAND_MOTION_RANGE_CONFORM_TO_CONTROLLER, "right motion range loaded")
	expect(not server.is_hand_tracking_enabled(), "hand tracking choice loaded")
	server.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
