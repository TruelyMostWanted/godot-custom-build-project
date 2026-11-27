@tool
class_name FormatsAmountNode
extends FlowNode
## Script node type of the tests: a flow node with a property and a typed data input "amount".
## Shows that script node types round-trip through every format.

@export var factor: int = 2


func _get_input_ports() -> Array[FlowPort]:
	return [FlowPort.create_flow(&"in"), FlowPort.create_data(&"amount", TYPE_INT)]


func _get_output_ports() -> Array[FlowPort]:
	return [FlowPort.create_flow(&"out")]
