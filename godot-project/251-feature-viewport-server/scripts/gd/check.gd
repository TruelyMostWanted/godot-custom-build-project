extends FeatureCheck
## GDScript checks of 251-feature-viewport-server. Check the same things as scripts/cs/Issue251FeatureViewportServerCheck.cs.

var _added: Array[Viewport] = []
var _removed: Array[Viewport] = []


func _get_check_name() -> String:
	return "251-feature-viewport-server"


func _on_viewport_added(viewport: Viewport) -> void:
	_added.append(viewport)


func _on_viewport_removed(viewport: Viewport) -> void:
	_removed.append(viewport)


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
	expect(not windows.has(early), "get_windows() has no SubViewport")

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

	window.free()
	expect(_removed.has(window), "viewport_removed when the Window is freed")
	early.free()
	expect_equal(ViewportServer.get_viewport_count(), count - 1, "count after freeing the Window and the early viewport")

	ViewportServer.viewport_added.disconnect(_on_viewport_added)
	ViewportServer.viewport_removed.disconnect(_on_viewport_removed)
