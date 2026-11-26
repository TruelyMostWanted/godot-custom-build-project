@tool
class_name Issue259MoodLineNode
extends DialogueLineNode
## Script node type of the 259 checks: a dialogue line with a translatable mood ("angry", "happy").
## Its strings are added to the built-in strings of DialogueLineNode.

@export var mood := ""


func _get_translatable_strings() -> Array[PackedStringArray]:
	if mood.is_empty():
		return []
	return [PackedStringArray([mood, "", "", "Mood of a line"])]
