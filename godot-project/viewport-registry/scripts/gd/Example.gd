extends Node
## Minimal example of Viewport Registry: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Viewport-Registry


func _ready() -> void:
	# Listing viewports and windows.
	set_msaa_everywhere(Viewport.MSAA_2X)
	print_windows()

	# From an RID or a window ID back to the node.
	var rid: RID = get_viewport().get_viewport_rid()
	var viewport: Viewport = ViewportServer.get_viewport_by_rid(rid)
	if viewport:
		print(viewport.get_path())
	for window_id in DisplayServer.get_window_list():
		var window: Window = ViewportServer.get_window_by_id(window_id)
		if window:
			print(window_id, " -> ", window.title)

	# Reacting to new viewports.
	ViewportServer.viewport_added.connect(_on_viewport_added)
	# The viewports that are there already.
	for existing in ViewportServer.get_viewports():
		_on_viewport_added(existing)
	add_child(SubViewport.new())

	# Graphics settings for every viewport.
	# Split screen: every SubViewport gets the player's anti-aliasing and texture settings.
	RenderQualitySettingsServer.viewport_target = SettingsServer.VIEWPORT_TARGET_ALL_VIEWPORTS
	TextureSettingsServer.viewport_target = SettingsServer.VIEWPORT_TARGET_ALL_VIEWPORTS
	# Shadows only for the root and the viewports in the group.
	ShadowSettingsServer.viewport_target = SettingsServer.VIEWPORT_TARGET_GROUP
	get_tree().quit()


func set_msaa_everywhere(msaa: Viewport.MSAA) -> void:
	for viewport in ViewportServer.get_viewports():
		viewport.msaa_3d = msaa


func print_windows() -> void:
	for window in ViewportServer.get_windows():
		print(window.title, ": ", window.size)


func _on_viewport_added(viewport: Viewport) -> void:
	viewport.msaa_3d = Viewport.MSAA_4X
	print("Viewport added: ", viewport)
