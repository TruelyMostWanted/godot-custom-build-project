extends Node
## Minimal example of Process Memory: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Process-Memory


func _ready() -> void:
	# Show the memory of the game (the guide fills a Label from a Timer; the example prints the text).
	var text: String = "Memory: " + String.humanize_size(OS.get_process_memory_usage())
	print(text)

	# Add memory to the debugger monitors.
	Performance.add_custom_monitor("game/process_memory_mib", func() -> float:
		return OS.get_process_memory_usage() / 1048576.0)
	print("Monitor: ", Performance.get_custom_monitor("game/process_memory_mib"), " MiB")

	# Print the details.
	var info: Dictionary = OS.get_process_memory_info()
	for key: String in info:
		var value: int = info[key]
		if value == -1:
			print(key, ": unknown")
		else:
			print(key, ": ", String.humanize_size(value))
	get_tree().quit()
