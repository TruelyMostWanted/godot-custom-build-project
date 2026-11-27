extends Node
## Minimal example of HTTP Requests: the snippets of the wiki guide, in order.
## Wiki: https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki/HTTP-Requests
## request_target() is not called: the example sends nothing over the network. build_*() shows what would be sent.


func _ready() -> void:
	# Post a score as JSON.
	var http := HTTPRequest.new()
	add_child(http)
	http.response_received.connect(_on_response_received)

	var target := HTTPRequestTarget.new()
	target.url = "https://example.com/api/players/{id}"
	target.method = HTTPClient.METHOD_POST
	target.headers = { "Authorization": "Bearer {token}" }
	target.body_type = HTTPRequestTarget.BODY_TYPE_JSON
	target.body_fields = { "name": "{player}", "score": 100 }
	# http.request_target(target, { "id": 7, "token": "abc", "player": "Ada" })

	# Describe the request in the Inspector: a saved target (resources/echo_target.tres).
	var saved: HTTPRequestTarget = load("res://http-requests/resources/echo_target.tres")
	print("Saved target: ", saved.build_url({ "port": 8080, "id": 7, "term": "red apple" }))

	# Check what will be sent.
	target.query = { "page": 2, "q": "{term}" }
	var variables := { "id": 7, "term": "red apple", "token": "abc", "player": "Ada" }
	print(target.build_url(variables))
	print(target.build_headers(variables))
	print(target.build_body(variables).get_string_from_utf8())
	print(HTTPRequestTarget.encode_query({ "q": "red apple", "tag": ["a", "b"], "flag": null }))

	# Give one request a shorter timeout.
	target.timeout = 5.0
	target.max_redirects = 2
	# http.request_target(target, variables)
	get_tree().quit()


func _on_response_received(response: HTTPResponse) -> void:
	if response.is_success():
		print(response.status_code, " ", response.get_json())
	else:
		print("Request failed: ", response.result, " ", response.status_code)
