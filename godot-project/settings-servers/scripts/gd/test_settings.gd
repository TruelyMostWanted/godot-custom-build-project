class_name SettingsServersTestSettings
extends Resource
## Settings resource of the domain "test_gd": a float with a range hint and an enum.

@export_range(0.0, 1.0, 0.01) var volume: float = 0.5
@export_enum("Low", "Medium", "High") var quality: int = 1
@export var label: String = ""
@export var hidden: bool = false
