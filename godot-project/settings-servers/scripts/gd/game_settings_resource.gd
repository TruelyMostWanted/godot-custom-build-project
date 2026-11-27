class_name GameSettingsResource
extends Resource

@export_range(0.0, 1.0, 0.01) var music_volume: float = 0.8
@export_enum("Easy", "Normal", "Hard") var difficulty: int = 1
@export var show_hints: bool = true
@export var max_fps: int = 0
