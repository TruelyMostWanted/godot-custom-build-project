class_name Inventory
extends Resource
## Every kind of value the text formats write: nested and missing resources, typed and untyped containers, Godot types.

@export var owner_name := ""
@export var gold := 0
@export var tint := Color.WHITE
@export var cell := Vector3i.ZERO
@export var tags := PackedStringArray()
@export var main_hand: Item
@export var spare: Item
@export var items: Array[Item] = []
@export var scores: Dictionary[String, int] = {}
@export var notes: Dictionary = {}
@export var anything: Variant
