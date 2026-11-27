class_name GameSettings
extends Resource
## The GameSettings of the wiki guides TOML and YAML: an enum, a Vector2i, a nested resource and an array of resources.

enum Quality { Low, Medium, High }

@export var title := ""
@export var quality := Quality.Medium
@export var resolution := Vector2i(1280, 720)
@export var fullscreen := false
@export var audio: AudioSettings
@export var profiles: Array[PlayerProfile] = []
