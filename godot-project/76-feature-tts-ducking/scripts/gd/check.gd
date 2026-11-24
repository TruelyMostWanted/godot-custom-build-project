extends FeatureCheck
## GDScript checks of 76-feature-tts-ducking. Check the same things as scripts/cs/Issue76FeatureTtsDuckingCheck.cs.
## The utterance events are simulated through DisplayServer (like the OS callbacks), so no speech engine is needed.
## Real speech is only checked when TTSServer.is_available() and the system has voices.

var _ducking_events: Array[String] = []


func _get_check_name() -> String:
	return "76-feature-tts-ducking"


## A silent 16-bit mono WAV of the given length.
func _make_wav(seconds: float) -> AudioStreamWAV:
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = 44100
	wav.stereo = false
	var data := PackedByteArray()
	data.resize(int(seconds * 44100.0) * 2)
	wav.data = data
	return wav


func _wait(seconds: float) -> void:
	await get_tree().create_timer(seconds).timeout


func _frames(count: int) -> void:
	for i in count:
		await get_tree().process_frame


## Simulates the operating system: the event goes through DisplayServer and TTSServer (deferred to the main thread).
func _post(event: DisplayServer.TTSUtteranceEvent, id: int) -> void:
	DisplayServer.call("_tts_post_utterance_event", event, id, 0)


func _near(value: float, expected: float) -> bool:
	return absf(value - expected) < 0.01


func _on_ducking_changed(channel: StringName, ducked: bool) -> void:
	_ducking_events.append("%s:%s" % [channel, ducked])


func _run_checks() -> void:
	# 1. Settings on AudioMixerChannel.
	var defaults := AudioMixerChannel.new()
	expect(not defaults.duck_on_tts, "duck_on_tts is false by default")
	expect(_near(defaults.duck_volume_db, -12.0), "duck_volume_db is -12 dB by default")
	expect(_near(defaults.duck_fade_time, 0.25), "duck_fade_time is 0.25 s by default")
	defaults.duck_volume_db = 6.0
	expect(_near(defaults.duck_volume_db, 0.0), "duck_volume_db is clamped to 0 dB (ducking only lowers)")

	var music := AudioMixerChannel.new()
	music.channel_name = &"music"
	music.duck_on_tts = true
	music.duck_volume_db = -12.0
	music.duck_fade_time = 0.2
	var sfx := AudioMixerChannel.new()
	sfx.channel_name = &"sfx"
	var layout := AudioMixerLayout.new()
	layout.add_channel(music)
	layout.add_channel(sfx)
	AudioMixerServer.apply_layout(layout)
	AudioMixerServer.set_channel_volume_db(&"music", -3.0)
	AudioMixerServer.play(&"music", _make_wav(30.0))
	AudioMixerServer.play(&"sfx", _make_wav(30.0))
	AudioMixerServer.ducking_changed.connect(_on_ducking_changed)
	expect(AudioMixerServer.is_ducking_enabled(), "ducking is enabled by default")
	expect(AudioMixerServer.get_active_utterance_count() == 0, "no active utterances at the start")

	# 2. Music drops by duck_volume_db while speech runs.
	_post(DisplayServer.TTS_UTTERANCE_STARTED, 7601)
	await _frames(2)
	expect(AudioMixerServer.get_active_utterance_count() == 1, "utterance_started counts one utterance")
	expect(AudioMixerServer.is_ducked(&"music"), "music (duck_on_tts) is ducked while speaking")
	expect(not AudioMixerServer.is_ducked(&"sfx"), "sfx (duck_on_tts = false) is not ducked")
	await _wait(0.4)
	expect(_near(AudioMixerServer.get_duck_volume_db(&"music"), -12.0), "music fades down by 12 dB (got %f)" % AudioMixerServer.get_duck_volume_db(&"music"))
	expect(_near(AudioMixerServer.get_channel_volume_db(&"music"), -3.0), "the channel volume itself stays -3 dB")
	expect_equal(_ducking_events, ["music:true"], "ducking_changed(music, true) is emitted once")

	# 3. Overlapping utterances: ducked until the last one ends; stop() restores.
	_post(DisplayServer.TTS_UTTERANCE_STARTED, 7602)
	_post(DisplayServer.TTS_UTTERANCE_ENDED, 7601)
	await _frames(2)
	expect(AudioMixerServer.is_ducked(&"music"), "music stays ducked while a second utterance runs")
	TTSServer.stop()
	_post(DisplayServer.TTS_UTTERANCE_CANCELED, 7602) # The platform cancels the utterance on stop().
	await _frames(2)
	expect(not AudioMixerServer.is_ducked(&"music"), "TTSServer.stop() during speech ends the ducking")
	await _wait(0.4)
	expect(_near(AudioMixerServer.get_duck_volume_db(&"music"), 0.0), "music returns to its volume after speech")
	expect(AudioMixerServer.is_playing(&"music"), "music keeps playing")
	expect_equal(_ducking_events, ["music:true", "music:false"], "ducking_changed(music, false) is emitted at the end")

	# 4. A fade_out() during ducking ends at silence and does not jump back up.
	_post(DisplayServer.TTS_UTTERANCE_STARTED, 7603)
	await _wait(0.3)
	AudioMixerServer.fade_out(&"music", 0.3)
	_post(DisplayServer.TTS_UTTERANCE_ENDED, 7603)
	await _wait(0.6)
	expect(AudioMixerServer.get_voice_count(&"music") == 0, "the faded-out voice is stopped")
	expect(not AudioMixerServer.is_playing(&"music"), "music stays silent after the speech ended")

	# 5. Global switch and manual ducking.
	AudioMixerServer.play(&"music", _make_wav(30.0))
	AudioMixerServer.set_ducking_enabled(false)
	_post(DisplayServer.TTS_UTTERANCE_STARTED, 7604)
	await _frames(2)
	expect(not AudioMixerServer.is_ducked(&"music"), "no ducking while set_ducking_enabled(false)")
	AudioMixerServer.set_ducking_enabled(true)
	expect(AudioMixerServer.is_ducked(&"music"), "enabling during speech ducks at once")
	_post(DisplayServer.TTS_UTTERANCE_ENDED, 7604)
	await _frames(2)
	expect(not AudioMixerServer.is_ducked(&"music"), "restored after the utterance ended")
	AudioMixerServer.duck(&"sfx", -6.0, 0.0)
	expect(AudioMixerServer.is_ducked(&"sfx"), "duck() ducks any channel")
	expect(_near(AudioMixerServer.get_duck_volume_db(&"sfx"), -6.0), "duck() with fade 0 applies at once")
	AudioMixerServer.unduck(&"sfx", 0.0)
	expect(not AudioMixerServer.is_ducked(&"sfx"), "unduck() restores the channel")

	# 6. Real speech, if available.
	if TTSServer.is_available() and not TTSServer.get_voices().is_empty():
		var id := TTSServer.speak("Ducking check.")
		for i in 60: # At most 3 seconds until the speech starts.
			if AudioMixerServer.is_ducked(&"music"):
				break
			await _wait(0.05)
		expect(AudioMixerServer.is_ducked(&"music"), "real speech (utterance %d) ducks music" % id)
		for i in 200: # At most 10 seconds until it ends.
			if not AudioMixerServer.is_ducked(&"music"):
				break
			await _wait(0.05)
		expect(not AudioMixerServer.is_ducked(&"music"), "music is restored after real speech")
	else:
		print("  (skipped: text to speech is not available, real speech not checked)")

	AudioMixerServer.ducking_changed.disconnect(_on_ducking_changed)
	AudioMixerServer.set_ducking_enabled(true)
	AudioMixerServer.stop_all()
	AudioMixerServer.apply_layout(AudioMixerLayout.new())
