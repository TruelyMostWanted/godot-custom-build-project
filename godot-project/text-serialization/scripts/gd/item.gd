class_name Item
extends Resource
## The Item of the wiki guide Text Serialization ("Compare the formats"). Flat, so a list of items fits CSV.

enum Rarity { Common, Uncommon, Rare, Epic }

@export var name := ""
@export var count := 1
@export var weight := 0.5
@export var rarity := Rarity.Common
@export var offset := Vector2.ZERO
@export var code := ""
