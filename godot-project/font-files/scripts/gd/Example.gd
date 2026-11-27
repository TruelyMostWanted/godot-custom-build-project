extends Node
## Minimal example of Font Files: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Font-Files
## Not run here: the settings menu snippet (it needs a UI with a check box).


func _ready() -> void:
	# Setup: fonts in user://fonts (the guide assumes them) and Labels named Label, Title and Code.
	DirAccess.make_dir_recursive_absolute("user://fonts")
	DirAccess.copy_absolute("res://font-files/resources/fonts/JetBrainsMono_Subset.ttf", "user://fonts/MyFont.ttf")
	DirAccess.copy_absolute("res://font-files/resources/bmfont/TestBitmap.fnt", "user://fonts/Pixel.fnt")
	DirAccess.copy_absolute("res://font-files/resources/bmfont/TestBitmap_0.png", "user://fonts/TestBitmap_0.png")
	for label_name in ["Label", "Title", "Code"]:
		var label := Label.new()
		label.name = label_name
		add_child(label)

	# Use a font the player added
	var font := load("user://fonts/MyFont.ttf") as FontFile
	$Label.add_theme_font_override("font", font)
	$Label.add_theme_font_size_override("font_size", 24)
	print("Font data bytes: ", font.data.size())

	# Load a bitmap font (the page of Pixel.fnt is named TestBitmap_0.png, so the sample .fnt is copied as Pixel.fnt)
	var pixel_font := load("user://fonts/Pixel.fnt") as FontFile
	print(pixel_font.fixed_size) # the size from the "info" line

	# Find an installed font
	var system_font := SystemFont.find_first(["Segoe UI", "SF Pro Text", "Roboto", "Noto Sans"], 600)
	if system_font:
		$Title.add_theme_font_override("font", system_font)

	var code_font := SystemFont.find("Consolas")
	print(code_font != null) # false if Consolas is not installed

	print("Segoe UI installed: ", SystemFont.find("Segoe UI") != null)
	print("NoSuchFont installed: ", SystemFont.find("NoSuchFont") != null)
	var first := SystemFont.find_first(["NoSuchFont", "Segoe UI"], 600)
	print("find_first picks: ", first.font_names, " weight ", first.font_weight)

	# sysfont:// paths
	if ResourceLoader.exists("sysfont://Consolas"):
		$Code.add_theme_font_override("font", load("sysfont://Consolas"))

	var theme := Theme.new()
	theme.default_font = load("sysfont://Segoe%20UI,Noto%20Sans?weight=600")
	ResourceSaver.save(theme, "user://ui_theme.tres")
	print("Theme saved with font: ", theme.default_font.resource_path)

	# Load a shader from user://
	DirAccess.make_dir_recursive_absolute("user://shaders")
	var include := ShaderInclude.new()
	include.code = "uniform vec4 tint : source_color = vec4(1.0);\n"
	ResourceSaver.save(include, "user://shaders/tint.gdshaderinc")

	var shader := Shader.new()
	shader.code = "shader_type canvas_item;\n#include \"user://shaders/tint.gdshaderinc\"\nvoid fragment() { COLOR *= tint; }\n"
	ResourceSaver.save(shader, "user://shaders/tinted.gdshader")

	var material := ShaderMaterial.new()
	material.shader = load("user://shaders/tinted.gdshader")
	print("Shader mode: ", material.shader.get_mode())
	get_tree().quit()
