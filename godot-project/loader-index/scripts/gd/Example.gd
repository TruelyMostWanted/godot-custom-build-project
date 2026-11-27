extends Node
## Minimal example of Loader Index: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Loader-Index

const MyLoader := preload("res://loader-index/scripts/gd/my_loader.gd")


func _ready() -> void:
	# Write a loader that the index finds
	var my_loader := MyLoader.new()
	ResourceLoader.add_resource_format_loader(my_loader)
	print("mydata known: ", ResourceLoader.get_recognized_extensions_for_type("Resource").has("mydata"))

	# Change the extensions of a loader at runtime
	my_loader.extensions = PackedStringArray(["mydata", "mydata2"])
	ResourceLoader.rebuild_extension_index()
	print(ResourceLoader.exists("res://level.mydata2"))

	ResourceLoader.remove_resource_format_loader(my_loader)
	get_tree().quit()
