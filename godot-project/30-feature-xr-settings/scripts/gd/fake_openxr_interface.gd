extends Object
## Stands in for OpenXRInterface in the checks of 30-feature-xr-settings: the same properties, methods and
## signals that XRSettingsServer uses, but no OpenXR runtime. Passed to XRSettingsServer.set_interface_override().
## Used by scripts/gd/check.gd and scripts/cs/Issue30FeatureXrSettingsCheck.cs. Free it with free().

signal session_begun
signal session_stopping

var initialized := false
var foveation_supported := true
var hand_tracking_supported := true
var refresh_rates: Array = []

var foveation_level := 0
var foveation_dynamic := false
var foveation_with_subsampled_images := false
var render_target_size_multiplier := 1.0
var display_refresh_rate := 0.0
var vrs_min_radius := 20.0
var vrs_strength := 1.0
var cpu_level := -1 # -1: never set.
var gpu_level := -1
var motion_ranges: Array[int] = [0, 0]


func is_initialized() -> bool:
	return initialized


func is_foveation_supported() -> bool:
	return foveation_supported


func is_hand_tracking_supported() -> bool:
	return hand_tracking_supported


func get_available_display_refresh_rates() -> Array:
	return refresh_rates if initialized else []


func set_cpu_level(level: int) -> void:
	cpu_level = level


func set_gpu_level(level: int) -> void:
	gpu_level = level


func set_motion_range(hand: int, motion: int) -> void:
	motion_ranges[clampi(hand, 0, 1)] = motion


func get_motion_range(hand: int) -> int:
	return motion_ranges[clampi(hand, 0, 1)]


## Starts a "session": what the runtime does when the headset is ready.
func begin_session() -> void:
	initialized = true
	session_begun.emit()


func stop_session() -> void:
	session_stopping.emit()
