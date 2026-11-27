extends Node
## Minimal example of JSON: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/JSON
## In the editor: double-click enemies.json for live errors, select it once for the data tree (no code).


func _ready() -> void:
	# Using the API
	var file := load("res://json/resources/enemies.json") as JSON
	for enemy: Dictionary in file.data["enemies"]:
		print(enemy["name"], " ", int(enemy["hp"]))

	var json := JSON.new()
	if json.parse('{"name": ') != OK:
		print(json.get_error_line(), ": ", json.get_error_message())

	# Load enemies from a web service: the body of a request, here the shipped file.
	var body: PackedByteArray = FileAccess.get_file_as_bytes("res://json/resources/enemies.json")
	_on_request_completed(HTTPRequest.RESULT_SUCCESS, 200, PackedStringArray(), body)

	# Save your own resource as JSON
	var sword := Item.new()
	sword.name = "Sword"
	ResourceSaver.save(sword, "user://sword.json")
	var loaded := load("user://sword.json") as Item
	var text := JSON.serialize(sword)
	var copy := JSON.deserialize(text) as Item
	print(loaded.name, " ", copy.name)
	print(text)
	get_tree().quit()


func _on_request_completed(_result: int, code: int, _headers: PackedStringArray, body: PackedByteArray) -> void:
	var data: Variant = JSON.parse_string(body.get_string_from_utf8())
	if code != 200 or data == null:
		return
	var schema := load("res://json/resources/enemies_schema.tres") as DataSchema
	var check := schema.validate(data)
	if not check.is_valid():
		push_warning(check.get_issue_message(0))
		return
	spawn_enemies(data["enemies"])


func spawn_enemies(enemies: Array) -> void:
	print("Spawned ", enemies.size(), " enemies")
