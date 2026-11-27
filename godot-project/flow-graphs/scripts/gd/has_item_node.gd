@tool
class_name HasItemNode
extends FlowNode
## Node type of the guide section "Write your own node type". _get_inline_properties() and _get_translatable_strings()
## are not in the guide snippet; Test.gd uses them.

@export var item: StringName:
	set(value):
		item = value
		notify_ports_changed()


func _get_input_ports() -> Array[FlowPort]:
	return [FlowPort.create_flow("in")]


func _get_output_ports() -> Array[FlowPort]:
	return [FlowPort.create_flow("yes"), FlowPort.create_flow("no")]


func _get_category() -> String:
	return "Inventory"


func _get_configuration_warnings() -> PackedStringArray:
	return PackedStringArray(["No item is set."]) if item.is_empty() else PackedStringArray()


func _get_inline_properties() -> PackedStringArray:
	return PackedStringArray(["item"])


func _get_translatable_strings() -> Array[PackedStringArray]:
	return [PackedStringArray(["Do you have the %s?" % item])]
