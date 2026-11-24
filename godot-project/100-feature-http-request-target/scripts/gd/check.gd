extends FeatureCheck
## GDScript checks of 100-feature-http-request-target. Check the same things as scripts/cs/Issue100FeatureHttpRequestTargetCheck.cs.
## A loopback TCPServer answers every request with a fixed response and keeps the raw request text for the checks.

const TARGET_PATH := "res://100-feature-http-request-target/resources/echo_target.tres"
const RESPONSE_BODY := "{\"ok\":true,\"items\":[1,2]}"
const METHOD_NAMES := ["GET", "HEAD", "POST", "PUT", "DELETE", "OPTIONS", "TRACE", "CONNECT", "PATCH"]

var _server := TCPServer.new()
var _peer: StreamPeerTCP
var _buffer := PackedByteArray()
var _last_request := ""


func _get_check_name() -> String:
	return "100-feature-http-request-target"


func _run_checks() -> void:
	_check_building()

	if not expect(_server.listen(0, "127.0.0.1") == OK, "loopback server listens"):
		return
	var port := _server.get_local_port()

	await _check_saved_target(port)
	await _check_methods(port)
	await _check_form_and_plain_request(port)

	_server.stop()


func _check_building() -> void:
	var target := HTTPRequestTarget.new()
	expect_equal(target.method, HTTPClient.METHOD_GET, "default method is GET")
	expect_equal(target.body_type, HTTPRequestTarget.BODY_TYPE_NONE, "default body type is NONE")

	target.url = "http://host/{section}/items"
	target.query = { "q": "{term}", "tag": ["a", "b&c"], "flag": null }
	var variables := { "section": "shop", "term": "red apple" }
	expect_equal(target.build_url(variables), "http://host/shop/items?q=red%20apple&tag=a&tag=b%26c&flag", "query is encoded and placeholders are filled")
	expect_equal(HTTPRequestTarget.encode_query({ "a": 1, "b": "x y" }), "a=1&b=x%20y", "encode_query matches HTTPClient rules")
	var fields := { "q": "red apple", "tag": ["a", "b"], "flag": null }
	expect_equal(HTTPRequestTarget.encode_query(fields), HTTPClient.new().query_string_from_dict(fields), "same result as HTTPClient.query_string_from_dict")

	target.headers = { "Authorization": "Bearer {token}" }
	target.body_type = HTTPRequestTarget.BODY_TYPE_JSON
	target.body_fields = { "name": "{player}", "tags": ["{term}"] }
	var headers := target.build_headers({ "token": "abc" })
	expect(headers.has("Authorization: Bearer abc"), "placeholder in header value is filled")
	expect(headers.has("Content-Type: application/json"), "Content-Type comes from body type")

	var body := target.build_body({ "player": "Ada \"A\"", "term": "x" }).get_string_from_utf8()
	expect_equal(body, "{\"name\":\"Ada \\\"A\\\"\",\"tags\":[\"x\"]}", "JSON body keeps order and escapes placeholder values")
	expect_equal(JSON.parse_string(body)["name"], "Ada \"A\"", "JSON body parses back")

	target.body_type = HTTPRequestTarget.BODY_TYPE_FORM_URLENCODED
	target.body_fields = { "user": "{player}", "score": 5 }
	expect_equal(target.build_body({ "player": "Ada Lovelace" }).get_string_from_utf8(), "user=Ada%20Lovelace&score=5", "form body is URL-encoded")
	expect(target.build_headers().has("Content-Type: application/x-www-form-urlencoded"), "form Content-Type")

	target.body_type = HTTPRequestTarget.BODY_TYPE_TEXT
	target.body_text = "Hello {player}!"
	expect_equal(target.build_body({ "player": "Ada" }).get_string_from_utf8(), "Hello Ada!", "placeholder in text body is filled")

	target.headers = { "content-type": "text/csv" }
	expect(not target.build_headers().has("Content-Type: text/plain; charset=utf-8"), "own Content-Type header wins")

	target.body_type = HTTPRequestTarget.BODY_TYPE_BINARY
	target.body_data = PackedByteArray([0, 255, 7])
	expect_equal(target.build_body(), PackedByteArray([0, 255, 7]), "binary body is sent unchanged")


func _check_saved_target(port: int) -> void:
	var saved := load(TARGET_PATH) as HTTPRequestTarget
	if not expect(saved != null, "target loads from .tres"):
		return
	expect_equal(saved.method, HTTPClient.METHOD_POST, "saved method is POST")

	var response := await _send(saved, { "port": port, "id": 7, "term": "red apple", "player": "Ada" })
	if not expect(response != null, "response_received delivers an HTTPResponse"):
		return
	expect(response.is_success(), "response is a success")
	expect_equal(response.status_code, 200, "status code 200")
	expect_equal(response.result, HTTPRequest.RESULT_SUCCESS, "result is RESULT_SUCCESS")
	expect(response.target == saved, "response.target is the sent target")
	expect_equal(response.get_header("x-echo"), "yes", "response header is readable without case")
	expect_equal(response.get_text(), RESPONSE_BODY, "response text")
	var json: Dictionary = response.get_json()
	expect(json.get("ok") == true and json.get("items", []).size() == 2, "response JSON")

	expect(_last_request.begins_with("POST /players/7?page=2&q=red%20apple HTTP/1.1\r\n"), "request line has verb, URL placeholders and encoded query")
	expect(_last_request.contains("X-Player: Ada\r\n"), "header placeholder is filled")
	expect(_last_request.contains("Content-Type: application/json\r\n"), "Content-Type is sent")
	expect(_last_request.ends_with("\r\n\r\n{\"name\":\"Ada\",\"score\":100}"), "JSON body is sent with filled placeholders")


func _check_methods(port: int) -> void:
	var target := HTTPRequestTarget.new()
	target.url = "http://127.0.0.1:%d/verb" % port
	target.timeout = 5.0
	for method in [HTTPClient.METHOD_GET, HTTPClient.METHOD_HEAD, HTTPClient.METHOD_POST, HTTPClient.METHOD_PUT,
			HTTPClient.METHOD_DELETE, HTTPClient.METHOD_OPTIONS, HTTPClient.METHOD_TRACE, HTTPClient.METHOD_PATCH]:
		target.method = method
		var response := await _send(target, {})
		var verb: String = METHOD_NAMES[method]
		expect(response != null and response.status_code == 200, "%s gets a response" % verb)
		expect(_last_request.begins_with("%s /verb HTTP/1.1\r\n" % verb), "%s is sent with the right verb" % verb)
	# CONNECT needs an authority ("host:port") as request target; HTTPRequest sends paths, so HTTPClient refuses it.
	target.method = HTTPClient.METHOD_CONNECT
	expect_equal(target.method, HTTPClient.METHOD_CONNECT, "CONNECT can be selected")


func _check_form_and_plain_request(port: int) -> void:
	var target := HTTPRequestTarget.new()
	target.url = "http://127.0.0.1:{port}/form"
	target.method = HTTPClient.METHOD_PUT
	target.body_type = HTTPRequestTarget.BODY_TYPE_FORM_URLENCODED
	target.body_fields = { "user": "{player}", "level": 3 }
	var response := await _send(target, { "port": port, "player": "Ada Lovelace" })
	expect(response != null and response.is_success(), "form request succeeds")
	expect(_last_request.contains("Content-Type: application/x-www-form-urlencoded\r\n"), "form Content-Type is sent")
	expect(_last_request.ends_with("\r\n\r\nuser=Ada%20Lovelace&level=3"), "form body is sent encoded")

	# Plain request() also emits response_received, without a target.
	var http := HTTPRequest.new()
	add_child(http)
	var plain_response: Array = []
	http.response_received.connect(func(r: HTTPResponse) -> void: plain_response.append(r))
	expect_equal(http.request("http://127.0.0.1:%d/plain" % port), OK, "plain request starts")
	await _wait_for(plain_response)
	expect(plain_response.size() == 1 and plain_response[0].target == null, "plain request emits response_received without target")
	http.queue_free()


func _send(target: HTTPRequestTarget, variables: Dictionary) -> HTTPResponse:
	var http := HTTPRequest.new()
	add_child(http)
	var responses: Array = []
	http.response_received.connect(func(r: HTTPResponse) -> void: responses.append(r))
	var err := http.request_target(target, variables)
	if err != OK:
		printerr("request_target failed: %s" % error_string(err))
		http.queue_free()
		return null
	await _wait_for(responses)
	http.queue_free()
	return responses[0] if not responses.is_empty() else null


func _wait_for(responses: Array) -> void:
	var frames := 0
	while responses.is_empty() and frames < 600:
		_poll_server()
		await get_tree().process_frame
		frames += 1


## Accepts a connection, reads one full request and answers it with a fixed response.
func _poll_server() -> void:
	if _server.is_connection_available():
		_peer = _server.take_connection()
		_buffer.clear()
	if _peer == null:
		return
	_peer.poll()
	var available := _peer.get_available_bytes()
	if available > 0:
		_buffer.append_array(_peer.get_data(available)[1])
	var text := _buffer.get_string_from_utf8()
	var header_end := text.find("\r\n\r\n")
	if header_end == -1:
		return
	var content_length := 0
	for line in text.substr(0, header_end).split("\r\n"):
		if line.to_lower().begins_with("content-length:"):
			content_length = line.substr(15).strip_edges().to_int()
	if _buffer.size() < header_end + 4 + content_length:
		return
	_last_request = text
	var is_head := text.begins_with("HEAD ")
	var body := "" if is_head else RESPONSE_BODY
	var response := "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nX-Echo: yes\r\nContent-Length: %d\r\nConnection: close\r\n\r\n%s" % [body.to_utf8_buffer().size(), body]
	_peer.put_data(response.to_utf8_buffer())
	_buffer.clear()
	_peer = null
