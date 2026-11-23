class_name Issue15CheckSettings
extends Resource
## Settings resource of the GDScript check domain "check15_gd": two values.

@export_range(0.0, 1.0, 0.01) var volume: float = 0.5
@export_enum("Low", "Medium", "High") var quality: int = 1
