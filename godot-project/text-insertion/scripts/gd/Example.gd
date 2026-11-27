extends Node
## Minimal example of Text Insertion: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Text-Insertion

const TEXT_VALUES := "res://text-insertion/resources/text_values.tres"

var player_name := "":
	set(value):
		player_name = value
		if not TextInsertionServer.has_provider(&"player_name"):
			# From now on {player_name} comes from here.
			TextInsertionServer.register_provider(&"player_name", func(): return player_name)
		TextInsertionServer.notify_changed(&"player_name")

var _template: TextInsertionTemplate = TextInsertionServer.parse("Greetings, {player_name}!")
var _greeting_label := Label.new()


func _ready() -> void:
	# The values resource. The guide lists it under Project Settings > Default Values; this project adds it from code.
	var text_values: TextInsertionValues = load(TEXT_VALUES)
	TextInsertionServer.add_values(text_values)

	# Using the API.
	print(TextInsertionServer.insert("Welcome to {game_name}, {player_name}!"))
	print(TextInsertionServer.insert("{count} enemies left", { "count": 3 }))
	TextInsertionServer.register_provider(&"player_name", func(): return "Mara")
	print(TextInsertionServer.insert("Greetings, {player_name:upper}!"))
	var template: TextInsertionTemplate = TextInsertionServer.parse("Level cap: {max_level:number}")
	print(TextInsertionServer.insert_template(template))
	print(template.has_name(&"max_level"))
	TextInsertionServer.unregister_provider(&"player_name")

	# A greeting with the player's name: PlayerProfile and NameGreeting of the guide live in this script.
	add_child(_greeting_label)
	TextInsertionServer.value_changed.connect(_on_value_changed)
	TextInsertionServer.values_changed.connect(_refresh)
	_refresh()
	print(_greeting_label.text)
	player_name = "Mara"  # What text_submitted of the LineEdit does.
	print(_greeting_label.text)

	# Modifiers of your own.
	TextInsertionServer.register_modifier(&"roman", func(value): return to_roman(int(value)))
	print(TextInsertionServer.insert("Chapter {chapter:roman}", { "chapter": 4 }))

	# RichTextLabel.
	print(TextInsertionServer.insert("[b]{player_name}[/b]", { "player_name": "[i]Mara" }, TextInsertionServer.ESCAPE_BBCODE))

	# The server keeps the callables of this script: take them away before it is freed.
	TextInsertionServer.unregister_provider(&"player_name")
	TextInsertionServer.unregister_modifier(&"roman")
	get_tree().quit()


func to_roman(number: int) -> String:
	var numerals: Array[String] = ["", "I", "II", "III", "IV", "V"]
	return numerals[number]


func _on_value_changed(value_name: StringName) -> void:
	# Only names this text uses.
	if _template.has_name(value_name):
		_refresh()


func _refresh() -> void:
	_greeting_label.text = TextInsertionServer.insert_template(_template)
