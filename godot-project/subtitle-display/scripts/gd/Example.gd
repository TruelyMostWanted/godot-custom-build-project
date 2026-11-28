extends Node
## Minimal example of Subtitle Display: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Subtitle-Display


func _ready() -> void:
	# The player the label follows.
	var player := SubtitlePlayer.new()
	player.name = "SubtitlePlayer"
	add_child(player)

	# Adding a SubtitleLabel.
	var label := SubtitleLabel.new()
	add_child(label) # First: get_path_to() needs a common ancestor.
	label.player = label.get_path_to($SubtitlePlayer)
	label.font_size = 36
	label.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE, Control.PRESET_MODE_MINSIZE, 48)
	label.grow_vertical = Control.GROW_DIRECTION_BEGIN

	# Speaker names and colors.
	label.speaker_colors = { &"Anna": Color.GOLD, &"Ben": Color.SKY_BLUE }
	label.show_speaker_names = false

	# Following the accessibility settings.
	print(label.get_display_font_size()) # font_size * subtitle scale * font size factor
	label.follow_accessibility_settings = false # Always on, always font_size.

	# Captions.
	AccessibilitySettingsServer.set_captions_enabled(false) # The player's choice.
	SubtitleServer.captions_enabled = false # The game's switch for all tracks.
	print(label.get_subtitle_text())

	SubtitleServer.captions_enabled = true
	AccessibilitySettingsServer.set_captions_enabled(true)
	get_tree().quit()
