extends Node
## Minimal example of Localized Text: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Localized-Text

const HUD_CSV := "res://localized-text/resources/hud.csv"

var enemies_alive: Array[String] = ["wolf", "bear"]


func _ready() -> void:
	# The texts. The guide lists hud.csv in a catalog manifest; this project adds it from code.
	var catalog := TextLocalizationCatalog.new()
	catalog.load_from_file(HUD_CSV)
	TextLocalizationServer.add_catalog(catalog)
	TranslationServer.set_locale("en")

	# The file: SceneGreetingLabel in this scene has the metadata localized_text.
	await _settle()
	print($SceneGreetingLabel.text)

	# Bind and set values.
	var greeting := LocalizedText.new()
	greeting.key = &"NPC_GREETING"
	greeting.context = &"casual"
	greeting.bind($GreetingLabel)
	TextInsertionServer.set_value(&"player_name", "Ada")
	await _settle()
	print($GreetingLabel.text)
	greeting.args = { &"player_name": "Lin" }
	print(greeting.get_text())
	greeting.unbind($GreetingLabel)

	# Plurals.
	var enemies := LocalizedText.new()
	enemies.key = &"ENEMIES_LEFT"
	enemies.count_argument = &"enemy.count"
	enemies.bind($EnemiesLabel)
	TextInsertionServer.set_value(&"enemy.count", 3)
	await _settle()
	print($EnemiesLabel.text)
	TextInsertionServer.set_value(&"enemy.count", 1)
	await _settle()
	print($EnemiesLabel.text)

	# Values that change on their own.
	TextInsertionServer.register_provider(&"enemy.count", func(): return enemies_alive.size())
	enemies.refresh_mode = LocalizedText.REFRESH_EVERY_FRAME
	await _settle()
	print($EnemiesLabel.text)

	# Other properties and channels.
	var tip := LocalizedText.new()
	tip.key = &"NPC_GREETING_TIP"
	tip.bind($GreetingLabel, &"tooltip_text")
	var line := LocalizedText.new()
	line.key = &"CUTSCENE_01"
	line.channel = &"subtitles"
	line.bind($SubtitleLabel)
	TranslationServer.set_locale_for(&"subtitles", "ja")
	await _settle()
	print($GreetingLabel.tooltip_text)
	print($SubtitleLabel.text)

	# A complete example: a HUD. The same bindings as above, driven by buttons: not repeated here.

	# The server keeps the callable of this script: take it away before it is freed.
	TextInsertionServer.unregister_provider(&"enemy.count")
	get_tree().quit()


func _settle() -> void:
	# Refreshes run at the end of the frame.
	await get_tree().process_frame
	await get_tree().process_frame
