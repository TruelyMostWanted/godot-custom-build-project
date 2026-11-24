class_name Issue36GdTomlSettings
extends Resource
## Settings with nested resources and arrays for 36-feature-toml-module. The property order is checked in the file.

@export var volume: float = 0.5
@export var title: String = "Default"
@export var main: Issue36GdTomlEntry
@export var spare: Issue36GdTomlEntry
@export var entries: Array[Issue36GdTomlEntry] = []
@export var extra: Dictionary = {}
@export var numbers: PackedInt32Array = PackedInt32Array()
@export var tint: Color = Color.WHITE
