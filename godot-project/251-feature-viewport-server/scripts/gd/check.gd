extends FeatureCheck
## GDScript checks of 251-feature-viewport-server. Check the same things as scripts/cs/Issue251FeatureViewportServerCheck.cs.

var _added: Array[Viewport] = []
var _removed: Array[Viewport] = []
# Instance IDs: a freed Window cannot be passed to Array.has().
var _removed_ids: Array[int] = []


func _get_check_name() -> String:
	return "251-feature-viewport-server"


func _on_viewport_added(viewport: Viewport) -> void:
	_added.append(viewport)


func _on_viewport_removed(viewport: Viewport) -> void:
	_removed.append(viewport)
	_removed_ids.append(viewport.get_instance_id())


func _run_checks() -> void:
	expect(Engine.has_singleton("ViewportServer"), "ViewportServer is a registered singleton")
	var root := get_tree().root

	# Initial scan: the root window and a viewport that entered the tree before the first call
	# (this runs in _ready(), before the first frame).
	var early := SubViewport.new()
	add_child(early)
	var viewports: Array[Viewport] = ViewportServer.get_viewports()
	expect(viewports.size() >= 2, "get_viewports() lists at least the root and the early viewport")
	expect(viewports.size() > 0 and viewports[0] == root, "the root window comes first")
	expect(viewports.has(early), "get_viewports() has the viewport added before the first call")
	expect_equal(ViewportServer.get_viewport_count(), viewports.size(), "get_viewport_count() matches get_viewports()")
	expect(ViewportServer.has_viewport(root), "has_viewport(root)")
	expect(ViewportServer.has_viewport(early), "has_viewport(early)")

	var windows: Array[Window] = ViewportServer.get_windows()
	expect(windows.has(root), "get_windows() has the root window")
	expect(not windows.any(func(w: Window) -> bool: return w == early), "get_windows() has no SubViewport")

	# Lookups.
	expect(ViewportServer.get_viewport_by_rid(early.get_viewport_rid()) == early, "get_viewport_by_rid() finds the SubViewport")
	expect(ViewportServer.get_viewport_by_rid(root.get_viewport_rid()) == root, "get_viewport_by_rid() finds the root")
	expect(ViewportServer.get_viewport_by_rid(RID()) == null, "get_viewport_by_rid(RID()) is null")
	expect(ViewportServer.get_window_by_id(DisplayServer.MAIN_WINDOW_ID) == root, "get_window_by_id(MAIN_WINDOW_ID) is the root")
	expect(ViewportServer.get_window_by_id(DisplayServer.INVALID_WINDOW_ID) == null, "get_window_by_id(INVALID_WINDOW_ID) is null")
	expect(ViewportServer.get_window_by_id(123456) == null, "get_window_by_id() of an unknown ID is null")

	# Signals.
	ViewportServer.viewport_added.connect(_on_viewport_added)
	ViewportServer.viewport_removed.connect(_on_viewport_removed)
	var count := ViewportServer.get_viewport_count()

	var holder := Node.new()
	var nested := SubViewport.new()
	holder.add_child(nested)
	add_child(holder)
	expect(_added.size() == 1 and _added[0] == nested, "viewport_added for a viewport inside an added subtree")
	expect_equal(ViewportServer.get_viewport_count(), count + 1, "count grows by one")

	var plain := Node.new()
	add_child(plain)
	plain.free()
	expect_equal(_added.size(), 1, "no viewport_added for a plain Node")
	expect_equal(_removed.size(), 0, "no viewport_removed for a plain Node")

	var window := Window.new()
	add_child(window)
	expect(_added.has(window), "viewport_added for a Window")
	expect(ViewportServer.get_windows().has(window), "get_windows() has the new Window")
	expect(ViewportServer.get_viewport_by_rid(window.get_viewport_rid()) == window, "get_viewport_by_rid() finds the Window")
	# The headless display server gives every native window ID 0, the ID of the root.
	if not window.is_embedded() and window.get_window_id() > DisplayServer.MAIN_WINDOW_ID:
		expect(ViewportServer.get_window_by_id(window.get_window_id()) == window, "get_window_by_id() finds the native Window")

	remove_child(holder)
	expect(_removed.size() == 1 and _removed[0] == nested, "viewport_removed when the subtree leaves the tree")
	expect(not ViewportServer.has_viewport(nested), "has_viewport() is false after removal")
	holder.free()

	var window_id := window.get_instance_id()
	window.free()
	expect(_removed_ids.has(window_id), "viewport_removed when the Window is freed")
	early.free()
	expect_equal(ViewportServer.get_viewport_count(), count - 1, "count after freeing the Window and the early viewport")

	ViewportServer.viewport_added.disconnect(_on_viewport_added)
	ViewportServer.viewport_removed.disconnect(_on_viewport_removed)

	# Part 2: the graphics settings servers can target every registered viewport.
	expect_equal(TextureSettingsServer.viewport_target, SettingsServer.VIEWPORT_TARGET_GROUP, "TextureSettingsServer.viewport_target defaults to VIEWPORT_TARGET_GROUP")
	expect_equal(RenderQualitySettingsServer.viewport_target, SettingsServer.VIEWPORT_TARGET_GROUP, "RenderQualitySettingsServer.viewport_target defaults to VIEWPORT_TARGET_GROUP")
	expect_equal(ShadowSettingsServer.viewport_target, SettingsServer.VIEWPORT_TARGET_GROUP, "ShadowSettingsServer.viewport_target defaults to VIEWPORT_TARGET_GROUP")
	var snap: bool = not root.snap_2d_transforms_to_pixel
	var outside := SubViewport.new()
	outside.snap_2d_transforms_to_pixel = not snap
	add_child(outside)
	# A session override is not stored in the player's settings file.
	TextureSettingsServer.set_session_override(&"snap_2d_transforms_to_pixel", snap)
	expect_equal(root.snap_2d_transforms_to_pixel, snap, "the root gets the value")
	expect_equal(outside.snap_2d_transforms_to_pixel, not snap, "VIEWPORT_TARGET_GROUP: a viewport outside the group keeps its value")
	TextureSettingsServer.viewport_target = SettingsServer.VIEWPORT_TARGET_ALL_VIEWPORTS
	expect_equal(TextureSettingsServer.viewport_target, SettingsServer.VIEWPORT_TARGET_ALL_VIEWPORTS, "viewport_target is VIEWPORT_TARGET_ALL_VIEWPORTS")
	expect_equal(outside.snap_2d_transforms_to_pixel, snap, "VIEWPORT_TARGET_ALL_VIEWPORTS: every registered viewport gets the value at once")
	var late := SubViewport.new()
	late.snap_2d_transforms_to_pixel = not snap
	add_child(late)
	expect_equal(late.snap_2d_transforms_to_pixel, snap, "VIEWPORT_TARGET_ALL_VIEWPORTS: a viewport that enters the tree later gets the value")
	TextureSettingsServer.viewport_target = SettingsServer.VIEWPORT_TARGET_ROOT_ONLY
	TextureSettingsServer.clear_session_override(&"snap_2d_transforms_to_pixel")
	expect_equal(root.snap_2d_transforms_to_pixel, not snap, "the root gets the stored value back")
	expect_equal(late.snap_2d_transforms_to_pixel, snap, "VIEWPORT_TARGET_ROOT_ONLY: the other viewports keep their values")
	TextureSettingsServer.viewport_target = SettingsServer.VIEWPORT_TARGET_GROUP
	late.free()
	outside.free()
