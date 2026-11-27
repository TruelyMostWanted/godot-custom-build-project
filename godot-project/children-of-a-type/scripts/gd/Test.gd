extends FeatureTest
## Test of Children of a Type. The generic methods are C#-only (scripts/cs/Test.cs); this tests find_children(),
## which gives the same results for native classes.

# Root
#   A (Button, owned)
#   B (Node2D, owned)
#     C (Button, owned)
#     D (Node2D, owned)
#   E (Node, not owned, like the root of an instanced scene)
#     F (Button, not owned)
const DIRECT_BUTTONS := "A"
const ALL_BUTTONS := "A,C,F"
const OWNED_BUTTONS := "A,C"
const ALL_NODE2D := "B,D"
const ALL_CANVAS_ITEMS := "A,B,C,D,F"

var _root: Node


func _run_tests() -> void:
	_root = _build_tree()
	_test_direct_children()
	_test_all_descendants()
	_test_owned()
	_test_inheritance()
	_test_no_match()
	_root.free()


func _build_tree() -> Node:
	var root := Node.new()
	root.name = "Root"
	var a := Button.new()
	a.name = "A"
	var b := Node2D.new()
	b.name = "B"
	var c := Button.new()
	c.name = "C"
	var d := Node2D.new()
	d.name = "D"
	var e := Node.new()
	e.name = "E"
	var f := Button.new()
	f.name = "F"
	root.add_child(a)
	root.add_child(b)
	b.add_child(c)
	b.add_child(d)
	root.add_child(e)
	e.add_child(f)
	a.owner = root
	b.owner = root
	c.owner = root
	d.owner = root
	return root


func _names(nodes: Array[Node]) -> String:
	var names: PackedStringArray = []
	for node in nodes:
		names.append(node.name)
	return ",".join(names)


func _test_direct_children() -> void:
	var found: Array[Node] = _root.find_children("*", "Button", false, false)
	expect_equal(_names(found), DIRECT_BUTTONS, "recursive = false: direct children only")


func _test_all_descendants() -> void:
	var found: Array[Node] = _root.find_children("*", "Button", true, false)
	expect_equal(_names(found), ALL_BUTTONS, "all descendants in tree order, instanced ones included")


func _test_owned() -> void:
	var found: Array[Node] = _root.find_children("*", "Button")
	expect_equal(_names(found), OWNED_BUTTONS, "the default owned = true skips nodes without owner")


func _test_inheritance() -> void:
	var node2d: Array[Node] = _root.find_children("*", "Node2D", true, false)
	expect_equal(_names(node2d), ALL_NODE2D, "inheritance: Node2D")
	var canvas_items: Array[Node] = _root.find_children("*", "CanvasItem", true, false)
	expect_equal(_names(canvas_items), ALL_CANVAS_ITEMS, "every CanvasItem")


func _test_no_match() -> void:
	var found: Array[Node] = _root.find_children("*", "Camera3D", true, false)
	expect(found.is_empty(), "no match is an empty array")
