using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;
using GodotArray = Godot.Collections.Array;
using GodotDictionary = Godot.Collections.Dictionary;

namespace TruelyInteractive.HttpRequests;

/// <summary>Full API test of HTTP Requests (HttpRequestTarget, HttpResponse, HttpRequest.RequestTarget and ResponseReceived).
/// Same tests as scripts/gd/Test.gd. No real network: a loopback TcpServer answers every request with a fixed response
/// and keeps the raw request text for the tests.</summary>
public partial class Test : FeatureTest
{
    private const string TargetPath = "res://http-requests/resources/echo_target.tres";
    private const string ResponseBody = "{\"ok\":true,\"items\":[1,2]}";
    private const int MaxFrames = 600;

    private static readonly string[] MethodNames = ["GET", "HEAD", "POST", "PUT", "DELETE", "OPTIONS", "TRACE", "CONNECT", "PATCH"];

    private readonly TcpServer _server = new();
    private readonly List<byte> _buffer = [];
    private StreamPeerTcp _peer;
    private string _lastRequest = "";
    private int _port;

    protected override async Task RunTests()
    {
        TestDefaults();
        TestFillPlaceholders();
        TestBuildUrl();
        TestEncodeQuery();
        TestContentTypes();
        TestBuildHeaders();
        TestBuildBody();
        _server.Listen(0, "127.0.0.1");
        _port = _server.GetLocalPort();
        await TestSavedTarget();
        await TestMethods();
        await TestFormRequest();
        await TestPlainRequest();
        await TestNodeValuesUnchanged();
        TestNullTarget();
        _server.Stop();
    }

    private static string Utf8(byte[] bytes)
    {
        return Encoding.UTF8.GetString(bytes);
    }

    private void TestDefaults()
    {
        var target = new HttpRequestTarget();
        ExpectEqual(target.Url, "", "default url is empty");
        ExpectEqual(target.Method, HttpClient.Method.Get, "default method is GET");
        ExpectEqual(target.Query.Count, 0, "default query is empty");
        ExpectEqual(target.Headers.Count, 0, "default headers are empty");
        ExpectEqual(target.BodyType, HttpRequestTarget.BodyTypeEnum.None, "default body type is NONE");
        ExpectEqual(target.BodyFields.Count, 0, "default body fields are empty");
        ExpectEqual(target.BodyText, "", "default body text is empty");
        ExpectEqual(target.BodyData.Length, 0, "default body data is empty");
        ExpectEqual(target.Timeout, 0.0, "default timeout is 0");
        Expect(target.AcceptGZip, "default AcceptGZip is true");
        ExpectEqual(target.MaxRedirects, 8, "default MaxRedirects is 8");
    }

    private void TestFillPlaceholders()
    {
        var variables = new GodotDictionary { { "a", 1 }, { "b", "two" } };
        string filled = HttpRequestTarget.FillPlaceholders("{a} and {b} and {c}", variables);
        ExpectEqual(filled, "1 and two and {c}", "FillPlaceholders fills known names and keeps unknown ones");
    }

    private void TestBuildUrl()
    {
        var target = new HttpRequestTarget { Url = "http://host/{section}/items" };
        target.Query = new GodotDictionary { { "q", "{term}" }, { "tag", new GodotArray { "a", "b&c" } }, { "flag", default } };
        var variables = new GodotDictionary { { "section", "shop" }, { "term", "red apple" } };
        string url = target.BuildUrl(variables);
        ExpectEqual(url, "http://host/shop/items?q=red%20apple&tag=a&tag=b%26c&flag", "BuildUrl fills placeholders and encodes the query");
    }

    private void TestEncodeQuery()
    {
        var simple = new GodotDictionary { { "a", 1 }, { "b", "x y" } };
        ExpectEqual(HttpRequestTarget.EncodeQuery(simple), "a=1&b=x%20y", "EncodeQuery encodes values");
        var fields = new GodotDictionary { { "q", "red apple" }, { "tag", new GodotArray { "a", "b" } }, { "flag", default } };
        string encoded = HttpRequestTarget.EncodeQuery(fields);
        ExpectEqual(encoded, new HttpClient().QueryStringFromDict(fields), "EncodeQuery gives the same as HttpClient.QueryStringFromDict");
    }

    private void TestContentTypes()
    {
        ExpectEqual(HttpRequestTarget.GetContentTypeFor(HttpRequestTarget.BodyTypeEnum.None), "", "NONE has no Content-Type");
        ExpectEqual(HttpRequestTarget.GetContentTypeFor(HttpRequestTarget.BodyTypeEnum.Text), "text/plain; charset=utf-8", "TEXT Content-Type");
        ExpectEqual(HttpRequestTarget.GetContentTypeFor(HttpRequestTarget.BodyTypeEnum.Json), "application/json", "JSON Content-Type");
        ExpectEqual(HttpRequestTarget.GetContentTypeFor(HttpRequestTarget.BodyTypeEnum.FormUrlencoded), "application/x-www-form-urlencoded", "FORM_URLENCODED Content-Type");
        ExpectEqual(HttpRequestTarget.GetContentTypeFor(HttpRequestTarget.BodyTypeEnum.Binary), "application/octet-stream", "BINARY Content-Type");
        var target = new HttpRequestTarget { BodyType = HttpRequestTarget.BodyTypeEnum.Json };
        ExpectEqual(target.GetContentType(), "application/json", "GetContentType follows BodyType");
    }

    private void TestBuildHeaders()
    {
        var target = new HttpRequestTarget { BodyType = HttpRequestTarget.BodyTypeEnum.Json };
        target.Headers = new GodotDictionary { { "Authorization", "Bearer {token}" } };
        string[] headers = target.BuildHeaders(new GodotDictionary { { "token", "abc" } });
        Expect(headers.Contains("Authorization: Bearer abc"), "placeholder in a header value is filled");
        Expect(headers.Contains("Content-Type: application/json"), "Content-Type comes from the body type");
        target.Headers = new GodotDictionary { { "content-type", "text/csv" } };
        headers = target.BuildHeaders();
        Expect(!headers.Contains("Content-Type: application/json"), "an own Content-Type header wins");
    }

    private void TestBuildBody()
    {
        var target = new HttpRequestTarget();
        ExpectEqual(target.BuildBody().Length, 0, "NONE builds an empty body");

        target.BodyType = HttpRequestTarget.BodyTypeEnum.Json;
        target.BodyFields = new GodotDictionary { { "name", "{player}" }, { "tags", new GodotArray { "{term}" } } };
        string json = Utf8(target.BuildBody(new GodotDictionary { { "player", "Ada \"A\"" }, { "term", "x" } }));
        ExpectEqual(json, "{\"name\":\"Ada \\\"A\\\"\",\"tags\":[\"x\"]}", "JSON body keeps the order and escapes placeholder values");
        GodotDictionary parsed = Json.ParseString(json).AsGodotDictionary();
        ExpectEqual((string)parsed["name"], "Ada \"A\"", "JSON body parses back");

        target.BodyType = HttpRequestTarget.BodyTypeEnum.FormUrlencoded;
        target.BodyFields = new GodotDictionary { { "user", "{player}" }, { "score", 5 } };
        string form = Utf8(target.BuildBody(new GodotDictionary { { "player", "Ada Lovelace" } }));
        ExpectEqual(form, "user=Ada%20Lovelace&score=5", "form body is URL-encoded");

        target.BodyType = HttpRequestTarget.BodyTypeEnum.Text;
        target.BodyText = "Hello {player}!";
        string text = Utf8(target.BuildBody(new GodotDictionary { { "player", "Ada" } }));
        ExpectEqual(text, "Hello Ada!", "placeholder in a text body is filled");

        target.BodyType = HttpRequestTarget.BodyTypeEnum.Binary;
        target.BodyData = [0, 255, 7];
        Expect(target.BuildBody().SequenceEqual(new byte[] { 0, 255, 7 }), "binary body is sent unchanged");
    }

    private async Task TestSavedTarget()
    {
        var saved = GD.Load<HttpRequestTarget>(TargetPath);
        ExpectEqual(saved.Method, HttpClient.Method.Post, "the saved target has method POST");
        var variables = new GodotDictionary { { "port", _port }, { "id", 7 }, { "term", "red apple" }, { "player", "Ada" } };
        HttpResponse response = await Send(saved, variables);
        Expect(response.IsSuccess(), "response is a success");
        ExpectEqual(response.StatusCode, 200, "status code is 200");
        ExpectEqual(response.Result, HttpRequest.Result.Success, "result is Success");
        Expect(response.Target == saved, "response.Target is the sent target");
        ExpectEqual(response.GetHeader("x-echo"), "yes", "GetHeader reads a header without case");
        ExpectEqual(response.GetHeader("x-missing", "none"), "none", "GetHeader gives the default for a missing header");
        ExpectEqual((string)response.Headers["x-echo"], "yes", "Headers holds the response headers");
        Expect(response.RawHeaders.Contains("X-Echo: yes"), "RawHeaders holds the header lines");
        ExpectEqual(Utf8(response.Body), ResponseBody, "Body holds the response bytes");
        ExpectEqual(response.GetText(), ResponseBody, "GetText gives the body as text");
        GodotDictionary json = response.GetJson().AsGodotDictionary();
        ExpectEqual(json["items"].AsGodotArray().Count, 2, "GetJson parses the body");

        Expect(_lastRequest.StartsWith("POST /players/7?page=2&q=red%20apple HTTP/1.1\r\n"), "request line has the verb, URL placeholders and the encoded query");
        Expect(_lastRequest.Contains("X-Player: Ada\r\n"), "the header placeholder is filled");
        Expect(_lastRequest.Contains("Content-Type: application/json\r\n"), "Content-Type is sent");
        Expect(_lastRequest.EndsWith("\r\n\r\n{\"name\":\"Ada\",\"score\":100}"), "the JSON body is sent with filled placeholders");
    }

    private async Task TestMethods()
    {
        var target = new HttpRequestTarget { Url = $"http://127.0.0.1:{_port}/verb" };
        HttpClient.Method[] methods =
        [
            HttpClient.Method.Get, HttpClient.Method.Head, HttpClient.Method.Post, HttpClient.Method.Put,
            HttpClient.Method.Delete, HttpClient.Method.Options, HttpClient.Method.Trace, HttpClient.Method.Patch,
        ];
        foreach (HttpClient.Method method in methods)
        {
            target.Method = method;
            HttpResponse response = await Send(target, new GodotDictionary());
            string verb = MethodNames[(int)method];
            ExpectEqual(response.StatusCode, 200, $"{verb} gets a response");
            Expect(_lastRequest.StartsWith($"{verb} /verb HTTP/1.1\r\n"), $"{verb} is sent with the right verb");
        }
        // CONNECT needs an authority ("host:port") as request target; HttpRequest sends paths, so HttpClient refuses it.
        target.Method = HttpClient.Method.Connect;
        ExpectEqual(target.Method, HttpClient.Method.Connect, "CONNECT can be selected");
    }

    private async Task TestFormRequest()
    {
        var target = new HttpRequestTarget
        {
            Url = "http://127.0.0.1:{port}/form",
            Method = HttpClient.Method.Put,
            BodyType = HttpRequestTarget.BodyTypeEnum.FormUrlencoded,
            BodyFields = new GodotDictionary { { "user", "{player}" }, { "level", 3 } },
        };
        HttpResponse response = await Send(target, new GodotDictionary { { "port", _port }, { "player", "Ada Lovelace" } });
        Expect(response.IsSuccess(), "the form request succeeds");
        Expect(_lastRequest.Contains("Content-Type: application/x-www-form-urlencoded\r\n"), "the form Content-Type is sent");
        Expect(_lastRequest.EndsWith("\r\n\r\nuser=Ada%20Lovelace&level=3"), "the form body is sent encoded");
    }

    private async Task TestPlainRequest()
    {
        var http = new HttpRequest();
        AddChild(http);
        var responses = new List<HttpResponse>();
        http.ResponseReceived += responses.Add;
        Error error = http.Request($"http://127.0.0.1:{_port}/plain");
        ExpectEqual(error, Error.Ok, "a plain request starts");
        await WaitFor(responses);
        ExpectEqual(responses.Count, 1, "a plain request emits ResponseReceived");
        Expect(responses[0].Target == null, "a plain request has no target in the response");
        http.QueueFree();
    }

    private async Task TestNodeValuesUnchanged()
    {
        var http = new HttpRequest { Timeout = 20.0, MaxRedirects = 3 };
        AddChild(http);
        var target = new HttpRequestTarget
        {
            Url = $"http://127.0.0.1:{_port}/own",
            Timeout = 5.0,
            MaxRedirects = 2,
            AcceptGZip = false,
        };
        var responses = new List<HttpResponse>();
        http.ResponseReceived += responses.Add;
        http.RequestTarget(target);
        await WaitFor(responses);
        ExpectEqual(http.Timeout, 20.0, "RequestTarget leaves the timeout of the node unchanged");
        ExpectEqual(http.MaxRedirects, 3, "RequestTarget leaves MaxRedirects of the node unchanged");
        Expect(http.AcceptGZip, "RequestTarget leaves AcceptGZip of the node unchanged");
        http.QueueFree();
    }

    private void TestNullTarget()
    {
        var http = new HttpRequest();
        AddChild(http);
        Error error = http.RequestTarget(null);
        ExpectEqual(error, Error.InvalidParameter, "RequestTarget(null) returns InvalidParameter");
        http.QueueFree();
    }

    private async Task<HttpResponse> Send(HttpRequestTarget target, GodotDictionary variables)
    {
        var http = new HttpRequest();
        AddChild(http);
        var responses = new List<HttpResponse>();
        http.ResponseReceived += responses.Add;
        http.RequestTarget(target, variables);
        await WaitFor(responses);
        http.QueueFree();
        return responses[0];
    }

    private async Task WaitFor(List<HttpResponse> responses)
    {
        for (int frames = 0; responses.Count == 0 && frames < MaxFrames; frames++)
        {
            PollServer();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    /// <summary>Accepts a connection, reads one full request and answers it with a fixed response.</summary>
    private void PollServer()
    {
        if (_server.IsConnectionAvailable())
        {
            _peer = _server.TakeConnection();
            _buffer.Clear();
        }
        if (_peer == null)
        {
            return;
        }
        _peer.Poll();
        int available = _peer.GetAvailableBytes();
        if (available > 0)
        {
            _buffer.AddRange(_peer.GetData(available)[1].AsByteArray());
        }
        string text = Utf8(_buffer.ToArray());
        int headerEnd = text.IndexOf("\r\n\r\n", System.StringComparison.Ordinal);
        if (headerEnd == -1)
        {
            return;
        }
        int contentLength = 0;
        foreach (string line in text[..headerEnd].Split("\r\n"))
        {
            if (line.StartsWith("content-length:", System.StringComparison.OrdinalIgnoreCase))
            {
                contentLength = int.Parse(line[15..].Trim());
            }
        }
        if (_buffer.Count < headerEnd + 4 + contentLength)
        {
            return;
        }
        _lastRequest = text;
        string body = ResponseBody;
        if (text.StartsWith("HEAD "))
        {
            body = "";
        }
        int length = Encoding.UTF8.GetByteCount(body);
        string response = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nX-Echo: yes\r\nContent-Length: {length}\r\nConnection: close\r\n\r\n{body}";
        _peer.PutData(Encoding.UTF8.GetBytes(response));
        _buffer.Clear();
        _peer = null;
    }
}
