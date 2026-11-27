extends Node
## Minimal example of Image Files: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/Image-Files


func _ready() -> void:
	# Setup: sample pictures in user://, a Sprite2D and a TextureRect (the guide assumes them).
	DirAccess.make_dir_recursive_absolute("user://screenshots")
	DirAccess.make_dir_recursive_absolute("user://mods")
	DirAccess.make_dir_recursive_absolute("user://photos")
	var sample: Image = Image.create_empty(64, 64, false, Image.FORMAT_RGBA8)
	sample.fill(Color.RED)
	sample.save_png("user://screenshots/shot.png")
	sample.save_webp("user://mods/skin.webp")
	sample.save_jpg("user://photos/large.jpg")
	var sprite := Sprite2D.new()
	sprite.name = "Sprite2D"
	add_child(sprite)
	var texture_rect := TextureRect.new()
	texture_rect.name = "TextureRect"
	add_child(texture_rect)

	# Show a picture from the player's folder
	$Sprite2D.texture = load("user://screenshots/shot.png")
	print("Texture: ", $Sprite2D.texture.get_size())

	# Edit the pixels of a loaded image
	var skin: Image = ResourceLoader.load("user://mods/skin.webp", "Image", ResourceLoader.CACHE_MODE_IGNORE) as Image
	skin.adjust_bcs(1.0, 1.2, 1.0)
	$Sprite2D.texture = ImageTexture.create_from_image(skin)
	print("Skin pixel: ", skin.get_pixel(0, 0))

	# Save a screenshot as JPEG with a quality (the sample stands in for the viewport image, which headless has none)
	var image: Image = sample
	var options := ImageSaveOptions.new()
	options.jpg_quality = 0.9
	options.attach_to(image)
	var error: Error = ResourceSaver.save(image, "user://screenshots/shot.jpg")
	ImageSaveOptions.detach_from(image)
	print("Saved JPEG: ", error == OK)

	# Load a large image in the background
	ResourceLoader.load_threaded_request("user://photos/large.jpg")
	while ResourceLoader.load_threaded_get_status("user://photos/large.jpg") == ResourceLoader.THREAD_LOAD_IN_PROGRESS:
		await get_tree().process_frame
	$TextureRect.texture = ResourceLoader.load_threaded_get("user://photos/large.jpg")
	print("Background load: ", $TextureRect.texture.get_size())
	get_tree().quit()
