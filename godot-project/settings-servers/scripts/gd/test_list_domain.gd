extends ListSettingsServer
## List domain "test_list" (not registered at startup).


func _get_domain() -> StringName:
	return &"test_list"


func _create_settings() -> Resource:
	return SettingsServersTestList.new()


func _get_entry_key() -> StringName:
	return &"entry_name"
