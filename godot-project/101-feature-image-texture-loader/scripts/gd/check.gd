extends FeatureCheck
## GDScript checks of 101-feature-image-texture-loader. Check the same things as scripts/cs/Issue101FeatureImageTextureLoaderCheck.cs.
## All test images are generated at runtime: encoded with ResourceSaver (png, jpg, webp, exr, dds) or written byte by byte (bmp, tga, hdr, svg, ktx).

const DIR := "user://101-feature-image-texture-loader-gd"
const IMPORTED_SVG := "res://07-feature-files-manifest/resources/data/a/icon.svg"


func _get_check_name() -> String:
	return "101-feature-image-texture-loader"


static func _push_u16(data: PackedByteArray, value: int) -> void:
	data.append(value & 0xff)
	data.append((value >> 8) & 0xff)


static func _push_u32(data: PackedByteArray, value: int) -> void:
	_push_u16(data, value & 0xffff)
	_push_u16(data, (value >> 16) & 0xffff)


# 4x4 RGBA8: top half red, bottom half blue.
static func _make_image() -> Image:
	var image := Image.create_empty(4, 4, false, Image.FORMAT_RGBA8)
	for y in 4:
		for x in 4:
			image.set_pixel(x, y, Color(1, 0, 0) if y < 2 else Color(0, 0, 1))
	return image


# 64x64 pattern, so that lossy encoders give different sizes at different qualities.
static func _make_pattern_image() -> Image:
	var image := Image.create_empty(64, 64, false, Image.FORMAT_RGBA8)
	for y in 64:
		for x in 64:
			image.set_pixel(x, y, Color(((x * 37 + y * 11) % 64) / 63.0, ((x * y) % 64) / 63.0, ((x + y * 53) % 64) / 63.0))
	return image


# Uncompressed 2x2 BMP, 24 bits, bottom-up: bottom row green, top row red.
static func _make_bmp() -> PackedByteArray:
	var data := PackedByteArray([0x42, 0x4D])
	_push_u32(data, 54 + 16)
	_push_u32(data, 0)
	_push_u32(data, 54)
	_push_u32(data, 40)
	_push_u32(data, 2)
	_push_u32(data, 2)
	_push_u16(data, 1)
	_push_u16(data, 24)
	_push_u32(data, 0)
	_push_u32(data, 16)
	_push_u32(data, 2835)
	_push_u32(data, 2835)
	_push_u32(data, 0)
	_push_u32(data, 0)
	for row in 2:
		for _x in 2:
			data.append_array(PackedByteArray([0, 255, 0]) if row == 0 else PackedByteArray([0, 0, 255]))
		data.append_array(PackedByteArray([0, 0]))
	return data


# Uncompressed true-color 2x2 TGA, 32 bits, top-left origin, all red.
static func _make_tga() -> PackedByteArray:
	var data := PackedByteArray([0, 0, 2, 0, 0, 0, 0, 0])
	_push_u16(data, 0)
	_push_u16(data, 0)
	_push_u16(data, 2)
	_push_u16(data, 2)
	data.append_array(PackedByteArray([32, 0x28]))
	for _i in 4:
		data.append_array(PackedByteArray([0, 0, 255, 255]))
	return data


# Flat 2x2 Radiance HDR, all (1, 0, 0).
static func _make_hdr() -> PackedByteArray:
	var data := "#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n-Y 2 +X 2\n".to_ascii_buffer()
	for _i in 4:
		data.append_array(PackedByteArray([128, 0, 0, 129]))
	return data


# KTX 1.1, 2x2 GL_RGBA8, one level, all red.
static func _make_ktx() -> PackedByteArray:
	var data := PackedByteArray([0xAB, 0x4B, 0x54, 0x58, 0x20, 0x31, 0x31, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A])
	for value in [0x04030201, 0x1401, 1, 0x1908, 0x8058, 0x1908, 2, 2, 0, 0, 1, 1, 0, 16]:
		_push_u32(data, value)
	for _i in 4:
		data.append_array(PackedByteArray([255, 0, 0, 255]))
	return data


static func _write(path: String, data: PackedByteArray) -> void:
	var file := FileAccess.open(path, FileAccess.WRITE)
	file.store_buffer(data)
	file.close()


func _expect_texture(path: String, width: int, label: String) -> ImageTexture:
	var res := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	var texture := res as ImageTexture
	expect(texture != null, "%s loads as ImageTexture" % label)
	if texture != null:
		expect(texture.get_width() == width, "%s has width %d" % [label, width])
	return texture


func _expect_image(path: String, width: int, label: String) -> Image:
	var res := ResourceLoader.load(path, "Image", ResourceLoader.CACHE_MODE_IGNORE)
	var image := res as Image
	expect(image != null, "%s loads as Image with the hint \"Image\"" % label)
	if image != null:
		expect(image.get_width() == width, "%s Image has width %d" % [label, width])
	return image


func _run_checks() -> void:
	DirAccess.make_dir_recursive_absolute(DIR)
	expect(ClassDB.class_exists("ImageSaveOptions"), "ImageSaveOptions is registered")

	# ImageSaveOptions defaults.
	var defaults := ImageSaveOptions.new()
	expect(is_equal_approx(defaults.jpg_quality, 0.75), "jpg_quality defaults to 0.75")
	expect(not defaults.webp_lossy, "webp_lossy defaults to false")
	expect(is_equal_approx(defaults.webp_quality, 0.75), "webp_quality defaults to 0.75")
	expect(not defaults.exr_grayscale, "exr_grayscale defaults to false")

	# PNG: save an ImageTexture, load it back as ImageTexture and as Image.
	var png := DIR + "/round_trip.png"
	var texture := ImageTexture.create_from_image(_make_image())
	expect(ResourceSaver.save(texture, png) == OK, "ResourceSaver.save writes .png")
	expect(ResourceLoader.exists(png), "ResourceLoader.exists finds the .png")
	expect(ResourceLoader.exists(png, "Image"), "ResourceLoader.exists with hint Image")
	var png_texture := _expect_texture(png, 4, "png")
	if png_texture != null:
		var pixels := png_texture.get_image()
		expect(pixels.get_pixel(0, 0).is_equal_approx(Color(1, 0, 0)), "png pixel (0, 0) is red")
		expect(pixels.get_pixel(3, 3).is_equal_approx(Color(0, 0, 1)), "png pixel (3, 3) is blue")
	_expect_image(png, 4, "png")
	var cached_a := ResourceLoader.load(png)
	var cached_b := ResourceLoader.load(png)
	expect(cached_a != null and cached_a == cached_b, "png loads through the resource cache")

	# An Image saves directly.
	var image_png := DIR + "/from_image.png"
	expect(ResourceSaver.save(_make_image(), image_png) == OK, "ResourceSaver.save writes an Image to .png")
	_expect_image(image_png, 4, "Image png")

	# JPEG with quality options.
	var pattern := ImageTexture.create_from_image(_make_pattern_image())
	var options := ImageSaveOptions.new()
	options.jpg_quality = 0.1
	options.attach_to(pattern)
	expect(ImageSaveOptions.get_attached(pattern) == options, "get_attached returns the attached options")
	expect(ResourceSaver.save(pattern, DIR + "/low.jpg") == OK, "ResourceSaver.save writes .jpg")
	options.jpg_quality = 1.0
	expect(ResourceSaver.save(pattern, DIR + "/high.jpeg") == OK, "ResourceSaver.save writes .jpeg")
	expect(FileAccess.get_size(DIR + "/low.jpg") < FileAccess.get_size(DIR + "/high.jpeg"), "jpg_quality changes the file size")
	_expect_texture(DIR + "/low.jpg", 64, "jpg")
	_expect_texture(DIR + "/high.jpeg", 64, "jpeg")

	# WebP lossless and lossy.
	ImageSaveOptions.detach_from(pattern)
	expect(ImageSaveOptions.get_attached(pattern) == null, "detach_from removes the options")
	expect(ResourceSaver.save(pattern, DIR + "/lossless.webp") == OK, "ResourceSaver.save writes lossless .webp")
	options.webp_lossy = true
	options.webp_quality = 0.1
	options.attach_to(pattern)
	expect(ResourceSaver.save(pattern, DIR + "/lossy.webp") == OK, "ResourceSaver.save writes lossy .webp")
	expect(FileAccess.get_size(DIR + "/lossy.webp") < FileAccess.get_size(DIR + "/lossless.webp"), "webp_lossy makes a smaller file")
	_expect_texture(DIR + "/lossless.webp", 64, "webp")

	# OpenEXR and DDS.
	expect(ResourceSaver.save(texture, DIR + "/float.exr") == OK, "ResourceSaver.save writes .exr")
	_expect_texture(DIR + "/float.exr", 4, "exr")
	expect(ResourceSaver.save(_make_image(), DIR + "/texture.dds") == OK, "ResourceSaver.save writes .dds")
	_expect_texture(DIR + "/texture.dds", 4, "dds")
	_expect_image(DIR + "/texture.dds", 4, "dds")

	# Formats without an encoder: written by hand, loaded through ResourceLoader.
	_write(DIR + "/image.bmp", _make_bmp())
	var bmp := _expect_texture(DIR + "/image.bmp", 2, "bmp")
	if bmp != null:
		expect(bmp.get_image().get_pixel(0, 1).is_equal_approx(Color(0, 1, 0)), "bmp bottom row is green")
	_write(DIR + "/image.tga", _make_tga())
	_expect_texture(DIR + "/image.tga", 2, "tga")
	_write(DIR + "/image.hdr", _make_hdr())
	_expect_texture(DIR + "/image.hdr", 2, "hdr")
	_write(DIR + "/image.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"8\" height=\"6\"><rect width=\"8\" height=\"6\" fill=\"#ff0000\"/></svg>".to_utf8_buffer())
	_expect_texture(DIR + "/image.svg", 8, "svg")
	_write(DIR + "/image.ktx", _make_ktx())
	_expect_texture(DIR + "/image.ktx", 2, "ktx")
	_expect_image(DIR + "/image.ktx", 2, "ktx")

	# No encoder for bmp.
	expect(ResourceSaver.save(texture, DIR + "/nope.bmp") != OK, "ResourceSaver.save refuses .bmp")

	# Imported files keep their importer.
	var imported := load(IMPORTED_SVG)
	expect(imported != null and not (imported is ImageTexture), "imported res:// svg still loads through the importer")

	# Background loading.
	var threaded := DIR + "/lossless.webp"
	expect(ResourceLoader.load_threaded_request(threaded) == OK, "load_threaded_request accepts an image file")
	while ResourceLoader.load_threaded_get_status(threaded) == ResourceLoader.THREAD_LOAD_IN_PROGRESS:
		await get_tree().process_frame
	expect(ResourceLoader.load_threaded_get_status(threaded) == ResourceLoader.THREAD_LOAD_LOADED, "threaded load finishes")
	var threaded_texture := ResourceLoader.load_threaded_get(threaded) as ImageTexture
	expect(threaded_texture != null and threaded_texture.get_width() == 64, "load_threaded_get returns the ImageTexture")
