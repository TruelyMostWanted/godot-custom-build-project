using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Godot;
using TruelyInteractive.Checks;
using GodotArray = Godot.Collections.Array;
using GodotDictionary = Godot.Collections.Dictionary;

namespace TruelyInteractive.Issue100FeatureHttpRequestTarget;

/// <summary>C# checks of 100-feature-http-request-target. Check the same things as scripts/gd/check.gd.</summary>
/// <remarks>A loopback TcpServer answers every request with a fixed response and keeps the raw request text for the checks.</remarks>
public partial class Issue100FeatureHttpRequestTargetCheck : FeatureCheckCs
{
    private const string TargetPath = "res://100-feature-http-request-target/resources/echo_target.tres";
    private const string ResponseBody = "{\"ok\":true,\"items\":[1,2]}";

    private static readonly string[] MethodNames = ["GET", "HEAD", "POST", "PUT", "DELETE", "OPTIONS", "TRACE", "CONNECT", "PATCH"];

    private readonly TcpServer _server = new();
    private StreamPeerTcp _peer;
    private readonly List<byte> _buffer = [];
    private string _lastRequest = "";

    protected override string CheckName => "100-feature-http-request-target";

    protected override async Task RunChecks()
    {
        CheckBuilding();

        if (!Expect(_server.Listen(0, "127.0.0.1") == Error.Ok, "loopback server listens"))
        {
            return;
        }
        int port = _server.GetLocalPort();

        await CheckSavedTarget(port);
        await CheckMethods(port);
        await CheckFormAndPlainRequest(port);

        _server.Stop();
    }

    private static string Utf8(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private void CheckBuilding()
    {
        var target = new HttpRequestTarget();
        ExpectEqual(target.Method, HttpClient.Method.Get, "default method is GET");
        ExpectEqual(target.BodyType, HttpRequestTarget.BodyTypeEnum.None, "default body type is NONE");

        target.Url = "http://host/{section}/items";
        target.Query = new GodotDictionary { { "q", "{term}" }, { "tag", new GodotArray { "a", "b&c" } }, { "flag", default } };
        var variables = new GodotDictionary { { "section", "shop" }, { "term", "red apple" } };
        ExpectEqual(target.BuildUrl(variables), "http://host/shop/items?q=red%20apple&tag=a&tag=b%26c&flag", "query is encoded and placeholders are filled");
        ExpectEqual(HttpRequestTarget.EncodeQuery(new GodotDictionary { { "a", 1 }, { "b", "x y" } }), "a=1&b=x%20y", "EncodeQuery matches HttpClient rules");
        var fields = new GodotDictionary { { "q", "red apple" }, { "tag", new GodotArray { "a", "b" } }, { "flag", default } };
        ExpectEqual(HttpRequestTarget.EncodeQuery(fields), new HttpClient().QueryStringFromDict(fields), "same result as HttpClient.QueryStringFromDict");

        target.Headers = new GodotDictionary { { "Authorization", "Bearer {token}" } };
        target.BodyType = HttpRequestTarget.BodyTypeEnum.Json;
        target.BodyFields = new GodotDictionary { { "name", "{player}" }, { "tags", new GodotArray { "{term}" } } };
        string[] headers = target.BuildHeaders(new GodotDictionary { { "token", "abc" } });
        Expect(headers.Contains("Authorization: Bearer abc"), "placeholder in header value is filled");
        Expect(headers.Contains("Content-Type: application/json"), "Content-Type comes from body type");

        string body = Utf8(target.BuildBody(new GodotDictionary { { "player", "Ada \"A\"" }, { "term", "x" } }));
        ExpectEqual(body, "{\"name\":\"Ada \\\"A\\\"\",\"tags\":[\"x\"]}", "JSON body keeps order and escapes placeholder values");
        ExpectEqual((string)Json.ParseString(body).AsGodotDictionary()["name"], "Ada \"A\"", "JSON body parses back");

        target.BodyType = HttpRequestTarget.BodyTypeEnum.FormUrlencoded;
        target.BodyFields = new GodotDictionary { { "user", "{player}" }, { "score", 5 } };
        ExpectEqual(Utf8(target.BuildBody(new GodotDictionary { { "player", "Ada Lovelace" } })), "user=Ada%20Lovelace&score=5", "form body is URL-encoded");
        Expect(target.BuildHeaders().Contains("Content-Type: application/x-www-form-urlencoded"), "form Content-Type");

        target.BodyType = HttpRequestTarget.BodyTypeEnum.Text;
        target.BodyText = "Hello {player}!";
        ExpectEqual(Utf8(target.BuildBody(new GodotDictionary { { "player", "Ada" } })), "Hello Ada!", "placeholder in text body is filled");

        target.Headers = new GodotDictionary { { "content-type", "text/csv" } };
        Expect(!target.BuildHeaders().Contains("Content-Type: text/plain; charset=utf-8"), "own Content-Type header wins");

        target.BodyType = HttpRequestTarget.BodyTypeEnum.Binary;
        target.BodyData = [0, 255, 7];
        Expect(target.BuildBody().SequenceEqual(new byte[] { 0, 255, 7 }), "binary body is sent unchanged");
    }

    private async Task CheckSavedTarget(int port)
    {
        var saved = GD.Load<HttpRequestTarget>(TargetPath);
        if (!Expect(saved != null, "target loads from .tres"))
        {
            return;
        }
        ExpectEqual(saved.Method, HttpClient.Method.Post, "saved method is POST");

        HttpResponse response = await Send(saved, new GodotDictionary { { "port", port }, { "id", 7 }, { "term", "red apple" }, { "player", "Ada" } });
        if (!Expect(response != null, "ResponseReceived delivers an HttpResponse"))
        {
            return;
        }
        Expect(response.IsSuccess(), "response is a success");
        ExpectEqual(response.StatusCode, 200, "status code 200");
        ExpectEqual(response.Result, HttpRequest.Result.Success, "result is Success");
        Expect(response.Target == saved, "response.Target is the sent target");
        ExpectEqual(response.GetHeader("x-echo"), "yes", "response header is readable without case");
        ExpectEqual(response.GetText(), ResponseBody, "response text");
        GodotDictionary json = response.GetJson().AsGodotDictionary();
        Expect(json["ok"].AsBool() && json["items"].AsGodotArray().Count == 2, "response JSON");

        Expect(_lastRequest.StartsWith("POST /players/7?page=2&q=red%20apple HTTP/1.1\r\n"), "request line has verb, URL placeholders and encoded query");
        Expect(_lastRequest.Contains("X-Player: Ada\r\n"), "header placeholder is filled");
        Expect(_lastRequest.Contains("Content-Type: application/json\r\n"), "Content-Type is sent");
        Expect(_lastRequest.EndsWith("\r\n\r\n{\"name\":\"Ada\",\"score\":100}"), "JSON body is sent with filled placeholders");
    }

    private async Task CheckMethods(int port)
    {
        var target = new HttpRequestTarget { Url = $"http://127.0.0.1:{port}/verb", Timeout = 5.0 };
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
            Expect(response != null && response.StatusCode == 200, $"{verb} gets a response");
            Expect(_lastRequest.StartsWith($"{verb} /verb HTTP/1.1\r\n"), $"{verb} is sent with the right verb");
        }
        // CONNECT needs an authority ("host:port") as request target; HttpRequest sends paths, so HttpClient refuses it.
        target.Method = HttpClient.Method.Connect;
        ExpectEqual(target.Method, HttpClient.Method.Connect, "CONNECT can be selected");
    }

    private async Task CheckFormAndPlainRequest(int port)
    {
        var target = new HttpRequestTarget
        {
            Url = "http://127.0.0.1:{port}/form",
            Method = HttpClient.Method.Put,
            BodyType = HttpRequestTarget.BodyTypeEnum.FormUrlencoded,
            BodyFields = new GodotDictionary { { "user", "{player}" }, { "level", 3 } },
        };
        HttpResponse response = await Send(target, new GodotDictionary { { "port", port }, { "player", "Ada Lovelace" } });
        Expect(response != null && response.IsSuccess(), "form request succeeds");
        Expect(_lastRequest.Contains("Content-Type: application/x-www-form-urlencoded\r\n"), "form Content-Type is sent");
        Expect(_lastRequest.EndsWith("\r\n\r\nuser=Ada%20Lovelace&level=3"), "form body is sent encoded");

        // Plain Request() also emits ResponseReceived, without a target.
        var http = new HttpRequest();
        AddChild(http);
        var plainResponses = new List<HttpResponse>();
        http.ResponseReceived += r => plainResponses.Add(r);
        ExpectEqual(http.Request($"http://127.0.0.1:{port}/plain"), Error.Ok, "plain request starts");
        await WaitFor(plainResponses);
        Expect(plainResponses.Count == 1 && plainResponses[0].Target == null, "plain request emits ResponseReceived without target");
        http.QueueFree();
    }

    private async Task<HttpResponse> Send(HttpRequestTarget target, GodotDictionary variables)
    {
        var http = new HttpRequest();
        AddChild(http);
        var responses = new List<HttpResponse>();
        http.ResponseReceived += r => responses.Add(r);
        Error err = http.RequestTarget(target, variables);
        if (err != Error.Ok)
        {
            GD.PrintErr($"RequestTarget failed: {err}");
            http.QueueFree();
            return null;
        }
        await WaitFor(responses);
        http.QueueFree();
        return responses.Count > 0 ? responses[0] : null;
    }

    private async Task WaitFor(List<HttpResponse> responses)
    {
        for (int frames = 0; responses.Count == 0 && frames < 600; frames++)
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
        string text = Encoding.UTF8.GetString(_buffer.ToArray());
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
        string body = text.StartsWith("HEAD ") ? "" : ResponseBody;
        string response = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nX-Echo: yes\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";
        _peer.PutData(Encoding.UTF8.GetBytes(response));
        _buffer.Clear();
        _peer = null;
    }
}
