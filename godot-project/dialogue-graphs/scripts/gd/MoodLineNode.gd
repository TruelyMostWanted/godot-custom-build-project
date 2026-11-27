@tool
class_name MoodLineNode
extends DialogueLineNode
## Script node type of the wiki guide (Localization): a dialogue line with a translatable mood.
## Its strings are added to the built-in strings of DialogueLineNode.

@export var mood: String = ""


func _get_translatable_strings() -> Array[PackedStringArray]:
	if mood.is_empty():
		return []
	return [PackedStringArray([mood, "", "", "Mood of a line"])]
