extends SceneTree
## Started by the test of Streaming Assets in a second process with
## --streaming-assets-path=<folder>. Prints what StreamingAssetsServer resolved, then quits.


func _init() -> void:
	print("STREAMING_LOCATION=%d" % StreamingAssetsServer.get_location())
	print("STREAMING_ROOT=%s" % StreamingAssetsServer.get_root())
	var file := FileAccess.open("streaming://hello.txt", FileAccess.READ)
	print("STREAMING_TEXT=%s" % (file.get_as_text().strip_edges() if file else ""))
	print("STREAMING_OS_PATH=%s" % StreamingAssetsServer.get_os_path("streaming://hello.txt"))
	quit()
