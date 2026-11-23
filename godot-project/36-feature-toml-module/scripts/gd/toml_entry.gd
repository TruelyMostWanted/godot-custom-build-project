class_name Issue36GdTomlEntry
extends Resource
## One record with an optional child for 36-feature-toml-module.

enum Mode { CALM, ANGRY = 5, HAPPY }

@export var name: String = "Entry"
@export var level: int = 1
@export var speed: float = 1.0
@export var mode: Mode = Mode.CALM
@export var offset: Vector2 = Vector2.ZERO
@export var child: Issue36GdTomlEntry
