@tool
class_name Issue255DoubleNode
extends FlowNode
## Script node type of the 255 checks: a flow node with a typed data input "amount" and output "doubled".

@export var factor: int = 2


func _get_input_ports() -> Array[FlowPort]:
	return [FlowPort.create_flow(&"in"), FlowPort.create_data(&"amount", TYPE_INT, 1, "Amount")]


func _get_output_ports() -> Array[FlowPort]:
	return [FlowPort.create_flow(&"out"), FlowPort.create_data(&"doubled", TYPE_INT)]


func _get_category() -> String:
	return "Checks"


func _get_inline_properties() -> PackedStringArray:
	return PackedStringArray(["factor"])
