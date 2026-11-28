extends Node
## Minimal example of Subtitles: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Subtitles

const RES := "res://subtitles/resources/"


func _ready() -> void:
	# Loading a SubRip file (exported from a video editor: the timeline starts at 01:00:00).
	var subtitle: Subtitle = load(RES + "video_editor_export.srt")
	subtitle.time_offset = -3600.0
	for segment: SubtitleSegment in subtitle.segments:
		print(segment.start_time, " ", segment.text)

	# Subtitle formats.
	var interview: Subtitle = load(RES + "interview.vtt")
	ResourceSaver.save(interview, "user://interview.ass") # Same segments as Advanced SubStation Alpha.
	var ttml: String = interview.to_text(Subtitle.FORMAT_TTML)
	print("TTML: ", ttml.length(), " characters")

	# Creating subtitles in code.
	var door := Subtitle.new()
	var line := SubtitleSegment.new()
	line.start_time = 1.5
	line.end_time = 4.0
	line.text = "Who is there?"
	line.speaker = &"guard"
	door.add_segment(line)
	var knock := SubtitleSegment.new()
	knock.start_time = 0.5
	knock.end_time = 1.5
	knock.text = "Knocking"
	knock.kind = SubtitleSegment.KIND_SOUND
	door.add_segment(knock) # Inserted before the line: the list stays sorted.
	ResourceSaver.save(door, "user://door.srt")
	print("door.srt: ", FileAccess.get_file_as_string("user://door.srt").replace("\n", " | "))

	# Playing subtitles with a voice line.
	SubtitleServer.text_changed.connect(_on_subtitle_text_changed)
	SubtitleServer.play(subtitle, $Voice.get_playback_position)
	$Voice.play()
	await get_tree().create_timer(0.8).timeout
	print("Label: ", $SubtitleLabel.text.replace("\n", " "))
	SubtitleServer.stop_all()

	# Following the track.
	var track_id: int = SubtitleServer.play(subtitle) # Own clock from 0.
	SubtitleServer.seek(track_id, 13.0)
	print("At 13 s: ", SubtitleServer.get_track_text(track_id))
	SubtitleServer.pause(track_id)
	SubtitleServer.resume(track_id)
	SubtitleServer.stop(track_id)

	# Following the player's settings.
	SubtitleServer.enabled = AccessibilitySettingsServer.is_subtitles_enabled()
	AccessibilitySettingsServer.subtitles_enabled_changed.connect(func(enabled: bool) -> void:
		SubtitleServer.enabled = enabled)
	print("Subtitles enabled: ", SubtitleServer.enabled)

	# Checking a subtitle.
	var check: SubtitleValidation = subtitle.validate(17.0) # Characters per second.
	if not check.valid:
		print("Too fast to read: ", check.fast_reading_segments)

	# Timing tools of the Subtitles dock as methods.
	var timed := Subtitle.new()
	var first := SubtitleSegment.new()
	first.start_time = 1.5
	first.end_time = 3.0
	first.text = "Hello there"
	timed.add_segment(first)
	timed.shift_times(-0.5)
	timed.sync_times(1.0, 2.0, 59.5, 62.5)
	var second_index: int = timed.split_segment(0, 2.5)
	timed.merge_segments(0)
	print("Split made segment ", second_index, ", segments after merge: ", timed.get_segment_count())

	SubtitleServer.text_changed.disconnect(_on_subtitle_text_changed)
	get_tree().quit()


func _on_subtitle_text_changed(_track_id: int, text: String) -> void:
	$SubtitleLabel.text = text
