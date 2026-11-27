extends FeatureTest
## Full API test of XR Settings (XRSettingsServer, XRSettings). Same tests as scripts/cs/Test.cs.
## The XR values go to the OpenXR interface of XRServer. The project runs without a headset, so the real interface
## is not initialized: values are stored and deferred. A fake interface (fake_openxr_interface.gd) with the same API
## shows the deferred application, the session handling and the unsupported values.

const XR_FILE := "user://settings/xr.ini"
const FakeInterface := preload("res://xr-settings/scripts/gd/fake_openxr_interface.gd")
const PERFORMANCE_BOOST := 3 # OpenXRInterface.PERF_SETTINGS_LEVEL_BOOST

var _received: Dictionary = {}


func _run_tests() -> void:
	XRSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	XRSettingsServer.reset_to_defaults()
	_test_defaults()
	_test_values()
	_test_validation()
	_test_without_headset()
	_test_deferred_until_session()
	_test_refresh_rate_matching()
	_test_unsupported()
	_test_signals()
	_test_storage()
	_test_resource()
	XRSettingsServer.clear_interface_override()
	XRSettingsServer.reset_to_defaults()
	DirAccess.remove_absolute(XR_FILE)


func _test_defaults() -> void:
	var project_level: int = int(ProjectSettings.get_setting("xr/openxr/foveation_level", 0))
	var project_dynamic: bool = ProjectSettings.get_setting("xr/openxr/foveation_dynamic", false)
	expect_equal(XRSettingsServer.get_foveation_level(), project_level, "the default foveation level is the project setting")
	expect_equal(XRSettingsServer.is_foveation_dynamic(), project_dynamic, "the default dynamic foveation is the project setting")
	expect_equal(XRSettingsServer.get_render_target_size_multiplier(), 1.0, "the default render scale is 1")
	expect_equal(XRSettingsServer.get_display_refresh_rate(), 0.0, "the default refresh rate is 0, the runtime default")
	expect_equal(XRSettingsServer.get_vrs_min_radius(), 20.0, "the default VRS radius is 20")
	expect_equal(XRSettingsServer.get_vrs_strength(), 1.0, "the default VRS strength is 1")
	expect_equal(XRSettingsServer.get_cpu_level(), XRSettings.PERFORMANCE_LEVEL_RUNTIME_DEFAULT, "the default CPU level is the runtime default")
	expect_equal(XRSettingsServer.get_gpu_level(), XRSettings.PERFORMANCE_LEVEL_RUNTIME_DEFAULT, "the default GPU level is the runtime default")
	expect_equal(XRSettingsServer.get_left_hand_motion_range(), XRSettings.HAND_MOTION_RANGE_UNOBSTRUCTED, "the default left motion range")
	expect(XRSettingsServer.is_hand_tracking_enabled(), "hand tracking is on by default")
	expect_equal(XRSettingsServer.get_interface_name(), &"OpenXR", "get_interface_name returns OpenXR")
	var flags: int = XRSettingsServer.get_setting_flags(&"hand_tracking_enabled")
	expect(flags & SettingsServer.SETTING_FLAG_NO_ENGINE_TARGET != 0, "hand_tracking_enabled is game-side")


func _test_values() -> void:
	XRSettingsServer.set_foveation_level(XRSettings.FOVEATION_LEVEL_HIGH)
	expect_equal(XRSettingsServer.get_foveation_level(), XRSettings.FOVEATION_LEVEL_HIGH, "foveation_level round trip")
	XRSettingsServer.set_foveation_dynamic(true)
	expect(XRSettingsServer.is_foveation_dynamic(), "foveation_dynamic round trip")
	XRSettingsServer.set_foveation_with_subsampled_images(false)
	expect(not XRSettingsServer.is_foveation_with_subsampled_images(), "foveation_with_subsampled_images round trip")
	XRSettingsServer.set_render_target_size_multiplier(1.5)
	expect_equal(XRSettingsServer.get_render_target_size_multiplier(), 1.5, "render_target_size_multiplier round trip")
	XRSettingsServer.set_display_refresh_rate(90.0)
	expect_equal(XRSettingsServer.get_display_refresh_rate(), 90.0, "display_refresh_rate round trip")
	XRSettingsServer.set_vrs_min_radius(40.0)
	expect_equal(XRSettingsServer.get_vrs_min_radius(), 40.0, "vrs_min_radius round trip")
	XRSettingsServer.set_vrs_strength(2.0)
	expect_equal(XRSettingsServer.get_vrs_strength(), 2.0, "vrs_strength round trip")
	XRSettingsServer.set_cpu_level(XRSettings.PERFORMANCE_LEVEL_SUSTAINED_HIGH)
	expect_equal(XRSettingsServer.get_cpu_level(), XRSettings.PERFORMANCE_LEVEL_SUSTAINED_HIGH, "cpu_level round trip")
	XRSettingsServer.set_gpu_level(XRSettings.PERFORMANCE_LEVEL_BOOST)
	expect_equal(XRSettingsServer.get_gpu_level(), XRSettings.PERFORMANCE_LEVEL_BOOST, "gpu_level round trip")
	XRSettingsServer.set_left_hand_motion_range(XRSettings.HAND_MOTION_RANGE_CONFORM_TO_CONTROLLER)
	expect_equal(XRSettingsServer.get_left_hand_motion_range(), XRSettings.HAND_MOTION_RANGE_CONFORM_TO_CONTROLLER, "left_hand_motion_range round trip")
	XRSettingsServer.set_right_hand_motion_range(XRSettings.HAND_MOTION_RANGE_CONFORM_TO_CONTROLLER)
	expect_equal(XRSettingsServer.get_right_hand_motion_range(), XRSettings.HAND_MOTION_RANGE_CONFORM_TO_CONTROLLER, "right_hand_motion_range round trip")
	XRSettingsServer.set_hand_tracking_enabled(false)
	expect(not XRSettingsServer.is_hand_tracking_enabled(), "hand_tracking_enabled round trip")
	XRSettingsServer.reset_to_defaults()


func _test_validation() -> void:
	XRSettingsServer.set_render_target_size_multiplier(10.0)
	expect_equal(XRSettingsServer.get_render_target_size_multiplier(), 3.0, "the render scale is clamped to 3")
	XRSettingsServer.set_render_target_size_multiplier(0.0)
	expect(is_equal_approx(XRSettingsServer.get_render_target_size_multiplier(), 0.1), "the render scale is clamped to 0.1")
	XRSettingsServer.set_display_refresh_rate(-60.0)
	expect_equal(XRSettingsServer.get_display_refresh_rate(), 0.0, "a negative refresh rate is clamped to 0")
	XRSettingsServer.set_vrs_strength(50.0)
	expect_equal(XRSettingsServer.get_vrs_strength(), 10.0, "the VRS strength is clamped to 10")
	XRSettingsServer.reset_to_defaults()


func _test_without_headset() -> void:
	expect(not XRSettingsServer.is_interface_ready(), "there is no initialized OpenXR interface in the test project")
	expect(not XRSettingsServer.is_session_running(), "there is no XR session")
	XRSettingsServer.set_foveation_level(XRSettings.FOVEATION_LEVEL_HIGH)
	XRSettingsServer.set_gpu_level(XRSettings.PERFORMANCE_LEVEL_BOOST)
	expect_equal(XRSettingsServer.get_applied_value(&"foveation_level"), XRSettings.FOVEATION_LEVEL_HIGH, "the value counts as applied without a headset")
	var deferred: PackedStringArray = XRSettingsServer.get_deferred_setting_names()
	expect(deferred.has("foveation_level"), "foveation_level waits for the headset")
	expect(deferred.has("gpu_level"), "gpu_level waits for the headset")
	expect(XRSettingsServer.get_unsupported_setting_names().is_empty(), "nothing is unsupported before the runtime is known")
	expect(XRSettingsServer.get_available_display_refresh_rates().is_empty(), "there are no refresh rates without a runtime")
	expect(XRSettingsServer.get_interface() != null, "get_interface returns the OpenXR interface")
	XRSettingsServer.reset_to_defaults()


func _test_deferred_until_session() -> void:
	var fake: Object = FakeInterface.new()
	fake.refresh_rates = [72.0, 90.0, 120.0]
	XRSettingsServer.set_interface_override(fake)
	expect_equal(XRSettingsServer.get_interface(), fake, "set_interface_override replaces the interface")
	XRSettingsServer.set_render_target_size_multiplier(1.5)
	XRSettingsServer.set_foveation_level(XRSettings.FOVEATION_LEVEL_LOW)
	XRSettingsServer.set_foveation_dynamic(true)
	XRSettingsServer.set_display_refresh_rate(90.0)
	XRSettingsServer.set_gpu_level(XRSettings.PERFORMANCE_LEVEL_BOOST)
	XRSettingsServer.set_left_hand_motion_range(XRSettings.HAND_MOTION_RANGE_CONFORM_TO_CONTROLLER)
	expect_equal(fake.render_target_size_multiplier, 1.5, "the render scale goes to the interface before it is initialized")
	expect_equal(fake.foveation_level, 0, "foveation waits for the session")
	expect_equal(fake.gpu_level, -1, "the GPU level waits for the session")
	var deferred: PackedStringArray = XRSettingsServer.get_deferred_setting_names()
	expect(not deferred.has("render_target_size_multiplier"), "the render scale is not deferred")
	expect(deferred.has("foveation_level"), "foveation is deferred")
	expect(deferred.has("display_refresh_rate"), "the refresh rate is deferred")
	fake.begin_session()
	expect(XRSettingsServer.is_interface_ready(), "is_interface_ready is true after initialize")
	expect(XRSettingsServer.is_session_running(), "is_session_running is true after session_begun")
	expect(XRSettingsServer.get_deferred_setting_names().is_empty(), "nothing is deferred after the session began")
	expect_equal(fake.foveation_level, 1, "the foveation level is pushed when the session begins")
	expect(fake.foveation_dynamic, "dynamic foveation is pushed")
	expect_equal(fake.display_refresh_rate, 90.0, "the refresh rate is pushed")
	expect_equal(fake.gpu_level, PERFORMANCE_BOOST, "the GPU level is pushed as the OpenXR boost level")
	expect_equal(fake.cpu_level, -1, "the runtime default CPU level is never pushed")
	expect_equal(fake.motion_ranges[0], 1, "the left motion range is pushed")
	XRSettingsServer.set_foveation_level(XRSettings.FOVEATION_LEVEL_HIGH)
	expect_equal(fake.foveation_level, 3, "changes during the session reach the interface at once")
	fake.stop_session()
	expect(not XRSettingsServer.is_session_running(), "session_stopping ends the session")
	fake.foveation_level = 0
	fake.begin_session()
	expect_equal(fake.foveation_level, 3, "a new session gets the values again")
	XRSettingsServer.clear_interface_override()
	fake.free()
	XRSettingsServer.reset_to_defaults()


func _test_refresh_rate_matching() -> void:
	var fake: Object = FakeInterface.new()
	fake.initialized = true
	fake.refresh_rates = [120.0, 72.0, 90.0]
	XRSettingsServer.set_interface_override(fake)
	var rates: PackedFloat64Array = XRSettingsServer.get_available_display_refresh_rates()
	expect_equal(rates, PackedFloat64Array([72.0, 90.0, 120.0]), "the rates of the runtime are sorted")
	XRSettingsServer.set_display_refresh_rate(100.0)
	expect_equal(fake.display_refresh_rate, 90.0, "100 Hz goes to the nearest rate, 90 Hz")
	expect_equal(XRSettingsServer.get_display_refresh_rate(), 100.0, "the choice of the player stays stored")
	XRSettingsServer.set_display_refresh_rate(0.0)
	expect_equal(fake.display_refresh_rate, 90.0, "0, the runtime default, is not pushed")
	XRSettingsServer.clear_interface_override()
	fake.free()
	XRSettingsServer.reset_to_defaults()


func _test_unsupported() -> void:
	var fake: Object = FakeInterface.new()
	fake.initialized = true
	fake.foveation_supported = false
	fake.hand_tracking_supported = false
	XRSettingsServer.set_interface_override(fake)
	var unsupported: PackedStringArray = XRSettingsServer.get_unsupported_setting_names()
	for setting: String in ["foveation_level", "foveation_dynamic", "foveation_with_subsampled_images", "display_refresh_rate", "left_hand_motion_range", "right_hand_motion_range", "hand_tracking_enabled"]:
		expect(unsupported.has(setting), "%s is unsupported by this runtime" % setting)
	expect(not unsupported.has("render_target_size_multiplier"), "the render scale works with every runtime")
	expect(XRSettingsServer.is_value_supported(&"vrs_strength"), "vrs_strength is supported")
	expect(not XRSettingsServer.is_value_supported(&"foveation_level"), "foveation_level is not supported")
	XRSettingsServer.set_foveation_level(XRSettings.FOVEATION_LEVEL_HIGH)
	expect_equal(XRSettingsServer.get_foveation_level(), XRSettings.FOVEATION_LEVEL_HIGH, "an unsupported value is stored")
	expect_equal(fake.foveation_level, 0, "an unsupported value is not pushed")
	expect(not XRSettingsServer.get_deferred_setting_names().has("foveation_level"), "an unsupported value is not deferred")
	XRSettingsServer.set_vrs_min_radius(40.0)
	expect_equal(fake.vrs_min_radius, 40.0, "supported values still reach the interface")
	XRSettingsServer.clear_interface_override()
	fake.free()
	XRSettingsServer.reset_to_defaults()


func _test_signals() -> void:
	XRSettingsServer.foveation_level_changed.connect(func(value: int) -> void: _received["foveation_level"] = value)
	XRSettingsServer.foveation_dynamic_changed.connect(func(value: bool) -> void: _received["foveation_dynamic"] = value)
	XRSettingsServer.foveation_with_subsampled_images_changed.connect(func(value: bool) -> void: _received["subsampled"] = value)
	XRSettingsServer.render_target_size_multiplier_changed.connect(func(value: float) -> void: _received["multiplier"] = value)
	XRSettingsServer.display_refresh_rate_changed.connect(func(value: float) -> void: _received["refresh_rate"] = value)
	XRSettingsServer.vrs_min_radius_changed.connect(func(value: float) -> void: _received["vrs_radius"] = value)
	XRSettingsServer.vrs_strength_changed.connect(func(value: float) -> void: _received["vrs_strength"] = value)
	XRSettingsServer.cpu_level_changed.connect(func(value: int) -> void: _received["cpu_level"] = value)
	XRSettingsServer.gpu_level_changed.connect(func(value: int) -> void: _received["gpu_level"] = value)
	XRSettingsServer.left_hand_motion_range_changed.connect(func(value: int) -> void: _received["left_range"] = value)
	XRSettingsServer.right_hand_motion_range_changed.connect(func(value: int) -> void: _received["right_range"] = value)
	XRSettingsServer.hand_tracking_enabled_changed.connect(func(value: bool) -> void: _received["hand_tracking"] = value)
	XRSettingsServer.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	XRSettingsServer.set_foveation_level(XRSettings.FOVEATION_LEVEL_MEDIUM)
	XRSettingsServer.set_foveation_dynamic(true)
	XRSettingsServer.set_foveation_with_subsampled_images(false)
	XRSettingsServer.set_render_target_size_multiplier(1.5)
	XRSettingsServer.set_display_refresh_rate(90.0)
	XRSettingsServer.set_vrs_min_radius(40.0)
	XRSettingsServer.set_vrs_strength(2.0)
	XRSettingsServer.set_cpu_level(XRSettings.PERFORMANCE_LEVEL_SUSTAINED_LOW)
	XRSettingsServer.set_gpu_level(XRSettings.PERFORMANCE_LEVEL_SUSTAINED_HIGH)
	XRSettingsServer.set_left_hand_motion_range(XRSettings.HAND_MOTION_RANGE_CONFORM_TO_CONTROLLER)
	XRSettingsServer.set_right_hand_motion_range(XRSettings.HAND_MOTION_RANGE_CONFORM_TO_CONTROLLER)
	XRSettingsServer.set_hand_tracking_enabled(false)
	expect(_received.is_empty(), "no typed signal fires for pending changes in manual mode")
	XRSettingsServer.apply()
	expect_equal(_received.get("foveation_level"), XRSettings.FOVEATION_LEVEL_MEDIUM, "foveation_level_changed passes the level, also without a headset")
	expect_equal(_received.get("foveation_dynamic"), true, "foveation_dynamic_changed passes the flag")
	expect_equal(_received.get("subsampled"), false, "foveation_with_subsampled_images_changed passes the flag")
	expect_equal(_received.get("multiplier"), 1.5, "render_target_size_multiplier_changed passes the multiplier")
	expect_equal(_received.get("refresh_rate"), 90.0, "display_refresh_rate_changed passes the rate")
	expect_equal(_received.get("vrs_radius"), 40.0, "vrs_min_radius_changed passes the radius")
	expect_equal(_received.get("vrs_strength"), 2.0, "vrs_strength_changed passes the strength")
	expect_equal(_received.get("cpu_level"), XRSettings.PERFORMANCE_LEVEL_SUSTAINED_LOW, "cpu_level_changed passes the level")
	expect_equal(_received.get("gpu_level"), XRSettings.PERFORMANCE_LEVEL_SUSTAINED_HIGH, "gpu_level_changed passes the level")
	expect_equal(_received.get("left_range"), XRSettings.HAND_MOTION_RANGE_CONFORM_TO_CONTROLLER, "left_hand_motion_range_changed passes the range")
	expect_equal(_received.get("right_range"), XRSettings.HAND_MOTION_RANGE_CONFORM_TO_CONTROLLER, "right_hand_motion_range_changed passes the range")
	expect_equal(_received.get("hand_tracking"), false, "hand_tracking_enabled_changed passes the flag, a game-side value")
	XRSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	XRSettingsServer.reset_to_defaults()


func _test_storage() -> void:
	XRSettingsServer.set_foveation_level(XRSettings.FOVEATION_LEVEL_LOW)
	XRSettingsServer.set_render_target_size_multiplier(1.25)
	XRSettingsServer.set_cpu_level(XRSettings.PERFORMANCE_LEVEL_SUSTAINED_HIGH)
	XRSettingsServer.set_right_hand_motion_range(XRSettings.HAND_MOTION_RANGE_CONFORM_TO_CONTROLLER)
	XRSettingsServer.set_hand_tracking_enabled(false)
	expect_equal(XRSettingsServer.save(), OK, "save works without a headset")
	XRSettingsServer.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	XRSettingsServer.reset_to_defaults()
	expect_equal(XRSettingsServer.load(), OK, "load reads the file")
	expect_equal(XRSettingsServer.get_foveation_level(), XRSettings.FOVEATION_LEVEL_LOW, "the foveation level is loaded")
	expect_equal(XRSettingsServer.get_render_target_size_multiplier(), 1.25, "the render scale is loaded")
	expect_equal(XRSettingsServer.get_cpu_level(), XRSettings.PERFORMANCE_LEVEL_SUSTAINED_HIGH, "the CPU level is loaded")
	expect_equal(XRSettingsServer.get_right_hand_motion_range(), XRSettings.HAND_MOTION_RANGE_CONFORM_TO_CONTROLLER, "the right motion range is loaded")
	expect(not XRSettingsServer.is_hand_tracking_enabled(), "the hand tracking choice is loaded")
	XRSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE


func _test_resource() -> void:
	var settings := XRSettings.new()
	expect_equal(settings.foveation_level, XRSettings.FOVEATION_LEVEL_OFF, "XRSettings: default foveation_level")
	expect(settings.foveation_with_subsampled_images, "XRSettings: foveation_with_subsampled_images by default")
	expect_equal(settings.render_target_size_multiplier, 1.0, "XRSettings: default render_target_size_multiplier")
	expect_equal(settings.display_refresh_rate, 0.0, "XRSettings: default display_refresh_rate")
	expect_equal(settings.vrs_min_radius, 20.0, "XRSettings: default vrs_min_radius")
	expect_equal(settings.vrs_strength, 1.0, "XRSettings: default vrs_strength")
	expect_equal(settings.cpu_level, XRSettings.PERFORMANCE_LEVEL_RUNTIME_DEFAULT, "XRSettings: default cpu_level")
	expect(settings.hand_tracking_enabled, "XRSettings: hand_tracking_enabled by default")
	settings.vrs_strength = 2.0
	expect_equal(settings.vrs_strength, 2.0, "XRSettings: vrs_strength is a plain property")
