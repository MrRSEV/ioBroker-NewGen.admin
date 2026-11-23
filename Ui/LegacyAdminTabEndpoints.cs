using System.Text;
using System.Text.Json;
using RSEV.Utilities.ApiBuilder;
using ioBroker_NewGen.Admin.Auth;
using ioBroker_NewGen.Core.Adapters;
using ioBroker_NewGen.Core.Bridge;
using ioBroker_NewGen.Core.Web;

namespace ioBroker_NewGen.Admin.Ui;

internal sealed class LegacyAdminTabEndpoints
{
    private readonly IAdapterContext _context;

    public LegacyAdminTabEndpoints(IAdapterContext context)
    {
        _context = context;
    }

    public WebEndpointResponse GetTabs(WebEndpointRequest request)
    {
        if (!IsAuthorized(request, "/api/adapters/tabs"))
        {
            return AuthorizationFailure();
        }

        var legacy = _context.Legacy;
        if (legacy is null)
        {
            return WebEndpointResponse.Text(503, "{\"error\":\"Legacy-Adapter-Runtime ist nicht initialisiert\"}", "application/json; charset=utf-8");
        }

        var tabs = legacy.GetInstalled()
            .Select(package => ReadTab(package))
            .OfType<LegacyAdminTab>()
            .ToArray();

        return WebEndpointResponse.Text(
            200,
            JsonSerializer.Serialize(tabs, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            "application/json; charset=utf-8");
    }

    public WebEndpointResponse ServeAdapterFile(WebEndpointRequest request)
    {
        if (!IsAuthorized(request, request.Route))
        {
            return AuthorizationFailure();
        }

        var route = Uri.UnescapeDataString(request.Route);
        var parts = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && string.Equals(parts[0], "adapter", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(parts[1], LegacyAssetsSegment, StringComparison.Ordinal))
        {
            return ServeLegacyAsset(string.Join('/', parts.Skip(2)));
        }

        if (parts.Length < 2 || !string.Equals(parts[0], "adapter", StringComparison.OrdinalIgnoreCase) ||
            !LegacyAdapterPackage.IsValidName(parts[1]))
        {
            return WebEndpointResponse.Text(404, "Adapter-Datei nicht gefunden.");
        }

        var package = _context.Legacy?.GetInstalled()
            .FirstOrDefault(candidate => string.Equals(candidate.Name, parts[1], StringComparison.OrdinalIgnoreCase));
        if (package is null)
        {
            return WebEndpointResponse.Text(404, "Adapter-Datei nicht gefunden.");
        }

        var relativePath = parts.Length == 2 ? "index.html" : Path.Combine(parts.Skip(2).ToArray());
        var normalizedRelativePath = relativePath.Replace('\\', '/');
        if (normalizedRelativePath.EndsWith("/_socket/info.js", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedRelativePath, "_socket/info.js", StringComparison.OrdinalIgnoreCase))
        {
            return SocketInfo(request);
        }

        // Some modern legacy admin builds (e.g. iot) load the socket client as
        // "./lib/js/socket.io.js" from index_m.html. That resolves to
        // /adapter/<name>/lib/js/socket.io.js, but the client is supplied by
        // NewGen and is not part of the installed adapter package.
        if (IsSocketClientPath(normalizedRelativePath))
        {
            return ServeSocketClient();
        }

        var adminRoot = Path.GetFullPath(Path.Combine(package.Directory, "admin"));
        var filePath = Path.GetFullPath(Path.Combine(adminRoot, relativePath));
        if (!filePath.StartsWith(adminRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(filePath))
        {
            return WebEndpointResponse.Text(404, "Adapter-Datei nicht gefunden.");
        }

        var contentType = ContentTypeFor(Path.GetExtension(filePath));
        var body = File.ReadAllBytes(filePath);
        if (contentType.StartsWith("text/html", StringComparison.Ordinal))
        {
            var html = Encoding.UTF8.GetString(body);
            if (Path.GetFileName(filePath).StartsWith("index", StringComparison.OrdinalIgnoreCase))
            {
                html = PrepareHtml(html);
            }

            body = Encoding.UTF8.GetBytes(html);
        }

        return new WebEndpointResponse
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = contentType,
                ["X-Content-Type-Options"] = "nosniff"
            },
            Body = body
        };
    }

    private const string LegacyAssetsSegment = "_assets";

    // Adapter-Seiten laden die Admin-Bibliotheken relativ über "../../" (ioBroker-Wurzel); hier liegen sie unter /adapter/_assets/.
    // Zusätzlich wird der WebSocket-Endpunkt gesetzt, den adapter-settings.js sonst aus dem Seitenpfad ableitet.
    private static string PrepareHtml(string html)
    {
        html = html.Replace("\"../../", "\"/adapter/" + LegacyAssetsSegment + "/", StringComparison.Ordinal)
            .Replace("'../../", "'/adapter/" + LegacyAssetsSegment + "/", StringComparison.Ordinal);
        const string viewport = "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">";
        const string themeSetup = "<script>(function(){var p=new URLSearchParams(window.location.search);if(p.get('react')==='dark'){document.documentElement.classList.add('newgen-dark');}})();</script><style>html.newgen-dark,html.newgen-dark body,html.newgen-dark body>.m.adapter-container,html.newgen-dark .admin-dialog-body{background-color:#272727!important;color:#fff!important}</style>";
        // Modern React admin builds pass socketPath to @iobroker/socket-client as
        // a URL suffix, while the older adapter-settings.js expects socketPath
        // to be the complete socket URL. Keep both variants working.
        var modernSocketClient = html.Contains("./lib/js/socket.io.js", StringComparison.Ordinal);
        var socketSetup = modernSocketClient
            ? "<script>window.socketPath=location.pathname.substring(0,location.pathname.lastIndexOf('/')+1);</script>"
            : "<script>window.socketPath=location.origin+'/';</script>";
        var setup = viewport + themeSetup + socketSetup;
        var head = html.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
        return head < 0
            ? setup + html
            : html.Insert(head + "<head>".Length, setup);
    }

    private WebEndpointResponse ServeLegacyAsset(string relativePath)
    {
        if (IsSocketClientPath(relativePath))
        {
            return ServeSocketClient();
        }

        if (relativePath.Contains("..", StringComparison.Ordinal))
        {
            return WebEndpointResponse.Text(404, "Adapter-Datei nicht gefunden.");
        }

        var assembly = typeof(LegacyAdminTabEndpoints).Assembly;
        var expected = "ui/legacy/" + relativePath;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => string.Equals(name.Replace('\\', '/'), expected, StringComparison.Ordinal));
        if (resourceName is null)
        {
            return WebEndpointResponse.Text(404, "Adapter-Datei nicht gefunden.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return new WebEndpointResponse
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = ContentTypeFor(Path.GetExtension(relativePath)),
                ["X-Content-Type-Options"] = "nosniff"
            },
            Body = memory.ToArray()
        };
    }

    private static bool IsSocketClientPath(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        return string.Equals(normalized, "lib/js/socket.io.js", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "socket.io/socket.io.js", StringComparison.OrdinalIgnoreCase);
    }

    public WebEndpointResponse ServeSocketClient()
    {
        const string resourcePath = "ui/lib/js/socket.io.js";
        var assembly = typeof(LegacyAdminTabEndpoints).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => string.Equals(name.Replace('\\', '/'), resourcePath, StringComparison.Ordinal));
        if (resourceName is null)
        {
            throw new InvalidOperationException($"Eingebettete Ressource '{resourcePath}' fehlt.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Eingebettete Ressource '{resourcePath}' kann nicht gelesen werden.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return new WebEndpointResponse
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = "application/javascript; charset=utf-8",
                ["X-Content-Type-Options"] = "nosniff"
            },
            Body = memory.ToArray()
        };
    }

    public WebEndpointResponse ServeSocketInfo(WebEndpointRequest request) => SocketInfo(request);

    private WebEndpointResponse SocketInfo(WebEndpointRequest request)
    {
        var host = Header(request, "Host");
        if (string.IsNullOrWhiteSpace(host))
        {
            return WebEndpointResponse.Text(400, "Host-Header fehlt.");
        }

        var forwardedProtocol = Header(request, "X-Forwarded-Proto");
        var scheme = string.Equals(forwardedProtocol, "https", StringComparison.OrdinalIgnoreCase) ? "https" : "http";
        var socketUrl = JsonSerializer.Serialize($"{scheme}://{host}/");
        var script = $"var socketEndpointUrl = {socketUrl};\nvar socketSession = \"\";\nvar systemLang = \"de\";\nvar socketForceWebSockets = true;\n";
        return WebEndpointResponse.Text(200, script, "application/javascript; charset=utf-8");
    }

    private bool IsAuthorized(WebEndpointRequest request, string route)
    {
        var endpointRequest = new EndpointRequest
        {
            Route = route,
            Headers = request.Headers.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            Body = Array.Empty<byte>()
        };
        return AuthGuard.IsAuthorized(_context, endpointRequest);
    }

    private static WebEndpointResponse AuthorizationFailure()
        => WebEndpointResponse.Text(
            AuthGuard.Forbidden.Value ? 403 : 401,
            AuthGuard.Forbidden.Value ? "{\"error\":\"Keine Berechtigung\"}" : "{\"error\":\"Authentifizierung erforderlich\"}",
            "application/json; charset=utf-8");

    private static LegacyAdminTab? ReadTab(LegacyPackageInfo package)
    {
        var ioPackagePath = Path.Combine(package.Directory, "io-package.json");
        if (!File.Exists(Path.Combine(package.Directory, "admin", "tab.html")))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(ioPackagePath));
        if (!document.RootElement.TryGetProperty("common", out var common) ||
            !common.TryGetProperty("adminTab", out var adminTab) ||
            adminTab.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var title = LocalizedName(adminTab);
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        return new LegacyAdminTab(package.Name, title, $"/adapter/{package.Name}/tab.html");
    }

    private static string? LocalizedName(JsonElement adminTab)
    {
        if (!adminTab.TryGetProperty("name", out var name))
        {
            return null;
        }

        if (name.ValueKind == JsonValueKind.String)
        {
            return name.GetString();
        }

        if (name.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var language in new[] { "de", "en" })
        {
            if (name.TryGetProperty(language, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return name.EnumerateObject()
            .Select(property => property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? Header(WebEndpointRequest request, string name)
        => request.Headers.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    private static string ContentTypeFor(string extension)
        => extension.ToLowerInvariant() switch
        {
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".js" or ".mjs" => "application/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".json" or ".webmanifest" => "application/json; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".ico" => "image/x-icon",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            ".wasm" => "application/wasm",
            ".txt" => "text/plain; charset=utf-8",
            _ => "application/octet-stream"
        };

    private sealed record LegacyAdminTab(string AdapterName, string Title, string Url);
}
