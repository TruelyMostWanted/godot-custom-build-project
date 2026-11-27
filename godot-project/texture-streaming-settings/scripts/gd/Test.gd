extends FeatureTest
## Full API test of Texture Streaming Settings (TextureStreamingSettingsServer, TextureStreamingSettings).
## Same tests as scripts/cs/Test.cs. The server only exists with the texture_streaming module, so it is reached
## through Engine.get_singleton(). The renderer applies the values only on Forward+ and Mobile.

const STREAMING_FILE := "user://settings/texture_streaming.ini"

var _streaming: Object
var _restart_names: PackedStringArray = PackedStringArray()


func _run_tests() -> void:
	if not Engine.has_singleton(&"TextureStreamingSettingsServer"):
		print("  SKIP  this build has no texture streaming module")
		return
	_streaming = Engine.get_singleton(&"TextureStreamingSettingsServer")
	_streaming.reset_to_defaults()
	_test_defaults()
	_test_values()
	_test_support()
	_test_engine()
	_test_restart_required()
	_test_resource()
	_streaming.reset_to_defaults()
	await get_tree().process_frame
	DirAccess.remove_absolute(STREAMING_FILE)


func _test_defaults() -> void:
	var project_budget: int = ProjectSettings.get_setting("rendering/textures/streaming/memory_budget_mb")
	expect_equal(_streaming.get_memory_budget_mb(), project_budget, "the default budget is the project setting")
	expect(not _streaming.is_memory_budget_enabled(), "the memory budget is off by default")
	expect_equal(_streaming.get_min_lod(), 0, "the default min_lod is 0")


func _test_values() -> void:
	_streaming.set_enabled(true)
	expect(_streaming.is_enabled(), "enabled round trip")
	_streaming.set_memory_budget_enabled(true)
	expect(_streaming.is_memory_budget_enabled(), "memory_budget_enabled round trip")
	_streaming.set_memory_budget_mb(1024)
	expect_equal(_streaming.get_memory_budget_mb(), 1024, "memory_budget_mb round trip")
	_streaming.set_min_lod(1)
	expect_equal(_streaming.get_min_lod(), 1, "min_lod round trip")
	_streaming.set_max_lod(2)
	expect_equal(_streaming.get_max_lod(), 2, "max_lod round trip")
	_streaming.reset_to_defaults()


func _test_support() -> void:
	var supported: bool = _streaming.is_renderer_supported()
	var compatibility: bool = RenderingServer.get_current_rendering_method() == "gl_compatibility"
	expect_equal(supported, not compatibility, "streaming works on Forward+ and Mobile only")
	var unsupported: PackedStringArray = _streaming.get_unsupported_setting_names()
	expect_equal(unsupported.is_empty(), supported, "get_unsupported_setting_names is empty when the renderer supports streaming")


func _test_engine() -> void:
	if not _streaming.is_renderer_supported():
		print("  SKIP  engine checks (the renderer has no texture streaming)")
		return
	var texture_streaming: Object = Engine.get_singleton(&"TextureStreaming")
	_streaming.set_memory_budget_enabled(true)
	_streaming.set_memory_budget_mb(1)
	_streaming.set_min_lod(2)
	expect_equal(texture_streaming.memory_budget_mb_override, 1, "memory_budget_mb reaches TextureStreaming at once")
	expect_equal(texture_streaming.min_lod_override, 2, "min_lod reaches TextureStreaming at once")
	var memory_enabled: bool = ProjectSettings.get_setting("rendering/textures/streaming/memory_budget_enabled")
	expect(memory_enabled, "memory_budget_enabled reaches the project setting")
	var used: int = _streaming.get_memory_budget_bytes_used()
	expect(used >= 0, "get_memory_budget_bytes_used is not negative")
	_streaming.reset_to_defaults()


func _test_restart_required() -> void:
	_streaming.restart_required.connect(func(names: PackedStringArray) -> void: _restart_names = names)
	_streaming.set_enabled(true)
	expect(_restart_names.has("enabled"), "restart_required is emitted with \"enabled\"")
	var names: PackedStringArray = _streaming.get_restart_required_names()
	expect(names.has("enabled"), "get_restart_required_names lists \"enabled\"")
	_streaming.reset_to_defaults()


func _test_resource() -> void:
	var settings := TextureStreamingSettings.new()
	expect(not settings.enabled, "TextureStreamingSettings: not enabled by default")
	expect(not settings.memory_budget_enabled, "TextureStreamingSettings: memory budget off by default")
	expect_equal(settings.memory_budget_mb, 512, "TextureStreamingSettings: default memory_budget_mb")
	expect_equal(settings.min_lod, 0, "TextureStreamingSettings: default min_lod")
	expect_equal(settings.max_lod, 3, "TextureStreamingSettings: default max_lod")
	settings.memory_budget_mb = 256
	expect_equal(settings.memory_budget_mb, 256, "TextureStreamingSettings: memory_budget_mb is a plain property")
