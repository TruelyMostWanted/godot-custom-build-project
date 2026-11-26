extends FeatureCheck
## GDScript checks of 367-refactoring-typed-loader-saver-virtuals. Check the same things as scripts/cs/Issue367RefactoringTypedLoaderSaverVirtualsCheck.cs.
##
## Both format pairs store resource_name as one text line. The "A" pair overrides the virtuals with the OLD
## `int` typing (cache_mode: int). The analyzer accepts an `int` parameter where the parent declares an enum,
## so this class must analyze without warnings or errors. The "B" pair uses the new enum typing.


class LoaderA extends ResourceFormatLoader:
	func _get_recognized_extensions() -> PackedStringArray:
		return PackedStringArray(["t367a"])

	func _handles_type(type: StringName) -> bool:
		return type == &"Resource"

	func _get_resource_type(path: String) -> String:
		return "Resource" if path.get_extension() == "t367a" else ""

	# Old typing: int instead of ResourceFormatLoader.CacheMode.
	func _load(path: String, original_path: String, use_sub_threads: bool, cache_mode: int) -> Variant:
		var file := FileAccess.open(path, FileAccess.READ)
		if file == null:
			return ERR_FILE_CANT_OPEN
		var res := Resource.new()
		res.resource_name = file.get_line()
		return res


class SaverA extends ResourceFormatSaver:
	func _recognize(resource: Resource) -> bool:
		return resource.get_meta("fmt", "") == "a"

	func _get_recognized_extensions(resource: Resource) -> PackedStringArray:
		return PackedStringArray(["t367a"])

	# Old typing: flags as plain int (GDScript sees BitField as int either way).
	func _save(resource: Resource, path: String, flags: int) -> Error:
		var file := FileAccess.open(path, FileAccess.WRITE)
		if file == null:
			return ERR_FILE_CANT_OPEN
		file.store_line(resource.resource_name)
		return OK


class LoaderB extends ResourceFormatLoader:
	var last_cache_mode: int = -1

	func _get_recognized_extensions() -> PackedStringArray:
		return PackedStringArray(["t367b"])

	func _handles_type(type: StringName) -> bool:
		return type == &"Resource"

	func _get_resource_type(path: String) -> String:
		return "Resource" if path.get_extension() == "t367b" else ""

	func _load(path: String, original_path: String, use_sub_threads: bool, cache_mode: ResourceFormatLoader.CacheMode) -> Variant:
		last_cache_mode = cache_mode
		var file := FileAccess.open(path, FileAccess.READ)
		if file == null:
			return ERR_FILE_CANT_OPEN
		var res := Resource.new()
		res.resource_name = file.get_line()
		return res


class SaverB extends ResourceFormatSaver:
	var last_flags: int = -1

	func _recognize(resource: Resource) -> bool:
		return resource.get_meta("fmt", "") == "b"

	func _get_recognized_extensions(resource: Resource) -> PackedStringArray:
		return PackedStringArray(["t367b"])

	func _save(resource: Resource, path: String, flags: int) -> Error:
		last_flags = flags
		var file := FileAccess.open(path, FileAccess.WRITE)
		if file == null:
			return ERR_FILE_CANT_OPEN
		file.store_line(resource.resource_name)
		return OK


func _get_check_name() -> String:
	return "367-refactoring-typed-loader-saver-virtuals"


func _remove(path: String) -> void:
	if FileAccess.file_exists(path):
		DirAccess.remove_absolute(path)


func _make(res_name: String, fmt: String = "") -> Resource:
	var res := Resource.new()
	res.resource_name = res_name
	if fmt != "":
		res.set_meta("fmt", fmt)
	return res


func _run_checks() -> void:
	_check_old_typing()
	_check_new_typing()
	_check_cache_modes()
	_check_paths()


func _check_old_typing() -> void:
	var path := "user://t367_a.t367a"
	var loader := LoaderA.new()
	var saver := SaverA.new()
	ResourceLoader.add_resource_format_loader(loader)
	ResourceSaver.add_resource_format_saver(saver)

	expect_equal(ResourceSaver.save(_make("hello-a", "a"), path), OK, "A: save with int-typed _save override")
	expect(FileAccess.file_exists(path), "A: file was written")
	var loaded := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(loaded != null, "A: load with int-typed _load override returns a resource")
	if loaded != null:
		expect_equal(loaded.resource_name, "hello-a", "A: value survives the round trip")

	ResourceLoader.remove_resource_format_loader(loader)
	ResourceSaver.remove_resource_format_saver(saver)
	_remove(path)


func _check_new_typing() -> void:
	var path := "user://t367_b.t367b"
	var loader := LoaderB.new()
	var saver := SaverB.new()
	ResourceLoader.add_resource_format_loader(loader)
	ResourceSaver.add_resource_format_saver(saver)

	expect_equal(ResourceSaver.save(_make("hello-b", "b"), path, ResourceSaver.FLAG_COMPRESS), OK, "B: save with enum-typed _save override")
	expect((saver.last_flags & ResourceSaver.FLAG_COMPRESS) != 0, "B: saver received FLAG_COMPRESS")
	expect_equal(ResourceSaver.save(_make("hello-b", "b"), path), OK, "B: save without flags")
	expect_equal(saver.last_flags & ResourceSaver.FLAG_COMPRESS, 0, "B: saver received no FLAG_COMPRESS by default")

	var ignored := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(ignored != null, "B: load with CACHE_MODE_IGNORE returns a resource")
	if ignored != null:
		expect_equal(ignored.resource_name, "hello-b", "B: value survives the round trip")
	expect_equal(loader.last_cache_mode, int(ResourceFormatLoader.CACHE_MODE_IGNORE), "B: loader received CACHE_MODE_IGNORE")
	expect_equal(loader.last_cache_mode, 0, "B: CACHE_MODE_IGNORE is 0")

	var reused := ResourceLoader.load(path)
	expect(reused != null, "B: default load returns a resource")
	expect_equal(loader.last_cache_mode, int(ResourceFormatLoader.CACHE_MODE_REUSE), "B: loader received CACHE_MODE_REUSE by default")
	expect_equal(loader.last_cache_mode, 1, "B: CACHE_MODE_REUSE is 1")

	ResourceLoader.remove_resource_format_loader(loader)
	ResourceSaver.remove_resource_format_saver(saver)
	_remove(path)


func _check_cache_modes() -> void:
	var path := "user://t367_cache.tres"
	expect_equal(ResourceSaver.save(_make("v1"), path), OK, "cache: save .tres")

	var first := ResourceLoader.load(path)
	expect(first != null, "cache: REUSE load returns a resource")
	if first == null:
		_remove(path)
		return
	expect_equal(first.resource_path, path, "cache: loaded resource_path equals the path")
	expect(ResourceLoader.load(path) == first, "cache: second REUSE load returns the same instance")

	var ignored := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
	expect(ignored != null and ignored != first, "cache: IGNORE returns a different instance")
	if ignored != null:
		expect_equal(ignored.resource_path, path, "cache: IGNORE instance has the path too")
	expect(ResourceLoader.load(path) == first, "cache: REUSE still returns the first instance after IGNORE")

	expect_equal(ResourceSaver.save(_make("v2"), path), OK, "cache: re-save with new content")
	var replaced := ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_REPLACE)
	expect(replaced == first, "cache: REPLACE returns the first instance")
	expect_equal(first.resource_name, "v2", "cache: REPLACE updated the instance in place")

	_remove(path)


func _check_paths() -> void:
	var path := "user://t367_paths.tres"
	var fresh := _make("fresh")
	expect_equal(ResourceSaver.save(fresh, path, ResourceSaver.FLAG_CHANGE_PATH), OK, "paths: save with FLAG_CHANGE_PATH")
	expect_equal(fresh.resource_path, "", "paths: resource_path is restored to empty after saving")

	var first := ResourceLoader.load(path)
	expect(first != null and first.resource_path == path, "paths: first resource holds the path")
	var second := Resource.new()
	second.take_over_path(path)
	expect_equal(second.resource_path, path, "paths: take_over_path gives the second resource the path")
	if first != null:
		expect_equal(first.resource_path, "", "paths: the first resource lost the path")

	_remove(path)
