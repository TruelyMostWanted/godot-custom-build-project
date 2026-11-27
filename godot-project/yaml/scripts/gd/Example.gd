extends Node
## Minimal example of YAML: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/YAML
## In the editor: double-click enemies.yaml for live errors, select it once for the data tree (no code).

const STATS: YAML = preload("res://yaml/resources/enemies.yaml")

@export var enemy_name := "Slime"
var hp := 0
var speed := 0.0


func _ready() -> void:
	# Using the API
	var enemies := load("res://yaml/resources/enemies.yaml") as YAML
	print(enemies.get_value("enemies/0/name"))
	print(enemies.get_value("enemies/1/flying", false))

	var mine := YAML.new()
	mine.parse(enemies.encode_to_text())
	mine.set_value("enemies/2", { "name": "Ghost", "hp": 20 })
	mine.save("user://enemies.yaml")

	# Enemy stats from the file: the guide's Enemy is a CharacterBody2D, here this node reads the stats.
	enemy_name = "Bat"
	_read_stats()
	print(enemy_name, ": ", hp, " hp, speed ", speed)

	# Save your own resource as YAML
	var settings := _make_settings()
	ResourceSaver.save(settings, "user://settings.yaml")
	var loaded := load("user://settings.yaml") as GameSettings
	var text := YAML.serialize(settings)
	print(loaded.title)
	print(text)
	get_tree().quit()


func _read_stats() -> void:
	for stats: Dictionary in STATS.data["enemies"]:
		if stats["name"] == enemy_name:
			hp = stats["hp"]
			speed = stats["speed"]
			return
	push_error("No stats for %s in enemies.yaml" % enemy_name)


## The GameSettings of the guide's file.
func _make_settings() -> GameSettings:
	var settings := GameSettings.new()
	settings.title = "Space Trader"
	settings.quality = GameSettings.Quality.High
	settings.resolution = Vector2i(1920, 1080)
	settings.fullscreen = true
	settings.audio = AudioSettings.new()
	var ann := PlayerProfile.new()
	ann.name = "Ann"
	ann.level = 12
	settings.profiles = [ann]
	return settings
