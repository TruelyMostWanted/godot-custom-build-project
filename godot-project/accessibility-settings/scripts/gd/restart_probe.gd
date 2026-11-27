extends Node
## Started as a separate process by the Test.gd: prints the accessibility values that were loaded and applied to
## the root window before the main scene was ready, then quits.


func _ready() -> void:
	var server: Object = Engine.get_singleton("AccessibilitySettingsServer")
	var button := Button.new()
	button.text = "Play"
	add_child(button)
	var overlay: Theme = server.get_overlay_theme()
	var ready_frame := Engine.get_process_frames()
	var overlay_on_root: bool = overlay != null and get_tree().root.theme == overlay
	# Controls see the root theme from the next frame, before the first draw.
	await get_tree().process_frame
	var probe := {
		"ready_frame": ready_frame,
		"started": server.is_started(),
		"ui_scale": server.get_ui_scale(),
		"root_factor": get_tree().root.content_scale_factor,
		"video_factor": Engine.get_singleton("WindowSettingsServer").get_content_scale_factor(),
		"color_scheme": server.get_color_scheme(),
		"overlay_on_root": overlay_on_root,
		"button_font_white": button.get_theme_color(&"font_color") == Color.WHITE,
		"reduce_motion": server.is_reducing_motion(),
	}
	print("PROBE " + JSON.stringify(probe))
	get_tree().quit(0)
