extends FeatureCheck
## GDScript checks of 239-feature-csharp-children-of-type. Check the same things as scripts/cs/Issue239FeatureCsharpChildrenOfTypeCheck.cs.
## GDScript has no generic methods: find_children(pattern, type, recursive, owned) is the way, with a class name
## (native or class_name). These checks show that it gives the same results as FindChildren<T>() in C#.


func _get_check_name() -> String:
	return "239-feature-csharp-children-of-type"


# Root
#   A (Button, owned)
#   B (Node2D, owned)
#     C (Button, owned)
#     D (Node2D, owned)
#   E (Node, not owned, like the root of an instanced scene)
#     F (Button, not owned)
func _build_tree() -> Node:
	var root := Node.new()
	root.name = "Root"
	var add := func(parent: Node, child: Node, child_name: String, owned: bool) -> Node:
		child.name = child_name
		parent.add_child(child)
		if owned:
			child.owner = root
		return child
	add.call(root, Button.new(), "A", true)
	var b: Node = add.call(root, Node2D.new(), "B", true)
	add.call(b, Button.new(), "C", true)
	add.call(b, Node2D.new(), "D", true)
	var e: Node = add.call(root, Node.new(), "E", false)
	add.call(e, Button.new(), "F", false)
	return root


func _names(nodes: Array) -> String:
	return ",".join(nodes.map(func(n: Node) -> String: return n.name))


func _run_checks() -> void:
	var root := _build_tree()
	expect_equal(_names(root.find_children("*", "Button", false, false)), "A", "direct children only")
	expect_equal(_names(root.find_children("*", "Button", true, false)), "A,C,F", "all descendants in tree order, instanced ones included")
	expect_equal(_names(root.find_children("*", "Button")), "A,C", "the default owned = true skips nodes without owner")
	expect_equal(_names(root.find_children("*", "Node2D", true, false)), "B,D", "inheritance")
	expect_equal(_names(root.find_children("*", "CanvasItem", true, false)), "A,B,C,D,F", "every CanvasItem")
	expect(root.find_children("*", "Camera3D", true, false).is_empty(), "no match is an empty array")
	root.free()
