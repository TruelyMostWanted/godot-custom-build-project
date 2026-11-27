extends Node
## Started as a separate process by the tests: prints the language values that the language settings domain
## applied before the main scene was ready, then quits.


func _ready() -> void:
	var root := get_tree().root
	var probe := {
		"ready_frame": Engine.get_process_frames(),
		"started": LanguageSettingsServer.is_started(),
		"locale": TranslationServer.get_locale(),
		"project_locale": TranslationServer.standardize_locale(LanguageSettingsServer.get_project_locale()),
		"command_line_locale": LanguageSettingsServer.get_command_line_locale(),
		"hello": tr("CHECK17_HELLO"),
		"pseudolocalization": TranslationServer.pseudolocalization_enabled,
		"layout_direction": root.get_layout_direction(),
		"auto_translate_mode": root.auto_translate_mode,
	}
	print("PROBE " + JSON.stringify(probe))
	get_tree().quit(0)
