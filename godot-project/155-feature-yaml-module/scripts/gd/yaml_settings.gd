class_name Issue155GdYamlSettings
extends Resource
## Settings with nested resources and arrays for 155-feature-yaml-module. The property order is checked in the file.

@export var volume: float = 0.5
@export var title: String = "Default"
@export var main: Issue155GdYamlEntry
@export var spare: Issue155GdYamlEntry
@export var entries: Array[Issue155GdYamlEntry] = []
@export var extra: Dictionary = {}
@export var numbers: PackedInt32Array = PackedInt32Array()
@export var tint: Color = Color.WHITE
