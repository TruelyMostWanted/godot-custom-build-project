extends Node
## Minimal example of Simple Resource Formats: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Simple-Resource-Formats


func _enter_tree() -> void:
	# Load and save a file format with two functions
	ResourceLoader.register_extension_loader("quiz", _load_quiz, &"QuizSet")
	ResourceSaver.register_extension_saver("quiz", _save_quiz, &"QuizSet")


func _ready() -> void:
	var quiz: QuizSet = load("res://simple-resource-formats/resources/questions.quiz")
	ResourceSaver.save(quiz, "user://my_questions.quiz")
	print("Questions: ", quiz.questions.size())

	# Limit a format to one resource type
	# (the guide uses _load_settings and GameSettings; this example reuses the quiz)
	ResourceLoader.register_extension_loader("cfg.json", _load_quiz, &"QuizSet", true)
	print("cfg.json registered: ", ResourceLoader.has_extension_loader("cfg.json"))

	# Report a broken file
	# (the guide shows _load_quiz with a check for an empty file; registered again, it replaces the first one)
	ResourceLoader.register_extension_loader("quiz", _load_checked_quiz, &"QuizSet")
	var checked: QuizSet = load("res://simple-resource-formats/resources/questions.quiz")
	print("Checked questions: ", checked.questions.size())

	ResourceLoader.unregister_extension_loader("quiz")
	ResourceLoader.unregister_extension_loader("cfg.json")
	ResourceSaver.unregister_extension_saver("quiz")
	get_tree().quit()


static func _load_quiz(path: String) -> Variant:
	var file := FileAccess.open(path, FileAccess.READ)
	if file == null:
		return FileAccess.get_open_error()
	var quiz := QuizSet.new()
	quiz.questions = file.get_as_text().split("\n", false)
	return quiz


static func _save_quiz(quiz: QuizSet, path: String) -> Error:
	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		return FileAccess.get_open_error()
	file.store_string("\n".join(quiz.questions))
	return OK


static func _load_checked_quiz(path: String) -> Variant:
	var text := FileAccess.get_file_as_string(path)
	if text.is_empty():
		push_error("'%s' has no questions." % path)
		return ERR_FILE_CORRUPT
	var quiz := QuizSet.new()
	quiz.questions = text.split("\n", false)
	return quiz
