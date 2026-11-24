class_name Issue05GdAudioBus
extends Resource
## One flat record for the text formats of 05-feature-resource-text-formats (fits CSV).

enum Mode { STEREO, SURROUND, MONO = 7 }

@export var name: String = "Bus"
@export var volume_db: float = 0.0
@export var muted: bool = false
@export var mode: Mode = Mode.STEREO
@export var pan: Vector2 = Vector2.ZERO
