class_name Issue68GdItem
extends Resource
## One item for the text formats of 68-feature-xml-and-unified-serializer (flat, so a list of items fits CSV).

enum Rarity { COMMON, RARE, EPIC = 5 }

@export var name: String = ""
@export var count: int = 1
@export var weight: float = 1.0
@export var rarity: Rarity = Rarity.COMMON
@export var offset: Vector2 = Vector2.ZERO
@export var code: String = ""
