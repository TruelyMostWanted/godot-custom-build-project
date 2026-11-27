@tool
class_name AddNode
extends FlowNode
## Data node of the guide section "Custom node types": outputs a + b.


func _get_input_ports() -> Array[FlowPort]:
	return [FlowPort.create_data("a", TYPE_INT), FlowPort.create_data("b", TYPE_INT)]


func _get_output_ports() -> Array[FlowPort]:
	return [FlowPort.create_data("sum", TYPE_INT)]


func _evaluate_output(playback: FlowGraphPlayback, _port: StringName) -> Variant:
	return playback.get_input_value("a") + playback.get_input_value("b")
