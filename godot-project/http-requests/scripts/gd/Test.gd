extends FeatureTest
## Full API test of HTTP Requests (HTTPRequestTarget, HTTPResponse, HTTPRequest.request_target and response_received).
## Same tests as scripts/cs/Test.cs. No real network: a loopback TCPServer answers every request with a fixed response
## and keeps the raw request text for the tests.

const TARGET_PATH := "res://http-requests/resources/echo_target.tres"
const RESPONSE_BODY := "{\"ok\":true,\"items\":[1,2]}"
const METHOD_NAMES := ["GET", "HEAD", "POST", "PUT", "DELETE", "OPTIONS", "TRACE", "CONNECT", "PATCH"]
const MAX_FRAMES := 600

var _server := TCPServer.new()
var _peer: StreamPeerTCP
var _buffer := PackedByteArray()
var _last_request := ""
var _port: int


func _run_tests() -> void:
	_test_defaults()
	_test_fill_placeholders()
	_test_build_url()
	_test_encode_query()
	_test_content_types()
	_test_build_headers()
	_test_build_body()
	_server.listen(0, "127.0.0.1")
	_port = _server.get_local_port()
	await _test_saved_target()
	await _test_methods()
	await _test_form_request()
	await _test_plain_request()
	await _test_node_values_unchanged()
	_test_null_target()
	_server.stop()


func _test_defaults() -> void:
	var target := HTTPRequestTarget.new()
	expect_equal(target.url, "", "default url is empty")
	expect_equal(target.method, HTTPClient.METHOD_GET, "default method is GET")
	expect_equal(target.query, {}, "default query is empty")
	expect_equal(target.headers, {}, "default headers are empty")
	expect_equal(target.body_type, HTTPRequestTarget.BODY_TYPE_NONE, "default body type is NONE")
	expect_equal(target.body_fields, {}, "default body fields are empty")
	expect_equal(target.body_text, "", "default body text is empty")
	expect(target.body_data.is_empty(), "default body data is empty")
	expect_equal(target.timeout, 0.0, "default timeout is 0")
	expect(target.accept_gzip, "default accept_gzip is true")
	expect_equal(target.max_redirects, 8, "default max_redirects is 8")


func _test_fill_placeholders() -> void:
	var filled: String = HTTPRequestTarget.fill_placeholders("{a} and {b} and {c}", { "a": 1, "b": "two" })
	expect_equal(filled, "1 and two and {c}", "fill_placeholders fills known names and keeps unknown ones")


func _test_build_url() -> void:
	var target := HTTPRequestTarget.new()
	target.url = "http://host/{section}/items"
	target.query = { "q": "{term}", "tag": ["a", "b&c"], "flag": null }
	var variables := { "section": "shop", "term": "red apple" }
	var url: String = target.build_url(variables)
	expect_equal(url, "http://host/shop/items?q=red%20apple&tag=a&tag=b%26c&flag", "build_url fills placeholders and encodes the query")


func _test_encode_query() -> void:
	var simple: String = HTTPRequestTarget.encode_query({ "a": 1, "b": "x y" })
	expect_equal(simple, "a=1&b=x%20y", "encode_query encodes values")
	var fields := { "q": "red apple", "tag": ["a", "b"], "flag": null }
	var encoded: String = HTTPRequestTarget.encode_query(fields)
	expect_equal(encoded, HTTPClient.new().query_string_from_dict(fields), "encode_query gives the same as HTTPClient.query_string_from_dict")


func _test_content_types() -> void:
	expect_equal(HTTPRequestTarget.get_content_type_for(HTTPRequestTarget.BODY_TYPE_NONE), "", "NONE has no Content-Type")
	expect_equal(HTTPRequestTarget.get_content_type_for(HTTPRequestTarget.BODY_TYPE_TEXT), "text/plain; charset=utf-8", "TEXT Content-Type")
	expect_equal(HTTPRequestTarget.get_content_type_for(HTTPRequestTarget.BODY_TYPE_JSON), "application/json", "JSON Content-Type")
	expect_equal(HTTPRequestTarget.get_content_type_for(HTTPRequestTarget.BODY_TYPE_FORM_URLENCODED), "application/x-www-form-urlencoded", "FORM_URLENCODED Content-Type")
	expect_equal(HTTPRequestTarget.get_content_type_for(HTTPRequestTarget.BODY_TYPE_BINARY), "application/octet-stream", "BINARY Content-Type")
	var target := HTTPRequestTarget.new()
	target.body_type = HTTPRequestTarget.BODY_TYPE_JSON
	expect_equal(target.get_content_type(), "application/json", "get_content_type follows body_type")


func _test_build_headers() -> void:
	var target := HTTPRequestTarget.new()
	target.headers = { "Authorization": "Bearer {token}" }
	target.body_type = HTTPRequestTarget.BODY_TYPE_JSON
	var headers: PackedStringArray = target.build_headers({ "token": "abc" })
	expect(headers.has("Authorization: Bearer abc"), "placeholder in a header value is filled")
	expect(headers.has("Content-Type: application/json"), "Content-Type comes from the body type")
	target.headers = { "content-type": "text/csv" }
	headers = target.build_headers()
	expect(not headers.has("Content-Type: application/json"), "an own Content-Type header wins")


func _test_build_body() -> void:
	var target := HTTPRequestTarget.new()
	expect(target.build_body().is_empty(), "NONE builds an empty body")

	target.body_type = HTTPRequestTarget.BODY_TYPE_JSON
	target.body_fields = { "name": "{player}", "tags": ["{term}"] }
	var json: String = target.build_body({ "player": "Ada \"A\"", "term": "x" }).get_string_from_utf8()
	expect_equal(json, "{\"name\":\"Ada \\\"A\\\"\",\"tags\":[\"x\"]}", "JSON body keeps the order and escapes placeholder values")
	var parsed: Dictionary = JSON.parse_string(json)
	expect_equal(parsed["name"], "Ada \"A\"", "JSON body parses back")

	target.body_type = HTTPRequestTarget.BODY_TYPE_FORM_URLENCODED
	target.body_fields = { "user": "{player}", "score": 5 }
	var form: String = target.build_body({ "player": "Ada Lovelace" }).get_string_from_utf8()
	expect_equal(form, "user=Ada%20Lovelace&score=5", "form body is URL-encoded")

	target.body_type = HTTPRequestTarget.BODY_TYPE_TEXT
	target.body_text = "Hello {player}!"
	var text: String = target.build_body({ "player": "Ada" }).get_string_from_utf8()
	expect_equal(text, "Hello Ada!", "placeholder in a text body is filled")

	target.body_type = HTTPRequestTarget.BODY_TYPE_BINARY
	target.body_data = PackedByteArray([0, 255, 7])
	expect_equal(target.build_body(), PackedByteArray([0, 255, 7]), "binary body is sent unchanged")


func _test_saved_target() -> void:
	var saved: HTTPRequestTarget = load(TARGET_PATH)
	expect_equal(saved.method, HTTPClient.METHOD_POST, "the saved target has method POST")
	var variables := { "port": _port, "id": 7, "term": "red apple", "player": "Ada" }
	var response: HTTPResponse = await _send(saved, variables)
	expect(response.is_success(), "response is a success")
	expect_equal(response.status_code, 200, "status code is 200")
	expect_equal(response.result, HTTPRequest.RESULT_SUCCESS, "result is RESULT_SUCCESS")
	expect(response.target == saved, "response.target is the sent target")
	expect_equal(response.get_header("x-echo"), "yes", "get_header reads a header without case")
	expect_equal(response.get_header("x-missing", "none"), "none", "get_header gives the default for a missing header")
	expect_equal(response.headers["x-echo"], "yes", "headers holds the response headers")
	expect(response.raw_headers.has("X-Echo: yes"), "raw_headers holds the header lines")
	expect_equal(response.body.get_string_from_utf8(), RESPONSE_BODY, "body holds the response bytes")
	expect_equal(response.get_text(), RESPONSE_BODY, "get_text gives the body as text")
	var json: Dictionary = response.get_json()
	expect_equal(json["items"], [1.0, 2.0], "get_json parses the body")

	expect(_last_request.begins_with("POST /players/7?page=2&q=red%20apple HTTP/1.1\r\n"), "request line has the verb, URL placeholders and the encoded query")
	expect(_last_request.contains("X-Player: Ada\r\n"), "the header placeholder is filled")
	expect(_last_request.contains("Content-Type: application/json\r\n"), "Content-Type is sent")
	expect(_last_request.ends_with("\r\n\r\n{\"name\":\"Ada\",\"score\":100}"), "the JSON body is sent with filled placeholders")


func _test_methods() -> void:
	var target := HTTPRequestTarget.new()
	target.url = "http://127.0.0.1:%d/verb" % _port
	for method in [HTTPClient.METHOD_GET, HTTPClient.METHOD_HEAD, HTTPClient.METHOD_POST, HTTPClient.METHOD_PUT,
			HTTPClient.METHOD_DELETE, HTTPClient.METHOD_OPTIONS, HTTPClient.METHOD_TRACE, HTTPClient.METHOD_PATCH]:
		target.method = method
		var response: HTTPResponse = await _send(target, {})
		var verb: String = METHOD_NAMES[method]
		expect_equal(response.status_code, 200, "%s gets a response" % verb)
		expect(_last_request.begins_with("%s /verb HTTP/1.1\r\n" % verb), "%s is sent with the right verb" % verb)
	# CONNECT needs an authority ("host:port") as request target; HTTPRequest sends paths, so HTTPClient refuses it.
	target.method = HTTPClient.METHOD_CONNECT
	expect_equal(target.method, HTTPClient.METHOD_CONNECT, "CONNECT can be selected")


func _test_form_request() -> void:
	var target := HTTPRequestTarget.new()
	target.url = "http://127.0.0.1:{port}/form"
	target.method = HTTPClient.METHOD_PUT
	target.body_type = HTTPRequestTarget.BODY_TYPE_FORM_URLENCODED
	target.body_fields = { "user": "{player}", "level": 3 }
	var response: HTTPResponse = await _send(target, { "port": _port, "player": "Ada Lovelace" })
	expect(response.is_success(), "the form request succeeds")
	expect(_last_request.contains("Content-Type: application/x-www-form-urlencoded\r\n"), "the form Content-Type is sent")
	expect(_last_request.ends_with("\r\n\r\nuser=Ada%20Lovelace&level=3"), "the form body is sent encoded")


func _test_plain_request() -> void:
	var http := HTTPRequest.new()
	add_child(http)
	var responses: Array[HTTPResponse] = []
	http.response_received.connect(responses.append)
	var error: Error = http.request("http://127.0.0.1:%d/plain" % _port)
	expect_equal(error, OK, "a plain request starts")
	await _wait_for(responses)
	expect_equal(responses.size(), 1, "a plain request emits response_received")
	expect(responses[0].target == null, "a plain request has no target in the response")
	http.queue_free()


func _test_node_values_unchanged() -> void:
	var http := HTTPRequest.new()
	add_child(http)
	http.timeout = 20.0
	http.max_redirects = 3
	var target := HTTPRequestTarget.new()
	target.url = "http://127.0.0.1:%d/own" % _port
	target.timeout = 5.0
	target.max_redirects = 2
	target.accept_gzip = false
	var responses: Array[HTTPResponse] = []
	http.response_received.connect(responses.append)
	http.request_target(target)
	await _wait_for(responses)
	expect_equal(http.timeout, 20.0, "request_target leaves the timeout of the node unchanged")
	expect_equal(http.max_redirects, 3, "request_target leaves max_redirects of the node unchanged")
	expect(http.accept_gzip, "request_target leaves accept_gzip of the node unchanged")
	http.queue_free()


func _test_null_target() -> void:
	var http := HTTPRequest.new()
	add_child(http)
	var error: Error = http.request_target(null)
	expect_equal(error, ERR_INVALID_PARAMETER, "request_target(null) returns ERR_INVALID_PARAMETER")
	http.queue_free()


func _send(target: HTTPRequestTarget, variables: Dictionary) -> HTTPResponse:
	var http := HTTPRequest.new()
	add_child(http)
	var responses: Array[HTTPResponse] = []
	http.response_received.connect(responses.append)
	http.request_target(target, variables)
	await _wait_for(responses)
	http.queue_free()
	return responses[0]


func _wait_for(responses: Array[HTTPResponse]) -> void:
	var frames := 0
	while responses.is_empty() and frames < MAX_FRAMES:
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
	var available: int = _peer.get_available_bytes()
	if available > 0:
		_buffer.append_array(_peer.get_data(available)[1])
	var text: String = _buffer.get_string_from_utf8()
	var header_end: int = text.find("\r\n\r\n")
	if header_end == -1:
		return
	var content_length := 0
	for line in text.substr(0, header_end).split("\r\n"):
		if line.to_lower().begins_with("content-length:"):
			content_length = line.substr(15).strip_edges().to_int()
	if _buffer.size() < header_end + 4 + content_length:
		return
	_last_request = text
	var body: String = RESPONSE_BODY
	if text.begins_with("HEAD "):
		body = ""
	var length: int = body.to_utf8_buffer().size()
	var response := "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nX-Echo: yes\r\nContent-Length: %d\r\nConnection: close\r\n\r\n%s" % [length, body]
	_peer.put_data(response.to_utf8_buffer())
	_buffer.clear()
	_peer = null
