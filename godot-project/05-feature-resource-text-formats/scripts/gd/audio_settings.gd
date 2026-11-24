class_name Issue05GdAudioSettings
extends Resource
## Settings with nested resources for the text formats of 05-feature-resource-text-formats (does not fit CSV).

enum Quality { LOW, MEDIUM, HIGH }

@export var master_volume: float = 1.0
@export var title: String = "Audio"
@export var quality: Quality = Quality.MEDIUM
@export var tint: Color = Color.WHITE
@export var main_bus: Issue05GdAudioBus
@export var buses: Array[Issue05GdAudioBus] = []
@export var tags: PackedStringArray = PackedStringArray()
@export var cell: Vector3i = Vector3i.ZERO
