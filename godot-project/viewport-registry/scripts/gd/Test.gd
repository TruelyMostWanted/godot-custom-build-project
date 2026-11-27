extends FeatureTest
## Full API test of Viewport Registry (ViewportServer and the viewport_target of the graphics settings servers).
## Same tests as scripts/cs/Test.cs.

const UNKNOWN_WINDOW_ID := 123456

var _root: Window
var _early: SubViewport
var _added: Array[Viewport] = []
var _removed: Array[Viewport] = []
# Instance IDs: a freed Window cannot be passed to Array.has().
var _removed_ids: Array[int] = []


func _run_tests() -> void:
	_root = get_tree().root
	_test_singleton()
	_test_initial_scan()
	_test_windows()
	_test_lookups()
	_test_signals()
	_test_free_early_viewport()
	_test_viewport_target_defaults()
	_test_viewport_target_modes()


func _test_singleton() -> void:
	expect(Engine.has_singleton("ViewportServer"), "ViewportServer is a registered singleton")


# This runs in _ready(), before the first frame: the viewport enters the tree before the first call.
func _test_initial_scan() -> void:
	_early = SubViewport.new()
	add_child(_early)
	var viewports: Array[Viewport] = ViewportServer.get_viewports()
	expect(viewports.size() >= 2, "get_viewports lists at least the root and the early viewport")
	expect(viewports[0] == _root, "the root window comes first")
	expect(viewports.has(_early), "get_viewports has the viewport added before the first call")
	expect_equal(ViewportServer.get_viewport_count(), viewports.size(), "get_viewport_count matches get_viewports")
	expect(ViewportServer.has_viewport(_root), "has_viewport(root)")
	expect(ViewportServer.has_viewport(_early), "has_viewport(early)")


func _test_windows() -> void:
	var windows: Array[Window] = ViewportServer.get_windows()
	expect(windows.has(_root), "get_windows has the root window")
	expect_equal(windows[0], _root, "the root window comes first")
	expect(not windows.any(func(window: Window) -> bool: return window == _early), "get_windows has no SubViewport")


func _test_lookups() -> void:
	expect(ViewportServer.get_viewport_by_rid(_early.get_viewport_rid()) == _early, "get_viewport_by_rid finds the SubViewport")
	expect(ViewportServer.get_viewport_by_rid(_root.get_viewport_rid()) == _root, "get_viewport_by_rid finds the root")
	expect(ViewportServer.get_viewport_by_rid(RID()) == null, "get_viewport_by_rid(RID()) is null")
	expect(ViewportServer.get_window_by_id(DisplayServer.MAIN_WINDOW_ID) == _root, "get_window_by_id(MAIN_WINDOW_ID) is the root")
	expect(ViewportServer.get_window_by_id(DisplayServer.INVALID_WINDOW_ID) == null, "get_window_by_id(INVALID_WINDOW_ID) is null")
	expect(ViewportServer.get_window_by_id(UNKNOWN_WINDOW_ID) == null, "get_window_by_id of an unknown ID is null")


func _test_signals() -> void:
	ViewportServer.viewport_added.connect(_on_viewport_added)
	ViewportServer.viewport_removed.connect(_on_viewport_removed)
	var count: int = ViewportServer.get_viewport_count()

	var holder := Node.new()
	var nested := SubViewport.new()
	holder.add_child(nested)
	add_child(holder)
	expect(_added.size() == 1 and _added[0] == nested, "viewport_added for a viewport inside an added subtree")
	expect_equal(ViewportServer.get_viewport_count(), count + 1, "the count grows by one")

	var plain := Node.new()
	add_child(plain)
	plain.free()
	expect_equal(_added.size(), 1, "no viewport_added for a plain Node")
	expect_equal(_removed.size(), 0, "no viewport_removed for a plain Node")

	var window := Window.new()
	add_child(window)
	expect(_added.has(window), "viewport_added for a Window")
	expect(ViewportServer.get_windows().has(window), "get_windows has the new Window")
	expect(ViewportServer.get_viewport_by_rid(window.get_viewport_rid()) == window, "get_viewport_by_rid finds the Window")
	# The headless display server gives every native window ID 0, the ID of the root: get_window_by_id(0) is the root.
	expect(ViewportServer.get_window_by_id(window.get_window_id()) != null, "get_window_by_id finds a window for the ID of the new Window")

	remove_child(holder)
	expect(_removed.size() == 1 and _removed[0] == nested, "viewport_removed when the subtree leaves the tree")
	expect(not ViewportServer.has_viewport(nested), "has_viewport is false after removal")
	holder.free()

	var window_id: int = window.get_instance_id()
	window.free()
	expect(_removed_ids.has(window_id), "viewport_removed when the Window is freed")
	ViewportServer.viewport_added.disconnect(_on_viewport_added)
	ViewportServer.viewport_removed.disconnect(_on_viewport_removed)


func _test_free_early_viewport() -> void:
	var count: int = ViewportServer.get_viewport_count()
	_early.free()
	expect_equal(ViewportServer.get_viewport_count(), count - 1, "the count shrinks by one after freeing a viewport")


func _test_viewport_target_defaults() -> void:
	expect_equal(TextureSettingsServer.viewport_target, SettingsServer.VIEWPORT_TARGET_GROUP, "TextureSettingsServer.viewport_target defaults to VIEWPORT_TARGET_GROUP")
	expect_equal(RenderQualitySettingsServer.viewport_target, SettingsServer.VIEWPORT_TARGET_GROUP, "RenderQualitySettingsServer.viewport_target defaults to VIEWPORT_TARGET_GROUP")
	expect_equal(ShadowSettingsServer.viewport_target, SettingsServer.VIEWPORT_TARGET_GROUP, "ShadowSettingsServer.viewport_target defaults to VIEWPORT_TARGET_GROUP")


func _test_viewport_target_modes() -> void:
	var snap: bool = not _root.snap_2d_transforms_to_pixel
	var outside := SubViewport.new()
	outside.snap_2d_transforms_to_pixel = not snap
	add_child(outside)
	# A session override is not stored in the player's settings file.
	TextureSettingsServer.set_session_override(&"snap_2d_transforms_to_pixel", snap)
	expect_equal(_root.snap_2d_transforms_to_pixel, snap, "the root gets the value")
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
	expect_equal(_root.snap_2d_transforms_to_pixel, not snap, "the root gets the stored value back")
	expect_equal(late.snap_2d_transforms_to_pixel, snap, "VIEWPORT_TARGET_ROOT_ONLY: the other viewports keep their values")
	TextureSettingsServer.viewport_target = SettingsServer.VIEWPORT_TARGET_GROUP
	late.free()
	outside.free()


func _on_viewport_added(viewport: Viewport) -> void:
	_added.append(viewport)


func _on_viewport_removed(viewport: Viewport) -> void:
	_removed.append(viewport)
	_removed_ids.append(viewport.get_instance_id())
