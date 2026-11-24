class_name Issue155GdYamlEntry
extends Resource
## One record with an optional child for 155-feature-yaml-module.

enum Mode { CALM, ANGRY = 5, HAPPY }

@export var name: String = "Entry"
@export var level: int = 1
@export var speed: float = 1.0
@export var mode: Mode = Mode.CALM
@export var offset: Vector2 = Vector2.ZERO
@export var child: Issue155GdYamlEntry
