class_name Issue68GdInventory
extends Resource
## A nested resource for the text formats of 68-feature-xml-and-unified-serializer.

@export var owner_name: String = ""
@export var gold: int = 0
@export var tint: Color = Color.WHITE
@export var main_hand: Issue68GdItem
@export var items: Array[Issue68GdItem] = []
@export var scores: Dictionary[String, int] = {}
@export var notes: Dictionary = {}
@export var anything: Variant
