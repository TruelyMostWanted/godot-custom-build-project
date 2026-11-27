using Godot;

namespace TruelyInteractive.HttpRequests;

/// <summary>Minimal example of HTTP Requests: the snippets of the wiki guide, in order. Same as scripts/gd/Example.gd.</summary>
/// <remarks>RequestTarget() is not called: the example sends nothing over the network. Build*() shows what would be sent.</remarks>
public partial class Example : Node
{
    public override void _Ready()
    {
        // Post a score as JSON.
        var http = new HttpRequest();
        AddChild(http);
        http.ResponseReceived += OnResponseReceived;

        var target = new HttpRequestTarget
        {
            Url = "https://example.com/api/players/{id}",
            Method = HttpClient.Method.Post,
            Headers = new Godot.Collections.Dictionary { { "Authorization", "Bearer {token}" } },
            BodyType = HttpRequestTarget.BodyTypeEnum.Json,
            BodyFields = new Godot.Collections.Dictionary { { "name", "{player}" }, { "score", 100 } },
        };
        // http.RequestTarget(target, new Godot.Collections.Dictionary { { "id", 7 }, { "token", "abc" }, { "player", "Ada" } });

        // Describe the request in the Inspector: a saved target (resources/echo_target.tres).
        var saved = GD.Load<HttpRequestTarget>("res://http-requests/resources/echo_target.tres");
        var savedVariables = new Godot.Collections.Dictionary { { "port", 8080 }, { "id", 7 }, { "term", "red apple" } };
        GD.Print("Saved target: ", saved.BuildUrl(savedVariables));

        // Check what will be sent.
        target.Query = new Godot.Collections.Dictionary { { "page", 2 }, { "q", "{term}" } };
        var variables = new Godot.Collections.Dictionary { { "id", 7 }, { "term", "red apple" }, { "token", "abc" }, { "player", "Ada" } };
        GD.Print(target.BuildUrl(variables));
        GD.Print(target.BuildHeaders(variables));
        GD.Print(target.BuildBody(variables).GetStringFromUtf8());
        var fields = new Godot.Collections.Dictionary
        {
            { "q", "red apple" },
            { "tag", new Godot.Collections.Array { "a", "b" } },
            { "flag", default },
        };
        GD.Print(HttpRequestTarget.EncodeQuery(fields));

        // Give one request a shorter timeout.
        target.Timeout = 5.0;
        target.MaxRedirects = 2;
        // http.RequestTarget(target, variables);
        GetTree().Quit();
    }

    private static void OnResponseReceived(HttpResponse response)
    {
        if (response.IsSuccess())
        {
            GD.Print(response.StatusCode, " ", response.GetJson());
        }
        else
        {
            GD.Print("Request failed: ", response.Result, " ", response.StatusCode);
        }
    }
}
