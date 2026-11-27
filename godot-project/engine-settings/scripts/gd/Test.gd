extends FeatureTest
## Full API test of Engine Settings (EngineSettingsServer, EngineSettings). Same tests as scripts/cs/Test.cs.
## The simulation values (time scale, physics rate, audio speed) are locked unless the project setting
## settings/engine/allow_simulation_values is on. The tests switch it on at runtime where they change those values and
## off again. The restart test starts the project again as its own process (desktop only): the probe runs with the
## project setting (locked), so a stored time scale must be ignored there.

const PROBE_SCENE := "res://engine-settings/scenes/restart_probe.tscn"
const ENGINE_FILE := "user://settings/engine.ini"
const ALLOW_SIMULATION := "settings/engine/allow_simulation_values"
const SIMULATION_NAMES: Array[StringName] = [&"time_scale", &"physics_ticks_per_second", &"max_physics_steps_per_frame", &"physics_jitter_fix", &"audio_playback_speed_scale"]

var _received: Dictionary = {}


func _run_tests() -> void:
	_test_defaults()
	_test_values()
	_test_lock()
	_test_validation()
	_test_signals()
	_test_resource()
	_test_restart()
	_reset()
	DirAccess.remove_absolute(ENGINE_FILE)


func _allow_simulation(allowed: bool) -> void:
	ProjectSettings.set_setting(ALLOW_SIMULATION, allowed)


func _reset() -> void:
	_allow_simulation(true)
	EngineSettingsServer.reset_to_defaults()
	_allow_simulation(false)


func _test_defaults() -> void:
	_reset()
	var project_fps: int = ProjectSettings.get_setting("application/run/max_fps")
	var project_ticks: int = ProjectSettings.get_setting("physics/common/physics_ticks_per_second")
	var project_steps: int = ProjectSettings.get_setting("physics/common/max_physics_steps_per_frame")
	var project_interpolation: bool = ProjectSettings.get_setting("physics/common/physics_interpolation")
	var project_low: bool = ProjectSettings.get_setting("application/run/low_processor_mode")
	expect_equal(ProjectSettings.get_setting(ALLOW_SIMULATION), false, "simulation values are locked by default")
	expect_equal(EngineSettingsServer.get_max_fps(), project_fps, "the default max_fps is the project setting")
	expect_equal(EngineSettingsServer.get_physics_ticks_per_second(), project_ticks, "the default physics rate is the project setting")
	expect_equal(EngineSettingsServer.get_max_physics_steps_per_frame(), project_steps, "the default physics steps are the project setting")
	expect_equal(EngineSettingsServer.is_physics_interpolation_enabled(), project_interpolation, "the default physics interpolation is the project setting")
	expect_equal(EngineSettingsServer.is_low_processor_mode_enabled(), project_low, "the default low-processor mode is the project setting")
	expect_equal(EngineSettingsServer.get_time_scale(), 1.0, "the default time scale is 1")
	expect_equal(EngineSettingsServer.get_audio_playback_speed_scale(), 1.0, "the default audio speed is 1")
	expect_equal(Engine.time_scale, 1.0, "the engine runs at the project time scale")


func _test_values() -> void:
	EngineSettingsServer.set_max_fps(45)
	expect_equal(EngineSettingsServer.get_max_fps(), 45, "max_fps round trip")
	expect_equal(Engine.max_fps, 45, "max_fps reaches Engine at once")
	EngineSettingsServer.set_physics_interpolation_enabled(true)
	expect(EngineSettingsServer.is_physics_interpolation_enabled(), "physics_interpolation round trip")
	expect(get_tree().physics_interpolation, "physics_interpolation reaches the SceneTree")
	EngineSettingsServer.set_low_processor_mode_sleep_usec(10000)
	expect_equal(EngineSettingsServer.get_low_processor_mode_sleep_usec(), 10000, "low_processor_mode_sleep_usec round trip")
	expect_equal(OS.low_processor_usage_mode_sleep_usec, 10000, "the low-processor sleep reaches OS")
	EngineSettingsServer.set_low_processor_mode_enabled(true)
	expect(EngineSettingsServer.is_low_processor_mode_enabled(), "low_processor_mode round trip")
	expect(OS.low_processor_usage_mode, "the low-processor mode reaches OS")
	EngineSettingsServer.set_low_processor_mode_enabled(false)
	_allow_simulation(true)
	expect(not EngineSettingsServer.is_simulation_locked(), "is_simulation_locked follows the project setting")
	EngineSettingsServer.set_time_scale(0.5)
	expect_equal(EngineSettingsServer.get_time_scale(), 0.5, "time_scale round trip")
	expect_equal(Engine.time_scale, 0.5, "time_scale reaches Engine at once")
	EngineSettingsServer.set_physics_ticks_per_second(90)
	expect_equal(Engine.physics_ticks_per_second, 90, "the physics rate reaches Engine")
	EngineSettingsServer.set_max_physics_steps_per_frame(4)
	expect_equal(Engine.max_physics_steps_per_frame, 4, "the physics steps reach Engine")
	EngineSettingsServer.set_physics_jitter_fix(0.25)
	expect_equal(Engine.physics_jitter_fix, 0.25, "the jitter fix reaches Engine")
	EngineSettingsServer.set_audio_playback_speed_scale(1.5)
	expect_equal(AudioServer.playback_speed_scale, 1.5, "the audio speed reaches AudioServer")
	Engine.max_fps = 75
	EngineSettingsServer.read_from_engine()
	expect_equal(EngineSettingsServer.get_max_fps(), 75, "read_from_engine reads Engine.max_fps back")
	_reset()
	expect_equal(Engine.time_scale, 1.0, "reset_to_defaults gives the time scale back")
	var project_fps: int = ProjectSettings.get_setting("application/run/max_fps")
	expect_equal(Engine.max_fps, project_fps, "reset_to_defaults gives max_fps back")


func _test_lock() -> void:
	expect(EngineSettingsServer.is_simulation_locked(), "the simulation is locked without the project setting")
	var simulation: PackedStringArray = EngineSettingsServer.get_simulation_setting_names()
	for setting: StringName in SIMULATION_NAMES:
		expect(simulation.has(setting), "%s is a simulation value" % setting)
		expect(EngineSettingsServer.is_simulation_setting(setting), "is_simulation_setting(%s) is true" % setting)
		expect(not EngineSettingsServer.is_value_supported(setting), "locked: %s is reported as unsupported" % setting)
	expect(not EngineSettingsServer.is_simulation_setting(&"max_fps"), "max_fps is not a simulation value")
	expect(EngineSettingsServer.is_value_supported(&"max_fps"), "max_fps is always supported")
	EngineSettingsServer.set_time_scale(3.0)
	expect_equal(EngineSettingsServer.get_time_scale(), 1.0, "locked: the time scale stays at the project value")
	expect_equal(Engine.time_scale, 1.0, "locked: Engine.time_scale is unchanged")
	EngineSettingsServer.set_physics_ticks_per_second(30)
	var project_ticks: int = ProjectSettings.get_setting("physics/common/physics_ticks_per_second")
	expect_equal(Engine.physics_ticks_per_second, project_ticks, "locked: the physics rate is unchanged")
	_reset()


func _test_validation() -> void:
	_allow_simulation(true)
	EngineSettingsServer.apply_mode = SettingsServer.APPLY_MODE_MANUAL
	EngineSettingsServer.set_max_fps(-5)
	expect_equal(EngineSettingsServer.get_max_fps(), 0, "max_fps is at least 0, unlimited")
	EngineSettingsServer.set_time_scale(0.0)
	expect(is_equal_approx(EngineSettingsServer.get_time_scale(), 0.01), "the time scale is at least 0.01")
	EngineSettingsServer.set_time_scale(500.0)
	expect_equal(EngineSettingsServer.get_time_scale(), 100.0, "the time scale is at most 100")
	EngineSettingsServer.set_physics_ticks_per_second(0)
	expect_equal(EngineSettingsServer.get_physics_ticks_per_second(), 1, "the physics rate is at least 1")
	EngineSettingsServer.set_max_physics_steps_per_frame(1000)
	expect_equal(EngineSettingsServer.get_max_physics_steps_per_frame(), 100, "the physics steps are at most 100")
	EngineSettingsServer.set_audio_playback_speed_scale(10.0)
	expect_equal(EngineSettingsServer.get_audio_playback_speed_scale(), 4.0, "the audio speed is at most 4")
	EngineSettingsServer.set_low_processor_mode_sleep_usec(5000000)
	expect_equal(EngineSettingsServer.get_low_processor_mode_sleep_usec(), 1000000, "the low-processor sleep is at most one second")
	expect_equal(Engine.time_scale, 1.0, "manual mode: nothing reached the engine before apply")
	EngineSettingsServer.revert()
	EngineSettingsServer.apply_mode = SettingsServer.APPLY_MODE_IMMEDIATE
	_reset()


func _test_signals() -> void:
	EngineSettingsServer.max_fps_changed.connect(func(value: int) -> void: _received["max_fps"] = value)
	EngineSettingsServer.low_processor_mode_changed.connect(func(value: bool) -> void: _received["low_processor_mode"] = value)
	EngineSettingsServer.physics_interpolation_changed.connect(func(value: bool) -> void: _received["physics_interpolation"] = value)
	EngineSettingsServer.physics_ticks_per_second_changed.connect(func(value: int) -> void: _received["physics_ticks"] = value)
	EngineSettingsServer.time_scale_changed.connect(func(value: float) -> void: _received["time_scale"] = value)
	_allow_simulation(true)
	EngineSettingsServer.set_max_fps(30)
	EngineSettingsServer.set_low_processor_mode_enabled(true)
	EngineSettingsServer.set_physics_interpolation_enabled(true)
	EngineSettingsServer.set_physics_ticks_per_second(90)
	EngineSettingsServer.set_time_scale(0.5)
	expect_equal(_received.get("max_fps"), 30, "max_fps_changed passes the limit")
	expect_equal(_received.get("low_processor_mode"), true, "low_processor_mode_changed passes the flag")
	expect_equal(_received.get("physics_interpolation"), true, "physics_interpolation_changed passes the flag")
	expect_equal(_received.get("physics_ticks"), 90, "physics_ticks_per_second_changed passes the rate")
	expect_equal(_received.get("time_scale"), 0.5, "time_scale_changed passes the scale")
	_reset()


func _test_resource() -> void:
	var settings := EngineSettings.new()
	expect_equal(settings.max_fps, 0, "EngineSettings: default max_fps")
	expect_equal(settings.time_scale, 1.0, "EngineSettings: default time_scale")
	expect_equal(settings.physics_ticks_per_second, 60, "EngineSettings: default physics_ticks_per_second")
	expect_equal(settings.max_physics_steps_per_frame, 8, "EngineSettings: default max_physics_steps_per_frame")
	expect_equal(settings.physics_jitter_fix, 0.5, "EngineSettings: default physics_jitter_fix")
	expect_equal(settings.audio_playback_speed_scale, 1.0, "EngineSettings: default audio_playback_speed_scale")
	expect(not settings.low_processor_mode, "EngineSettings: low_processor_mode is off by default")
	expect(not settings.physics_interpolation, "EngineSettings: physics_interpolation is off by default")
	expect_equal(settings.low_processor_mode_sleep_usec, 6900, "EngineSettings: default low_processor_mode_sleep_usec")
	settings.time_scale = 250.0
	expect_equal(settings.time_scale, 250.0, "EngineSettings: the resource stores raw values, the server clamps")


func _test_restart() -> void:
	if not OS.has_feature("pc"):
		print("  SKIP  restart test (needs a desktop platform to start a second process)")
		return
	_allow_simulation(true)
	EngineSettingsServer.set_max_fps(45)
	EngineSettingsServer.set_physics_interpolation_enabled(true)
	EngineSettingsServer.set_low_processor_mode_sleep_usec(12000)
	EngineSettingsServer.set_time_scale(0.5)
	expect_equal(EngineSettingsServer.save(), OK, "save writes the player file")
	_allow_simulation(false)
	var probe: Dictionary = _run_probe([])
	expect_equal(probe.get("ready_frame"), 0.0, "restart: applied before the first frame")
	expect_equal(probe.get("engine_max_fps"), 45.0, "restart: Engine.max_fps is the saved value")
	expect_equal(probe.get("max_fps"), 45.0, "restart: get_max_fps is the saved value")
	expect_equal(probe.get("physics_interpolation"), true, "restart: the SceneTree has physics interpolation")
	expect_equal(probe.get("sleep_usec"), 12000.0, "restart: OS has the saved low-processor sleep")
	expect_equal(probe.get("simulation_locked"), true, "restart: the project locks the simulation values")
	expect_equal(probe.get("engine_time_scale"), 1.0, "restart: a stored time scale is ignored while locked")
	probe = _run_probe(["--max-fps", "30", "--time-scale", "2"])
	expect_equal(probe.get("engine_max_fps"), 30.0, "restart: --max-fps reaches the engine")
	expect_equal(probe.get("engine_time_scale"), 2.0, "restart: --time-scale reaches the engine, even when locked")
	expect_equal(probe.get("max_fps_overridden"), true, "restart: --max-fps is a session override")
	expect_equal(probe.get("time_scale_overridden"), true, "restart: --time-scale is a session override")
	expect_equal(probe.get("max_fps"), 45.0, "restart: --max-fps does not change the stored value")


func _run_probe(extra_args: Array[String]) -> Dictionary:
	var args := PackedStringArray(["--headless"])
	args.append_array(PackedStringArray(extra_args))
	args.append_array(PackedStringArray(["--path", ProjectSettings.globalize_path("res://"), PROBE_SCENE]))
	var output: Array = []
	var code: int = OS.execute(OS.get_executable_path(), args, output, true)
	expect_equal(code, 0, "restart: the probe process ran")
	for line: String in str(output[0]).split("\n"):
		if line.begins_with("PROBE "):
			return JSON.parse_string(line.trim_prefix("PROBE "))
	expect(false, "restart: the probe printed its result")
	return {}
