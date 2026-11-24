extends FeatureCheck
## GDScript checks of 26-feature-engine-settings. Check the same things as scripts/cs/Issue26FeatureEngineSettingsCheck.cs.
##
## The simulation values (time scale, physics rate, audio speed) are locked unless the project setting
## settings/engine/allow_simulation_values is enabled. The checks enable it at runtime where they change those
## values and switch it off again. The restart checks start the project again as its own processes (desktop only):
## the probe runs with the project's setting (locked), so a stored time scale must be ignored there.

const PROBE_SCENE := "res://26-feature-engine-settings/scenes/restart_probe.tscn"
const ENGINE_FILE := "user://settings/engine.ini"
const ALLOW_SIMULATION := "settings/engine/allow_simulation_values"


func _get_check_name() -> String:
	return "26-feature-engine-settings"


func _run_checks() -> void:
	expect(Engine.has_singleton("EngineSettingsServer"), "EngineSettingsServer is a registered singleton")
	expect(Engine.get_singleton("EngineSettingsServer") is SettingsServer, "EngineSettingsServer is a SettingsServer")
	expect_equal(SettingsServerManager.get_server(&"engine"), Engine.get_singleton("EngineSettingsServer"), "the domain is registered as \"engine\"")
	expect(EngineSettingsServer.is_started(), "the domain was started before the main scene")
	expect_equal(EngineSettingsServer.get_apply_phase(), SettingsServer.APPLY_PHASE_SCENE, "applied after the SceneTree exists")
	expect_equal(EngineSettingsServer.get_file_path(), ENGINE_FILE, "file path")
	var names := EngineSettingsServer.get_setting_names()
	for setting in ["max_fps", "time_scale", "physics_ticks_per_second", "physics_jitter_fix", "physics_interpolation", "max_physics_steps_per_frame", "audio_playback_speed_scale", "low_processor_mode", "low_processor_mode_sleep_usec"]:
		expect(names.has(setting), "setting \"%s\" exists" % setting)

	_check_defaults()
	_check_apply()
	_check_lock()
	_check_validation()
	_check_session_override()
	_check_resource()
	await _check_restart()

	# Leave no player files and no changed engine values behind.
	_reset()
	await get_tree().process_frame
	DirAccess.remove_absolute(ENGINE_FILE)


func _set_allow_simulation(allowed: bool) -> void:
	ProjectSettings.set_setting(ALLOW_SIMULATION, allowed)


func _reset() -> void:
	_set_allow_simulation(true)
	EngineSettingsServer.reset_to_defaults()
	_set_allow_simulation(false)


func _check_defaults() -> void:
	_reset()
	expect_equal(ProjectSettings.get_setting(ALLOW_SIMULATION), false, "simulation values are locked by default")
	expect_equal(EngineSettingsServer.get_default_value(&"max_fps"), ProjectSettings.get_setting("application/run/max_fps"), "default max_fps is the project setting")
	expect_equal(EngineSettingsServer.get_default_value(&"physics_ticks_per_second"), ProjectSettings.get_setting("physics/common/physics_ticks_per_second"), "default physics rate is the project setting")
	expect_equal(EngineSettingsServer.get_default_value(&"max_physics_steps_per_frame"), ProjectSettings.get_setting("physics/common/max_physics_steps_per_frame"), "default physics steps is the project setting")
	expect_equal(EngineSettingsServer.get_default_value(&"physics_interpolation"), ProjectSettings.get_setting("physics/common/physics_interpolation"), "default physics interpolation is the project setting")
	expect_equal(EngineSettingsServer.get_default_value(&"low_processor_mode"), ProjectSettings.get_setting("application/run/low_processor_mode"), "default low-processor mode is the project setting")
	expect_equal(EngineSettingsServer.get_default_value(&"time_scale"), 1.0, "default time scale is 1.0")
	expect_equal(EngineSettingsServer.get_default_value(&"audio_playback_speed_scale"), 1.0, "default audio speed is 1.0")
	expect_equal(Engine.time_scale, 1.0, "the engine runs at the project time scale")


func _check_apply() -> void:
	EngineSettingsServer.set_max_fps(45)
	expect_equal(EngineSettingsServer.get_max_fps(), 45, "get_max_fps()")
	expect_equal(Engine.max_fps, 45, "max_fps reaches Engine at once")
	EngineSettingsServer.set_physics_interpolation_enabled(true)
	expect(get_tree().physics_interpolation, "physics interpolation reaches the SceneTree")
	EngineSettingsServer.set_low_processor_mode_sleep_usec(10000)
	expect_equal(OS.low_processor_usage_mode_sleep_usec, 10000, "the low-processor sleep reaches OS")
	EngineSettingsServer.set_low_processor_mode_enabled(true)
	expect(OS.low_processor_usage_mode, "the low-processor mode reaches OS")
	EngineSettingsServer.set_low_processor_mode_enabled(false)

	_set_allow_simulation(true)
	expect(not EngineSettingsServer.is_simulation_locked(), "is_simulation_locked() follows the project setting")
	EngineSettingsServer.set_time_scale(0.5)
	expect_equal(Engine.time_scale, 0.5, "time_scale reaches Engine at once")
	EngineSettingsServer.set_physics_ticks_per_second(90)
	expect_equal(Engine.physics_ticks_per_second, 90, "the physics rate reaches Engine")
	EngineSettingsServer.set_max_physics_steps_per_frame(4)
	expect_equal(Engine.max_physics_steps_per_frame, 4, "the physics steps reach Engine")
	EngineSettingsServer.set_physics_jitter_fix(0.25)
	expect(is_equal_approx(Engine.physics_jitter_fix, 0.25), "the jitter fix reaches Engine")
	EngineSettingsServer.set_audio_playback_speed_scale(1.5)
	expect(is_equal_approx(AudioServer.playback_speed_scale, 1.5), "the audio speed reaches AudioServer")

	# Changed directly on the engine, read back.
	Engine.max_fps = 75
	EngineSettingsServer.read_from_engine()
	expect_equal(EngineSettingsServer.get_max_fps(), 75, "read_from_engine() reads Engine.max_fps back")
	_reset()
	expect_equal(Engine.time_scale, 1.0, "reset_to_defaults() gives back the time scale")
	expect_equal(Engine.max_fps, ProjectSettings.get_setting("application/run/max_fps"), "reset_to_defaults() gives back max_fps")


func _check_lock() -> void:
	_set_allow_simulation(false)
	expect(EngineSettingsServer.is_simulation_locked(), "locked without the project setting")
	var simulation := EngineSettingsServer.get_simulation_setting_names()
	for setting in ["time_scale", "physics_ticks_per_second", "max_physics_steps_per_frame", "physics_jitter_fix", "audio_playback_speed_scale"]:
		expect(simulation.has(setting), "\"%s\" is a simulation value" % setting)
		expect(EngineSettingsServer.is_simulation_setting(setting), "is_simulation_setting(\"%s\")" % setting)
		expect(not EngineSettingsServer.is_value_supported(setting), "locked: \"%s\" is reported as unsupported" % setting)
	expect(not EngineSettingsServer.is_simulation_setting(&"max_fps"), "max_fps is not a simulation value")
	expect(EngineSettingsServer.is_value_supported(&"max_fps"), "max_fps is always supported")
	if OS.has_feature("web"):
		expect(not EngineSettingsServer.is_value_supported(&"low_processor_mode_sleep_usec"), "web: the low-processor sleep is unsupported")

	EngineSettingsServer.set_time_scale(3.0)
	expect_equal(EngineSettingsServer.get_time_scale(), 1.0, "locked: the time scale stays at the project value")
	expect_equal(Engine.time_scale, 1.0, "locked: Engine.time_scale is unchanged")
	EngineSettingsServer.set_physics_ticks_per_second(30)
	expect_equal(Engine.physics_ticks_per_second, ProjectSettings.get_setting("physics/common/physics_ticks_per_second"), "locked: the physics rate is unchanged")
	_reset()


func _check_validation() -> void:
	_set_allow_simulation(true)
	EngineSettingsServer.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	EngineSettingsServer.set_max_fps(-5)
	expect_equal(EngineSettingsServer.get_max_fps(), 0, "max_fps is at least 0 (unlimited)")
	EngineSettingsServer.set_time_scale(0.0)
	expect(is_equal_approx(EngineSettingsServer.get_time_scale(), 0.01), "the time scale is at least 0.01")
	EngineSettingsServer.set_time_scale(500.0)
	expect(is_equal_approx(EngineSettingsServer.get_time_scale(), 100.0), "the time scale is at most 100")
	EngineSettingsServer.set_physics_ticks_per_second(0)
	expect_equal(EngineSettingsServer.get_physics_ticks_per_second(), 1, "the physics rate is at least 1")
	EngineSettingsServer.set_max_physics_steps_per_frame(1000)
	expect_equal(EngineSettingsServer.get_max_physics_steps_per_frame(), 100, "the physics steps are at most 100")
	EngineSettingsServer.set_audio_playback_speed_scale(10.0)
	expect(is_equal_approx(EngineSettingsServer.get_audio_playback_speed_scale(), 4.0), "the audio speed is at most 4")
	EngineSettingsServer.set_low_processor_mode_sleep_usec(5000000)
	expect_equal(EngineSettingsServer.get_low_processor_mode_sleep_usec(), 1000000, "the low-processor sleep is at most one second")
	expect_equal(Engine.time_scale, 1.0, "manual mode: nothing reached the engine before apply()")
	EngineSettingsServer.revert()
	EngineSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	_reset()


func _check_session_override() -> void:
	EngineSettingsServer.set_max_fps(120)
	EngineSettingsServer.set_session_override(&"max_fps", 30)
	expect_equal(Engine.max_fps, 30, "a session override reaches the engine")
	expect(EngineSettingsServer.has_session_override(&"max_fps"), "has_session_override()")
	expect_equal(EngineSettingsServer.get_max_fps(), 120, "the stored value stays")
	EngineSettingsServer.read_from_engine()
	expect_equal(EngineSettingsServer.get_max_fps(), 120, "read_from_engine() does not store the override")
	EngineSettingsServer.clear_session_override(&"max_fps")
	expect_equal(Engine.max_fps, 120, "clear_session_override() gives back the stored value")
	_reset()


func _check_resource() -> void:
	var settings := EngineSettings.new()
	expect_equal(settings.max_fps, 0, "EngineSettings: default max_fps")
	expect_equal(settings.time_scale, 1.0, "EngineSettings: default time scale")
	expect_equal(settings.physics_ticks_per_second, 60, "EngineSettings: default physics rate")
	expect_equal(settings.max_physics_steps_per_frame, 8, "EngineSettings: default physics steps")
	expect_equal(settings.low_processor_mode, false, "EngineSettings: default low-processor mode")
	settings.time_scale = 250.0
	expect_equal(settings.time_scale, 250.0, "EngineSettings: the resource stores raw values; the server clamps")


func _check_restart() -> void:
	if not OS.has_feature("pc"):
		print("  SKIP  restart check (needs a desktop platform to start a second process)")
		return

	# 1) Saved values are applied before the main scene; a stored time scale is ignored while the project locks it.
	# save() writes the applied values; the domain is reset only after the probes, so the file stays as saved.
	_set_allow_simulation(true)
	EngineSettingsServer.set_max_fps(45)
	EngineSettingsServer.set_physics_interpolation_enabled(true)
	EngineSettingsServer.set_low_processor_mode_sleep_usec(12000)
	EngineSettingsServer.set_time_scale(0.5)
	expect_equal(EngineSettingsServer.save(), OK, "save() writes the player file")
	_set_allow_simulation(false)
	expect(FileAccess.file_exists(ENGINE_FILE), "the player file is in user://settings/")
	var text := FileAccess.get_file_as_string(ENGINE_FILE)
	expect(text.contains("max_fps") and text.contains("45"), "the file holds max_fps = 45")

	var probe := _run_probe([])
	expect(probe.get("started", false), "restart: the domain was started")
	expect_equal(int(probe.get("ready_frame", -1)), 0, "restart: applied before the first frame")
	expect_equal(int(probe.get("engine_max_fps", -1)), 45, "restart: Engine.max_fps is the saved value")
	expect_equal(int(probe.get("max_fps", -1)), 45, "restart: get_max_fps() is the saved value")
	expect(probe.get("physics_interpolation", false), "restart: the SceneTree has physics interpolation")
	expect_equal(int(probe.get("sleep_usec", -1)), 12000, "restart: OS has the saved low-processor sleep")
	expect(probe.get("simulation_locked", false), "restart: the project locks the simulation values")
	expect_equal(float(probe.get("engine_time_scale", -1.0)), 1.0, "restart: a stored time scale is ignored while locked")

	# 2) --max-fps and --time-scale are session overrides: active (even when locked), but never saved.
	probe = _run_probe(["--max-fps", "30", "--time-scale", "2"])
	expect_equal(int(probe.get("engine_max_fps", -1)), 30, "--max-fps reaches the engine")
	expect_equal(float(probe.get("engine_time_scale", -1.0)), 2.0, "--time-scale reaches the engine, even when locked")
	expect(probe.get("max_fps_overridden", false), "--max-fps is a session override")
	expect(probe.get("time_scale_overridden", false), "--time-scale is a session override")
	expect_equal(int(probe.get("max_fps", -1)), 45, "--max-fps does not change the stored value")
	expect(FileAccess.get_file_as_string(ENGINE_FILE).contains("45"), "--max-fps is not saved: the file still has 45")


func _run_probe(extra_args: Array) -> Dictionary:
	var output := []
	var args := PackedStringArray(["--headless"])
	args.append_array(PackedStringArray(extra_args))
	args.append_array(PackedStringArray(["--path", ProjectSettings.globalize_path("res://"), PROBE_SCENE]))
	var code := OS.execute(OS.get_executable_path(), args, output, true)
	expect_equal(code, 0, "restart: the probe process ran")
	for chunk in output:
		for line in str(chunk).split("\n"):
			if line.begins_with("CHECK26_PROBE "):
				var parsed = JSON.parse_string(line.trim_prefix("CHECK26_PROBE "))
				if parsed is Dictionary:
					return parsed
	expect(false, "restart: the probe printed its result")
	return {}
