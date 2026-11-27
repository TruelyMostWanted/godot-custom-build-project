extends FeatureTest
## Full API test of Image Files (runtime image loader and saver, ImageSaveOptions). Same tests as scripts/cs/Test.cs.

const DIR := "user://image-files-test"
const IMPORTED_SVG := "res://icon.svg"
const SIZE := 4
const PATTERN_SIZE := 64
const DEFAULT_QUALITY := 0.75
const SAMPLE_OPTIONS := "res://image-files/resources/high_quality_save_options.tres"
const SVG := '<svg xmlns="http://www.w3.org/2000/svg" width="8" height="6"><rect width="8" height="6" fill="#ff0000"/></svg>'
# Hand-written 2x2 files of formats without an encoder: [hex bytes, width].
const RAW_FILES := {
	"bmp": ["424d460000000000000036000000280000000200000002000000010018000000000010000000130b0000130b0000000000000000000000ff0000ff0000000000ff0000ff0000", 2],
	"tga": ["0000020000000000000000000200020020280000ffff0000ffff0000ffff0000ffff", 2],
	"hdr": ["233f52414449414e43450a464f524d41543d33322d6269745f726c655f726762650a0a2d592032202b5820320a80000081800000818000008180000081", 2],
	"ktx": ["ab4b5458203131bb0d0a1a0a0102030401140000010000000819000058800000081900000200000002000000000000000000000001000000010000000000000010000000ff0000ffff0000ffff0000ffff0000ff", 2],
}


func _run_tests() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	_test_options()
	_test_png()
	_test_jpeg()
	_test_webp()
	_test_exr_dds()
	_test_raw_formats()
	_test_unsupported_save()
	_test_imported()
	await _test_threaded()


func _make_image() -> Image:
	var image: Image = Image.create_empty(SIZE, SIZE, false, Image.FORMAT_RGBA8)
	image.fill_rect(Rect2i(0, 0, SIZE, 2), Color.RED)
	image.fill_rect(Rect2i(0, 2, SIZE, 2), Color.BLUE)
	return image


# A pattern, so that lossy encoders give different sizes at different qualities.
func _make_pattern() -> ImageTexture:
	var image: Image = Image.create_empty(PATTERN_SIZE, PATTERN_SIZE, false, Image.FORMAT_RGBA8)
	for y in PATTERN_SIZE:
		for x in PATTERN_SIZE:
			var red: float = ((x * 37 + y * 11) % 64) / 63.0
			var green: float = ((x * y) % 64) / 63.0
			var blue: float = ((x + y * 53) % 64) / 63.0
			image.set_pixel(x, y, Color(red, green, blue))
	return ImageTexture.create_from_image(image)


func _load_texture(path: String, width: int) -> ImageTexture:
	var texture: ImageTexture = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE) as ImageTexture
	expect(texture != null, "%s loads as ImageTexture" % path.get_file())
	expect_equal(texture.get_width(), width, "%s has the width of the file" % path.get_file())
	return texture


func _load_image(path: String, width: int) -> Image:
	var image: Image = ResourceLoader.load(path, "Image", ResourceLoader.CACHE_MODE_IGNORE) as Image
	expect(image != null, "%s loads as Image with the type hint Image" % path.get_file())
	expect_equal(image.get_width(), width, "%s Image has the width of the file" % path.get_file())
	return image


func _test_options() -> void:
	var options := ImageSaveOptions.new()
	expect_equal(options.jpg_quality, DEFAULT_QUALITY, "jpg_quality defaults to 0.75")
	expect(not options.webp_lossy, "webp_lossy defaults to false")
	expect_equal(options.webp_quality, DEFAULT_QUALITY, "webp_quality defaults to 0.75")
	expect(not options.exr_grayscale, "exr_grayscale defaults to false")
	var sample: ImageSaveOptions = load(SAMPLE_OPTIONS) as ImageSaveOptions
	expect(sample.webp_lossy, "the sample .tres loads with its values")
	var image: Image = _make_image()
	options.attach_to(image)
	expect(ImageSaveOptions.get_attached(image) == options, "get_attached returns the attached options")
	ImageSaveOptions.detach_from(image)
	expect(ImageSaveOptions.get_attached(image) == null, "detach_from removes the options")


func _test_png() -> void:
	var path: String = DIR + "/round_trip.png"
	var texture: ImageTexture = ImageTexture.create_from_image(_make_image())
	expect_equal(ResourceSaver.save(texture, path), OK, "ResourceSaver.save writes a texture to .png")
	expect(ResourceLoader.exists(path), "ResourceLoader.exists finds the .png")
	expect(ResourceLoader.exists(path, "Image"), "ResourceLoader.exists finds the .png with the hint Image")
	var loaded: ImageTexture = _load_texture(path, SIZE)
	var pixels: Image = loaded.get_image()
	expect(pixels.get_pixel(0, 0).is_equal_approx(Color.RED), "top-left pixel is red")
	expect(pixels.get_pixel(SIZE - 1, SIZE - 1).is_equal_approx(Color.BLUE), "bottom-right pixel is blue")
	_load_image(path, SIZE)
	var first: Resource = ResourceLoader.load(path)
	var second: Resource = ResourceLoader.load(path)
	expect(first == second, "the png loads through the resource cache")
	var image_path: String = DIR + "/from_image.png"
	expect_equal(ResourceSaver.save(_make_image(), image_path), OK, "ResourceSaver.save writes an Image to .png")
	_load_image(image_path, SIZE)


func _test_jpeg() -> void:
	var pattern: ImageTexture = _make_pattern()
	var options := ImageSaveOptions.new()
	options.jpg_quality = 0.1
	options.attach_to(pattern)
	expect_equal(ResourceSaver.save(pattern, DIR + "/low.jpg"), OK, "ResourceSaver.save writes .jpg")
	options.jpg_quality = 1.0
	expect_equal(ResourceSaver.save(pattern, DIR + "/high.jpeg"), OK, "ResourceSaver.save writes .jpeg")
	var low_size: int = FileAccess.get_size(DIR + "/low.jpg")
	var high_size: int = FileAccess.get_size(DIR + "/high.jpeg")
	expect(low_size < high_size, "jpg_quality changes the file size")
	_load_texture(DIR + "/low.jpg", PATTERN_SIZE)
	_load_texture(DIR + "/high.jpeg", PATTERN_SIZE)


func _test_webp() -> void:
	var pattern: ImageTexture = _make_pattern()
	expect_equal(ResourceSaver.save(pattern, DIR + "/lossless.webp"), OK, "ResourceSaver.save writes lossless .webp")
	var options := ImageSaveOptions.new()
	options.webp_lossy = true
	options.webp_quality = 0.1
	options.attach_to(pattern)
	expect_equal(ResourceSaver.save(pattern, DIR + "/lossy.webp"), OK, "ResourceSaver.save writes lossy .webp")
	var lossless_size: int = FileAccess.get_size(DIR + "/lossless.webp")
	var lossy_size: int = FileAccess.get_size(DIR + "/lossy.webp")
	expect(lossy_size < lossless_size, "webp_lossy makes a smaller file")
	_load_texture(DIR + "/lossless.webp", PATTERN_SIZE)


func _test_exr_dds() -> void:
	var texture: ImageTexture = ImageTexture.create_from_image(_make_image())
	var options := ImageSaveOptions.new()
	options.exr_grayscale = true
	options.attach_to(texture)
	expect_equal(ResourceSaver.save(texture, DIR + "/float.exr"), OK, "ResourceSaver.save writes .exr")
	_load_texture(DIR + "/float.exr", SIZE)
	expect_equal(ResourceSaver.save(texture, DIR + "/texture.dds"), OK, "ResourceSaver.save writes .dds")
	_load_texture(DIR + "/texture.dds", SIZE)
	_load_image(DIR + "/texture.dds", SIZE)


func _test_raw_formats() -> void:
	for extension: String in RAW_FILES:
		var path: String = DIR + "/image." + extension
		var file: FileAccess = FileAccess.open(path, FileAccess.WRITE)
		file.store_buffer(String(RAW_FILES[extension][0]).hex_decode())
		file.close()
		_load_texture(path, RAW_FILES[extension][1])
	var bmp: Image = _load_texture(DIR + "/image.bmp", 2).get_image()
	expect(bmp.get_pixel(0, 1).is_equal_approx(Color.GREEN), "bmp bottom row is green")
	_load_image(DIR + "/image.ktx", 2)
	var svg_path: String = DIR + "/image.svg"
	var svg_file: FileAccess = FileAccess.open(svg_path, FileAccess.WRITE)
	svg_file.store_string(SVG)
	svg_file.close()
	_load_texture(svg_path, 8)


func _test_unsupported_save() -> void:
	var texture: ImageTexture = ImageTexture.create_from_image(_make_image())
	expect(ResourceSaver.save(texture, DIR + "/nope.bmp") != OK, "ResourceSaver.save refuses .bmp (no encoder)")


func _test_imported() -> void:
	var imported: Resource = load(IMPORTED_SVG)
	expect(imported != null and not (imported is ImageTexture), "an imported res:// file still loads through the importer")


func _test_threaded() -> void:
	var path: String = DIR + "/lossless.webp"
	expect_equal(ResourceLoader.load_threaded_request(path), OK, "load_threaded_request accepts an image file")
	while ResourceLoader.load_threaded_get_status(path) == ResourceLoader.THREAD_LOAD_IN_PROGRESS:
		await get_tree().process_frame
	expect_equal(ResourceLoader.load_threaded_get_status(path), ResourceLoader.THREAD_LOAD_LOADED, "the threaded load finishes")
	var texture: ImageTexture = ResourceLoader.load_threaded_get(path) as ImageTexture
	expect_equal(texture.get_width(), PATTERN_SIZE, "load_threaded_get returns the ImageTexture")
