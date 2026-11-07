using System.Text.Json;
using System.Text.Json.Nodes;
using RSEV.Utilities.ApiBuilder;
using ioBroker_NewGen.Admin.Auth;
using ioBroker_NewGen.Core.Adapters;
using ioBroker_NewGen.Core.Bridge;
using ioBroker_NewGen.Core.Web;

namespace ioBroker_NewGen.Admin.Socket;

internal sealed class LegacyAdminSocketEndpoint
{
    private const int MaxMessageBytes = 1024 * 1024;
    private readonly IAdapterContext _context;

    public LegacyAdminSocketEndpoint(IAdapterContext context)
    {
        _context = context;
    }

    public async Task<WebEndpointResponse> HandleAsync(WebEndpointRequest request, CancellationToken cancellationToken)
    {
        var socket = request.WebSocket;
        if (socket is null)
        {
            return WebEndpointResponse.Text(400, "WebSocket-Verbindung fehlt.");
        }

        var authRequest = new EndpointRequest
        {
            Route = "/",
            Headers = request.Headers.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            Body = Array.Empty<byte>()
        };
        if (!AuthGuard.IsAuthorized(_context, authRequest))
        {
            var statusCode = AuthGuard.Forbidden.Value ? 403 : 401;
            return WebEndpointResponse.Text(statusCode, "Authentifizierung erforderlich.");
        }

        await socket.SendTextAsync("[0,0,\"___ready___\"]", cancellationToken);
        var subscriptions = new ClientSubscriptions();
        var sendLock = new SemaphoreSlim(1, 1);
        var api0 = _context.Legacy?.Api;

        async Task PushAsync(string eventName, params object?[] eventArgs)
        {
            try
            {
                var text = JsonSerializer.Serialize(new object?[] { 0, 0, eventName, eventArgs });
                await sendLock.WaitAsync(cancellationToken);
                try { if (socket.IsOpen) await socket.SendTextAsync(text, cancellationToken); }
                finally { sendLock.Release(); }
            }
            catch (Exception) { }
        }

        void OnState(string stateId, object? payload)
        {
            if (subscriptions.MatchesState(stateId)) _ = PushAsync("stateChange", stateId, payload);
        }

        void OnObject(string objectId, JsonNode? value)
        {
            if (subscriptions.MatchesObject(objectId)) _ = PushAsync("objectChange", objectId, value);
        }

        if (api0 is not null)
        {
            api0.StateChangedBroadcast += OnState;
            api0.ObjectChangedBroadcast += OnObject;
        }

        try
        {
        while (!cancellationToken.IsCancellationRequested && socket.IsOpen)
        {
            var message = await socket.ReceiveAsync(cancellationToken);
            if (message is null) break;
            if (!message.IsText || message.Data.Length > MaxMessageBytes) continue;

            using var document = JsonDocument.Parse(message.Data);
            var frame = document.RootElement;
            if (frame.ValueKind != JsonValueKind.Array || frame.GetArrayLength() == 0) continue;
            var type = frame[0].ValueKind == JsonValueKind.Number ? frame[0].GetInt32() : -1;
            if (type == 1)
            {
                await sendLock.WaitAsync(cancellationToken);
                try { await socket.SendTextAsync("[2]", cancellationToken); }
                finally { sendLock.Release(); }
                continue;
            }

            if (type is not (0 or 3) || frame.GetArrayLength() < 3) continue;

            var id = frame[1].GetInt32();
            var name = frame[2].GetString() ?? string.Empty;
            var args = frame.GetArrayLength() > 3 && frame[3].ValueKind == JsonValueKind.Array
                ? frame[3].EnumerateArray().ToArray()
                : Array.Empty<JsonElement>();
            try
            {
                var result = Execute(name, args, request.Query, subscriptions);
                if (type == 3)
                {
                    await SendCallbackAsync(socket, sendLock, id, null, result, cancellationToken);
                }
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                if (type == 3)
                {
                    await SendCallbackAsync(socket, sendLock, id, exception.Message, null, cancellationToken);
                }
            }
        }
        }
        finally
        {
            if (api0 is not null)
            {
                api0.StateChangedBroadcast -= OnState;
                api0.ObjectChangedBroadcast -= OnObject;
            }
        }

        return new WebEndpointResponse { StatusCode = 101 };
    }

    private sealed class ClientSubscriptions
    {
        private readonly object _lock = new();
        private readonly List<string> _states = new();
        private readonly List<string> _objects = new();

        public void Add(bool objects, string pattern)
        {
            lock (_lock)
            {
                var list = objects ? _objects : _states;
                if (!list.Contains(pattern)) list.Add(pattern);
            }
        }

        public void Remove(bool objects, string pattern)
        {
            lock (_lock) (objects ? _objects : _states).Remove(pattern);
        }

        public bool MatchesState(string id) => Matches(_states, id);

        public bool MatchesObject(string id) => Matches(_objects, id);

        private bool Matches(List<string> patterns, string id)
        {
            lock (_lock)
            {
                return patterns.Any(pattern => System.Text.RegularExpressions.Regex.IsMatch(
                    id,
                    "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant));
            }
        }
    }

    private object? Execute(string name, JsonElement[] args, string query, ClientSubscriptions subscriptions)
    {
        var api = _context.Legacy?.Api
            ?? throw new InvalidOperationException("Legacy-Adapter-Runtime ist nicht initialisiert.");
        switch (name)
        {
            case "getVersion":
                return AdminVersion.Value;
            case "authenticate":
                return true;
            case "getUserPermissions":
                return new Dictionary<string, object?>();
            case "getIsAdmin":
                return true;
            case "getAdapterName":
                return "ioBroker-NewGen";
            case "getCurrentInstance":
                return QueryValue(query, "name") is { Length: > 0 } adapterName ? adapterName + ".0" : "javascript.0";
            case "getObject":
            case "getForeignObject":
                return GetObjectNode(api, StringArg(args, 0));
            case "getObjects":
            case "getForeignObjects":
            {
                var pattern = StringArg(args, 0, "*");
                var objectType = StringArg(args, 1, null);
                return api.GetObjectNodes(pattern, objectType);
            }
            case "setObject":
            case "setForeignObject":
                return SetObject(api, StringArg(args, 0), args.Length > 1 ? args[1] : default, false);
            case "extendObject":
            case "extendForeignObject":
                return SetObject(api, StringArg(args, 0), args.Length > 1 ? args[1] : default, true);
            case "delObject":
            case "delForeignObject":
                api.DeleteObjectNode(StringArg(args, 0));
                return null;
            case "sendTo":
            case "sendToHost":
            {
                var target = StringArg(args, 0);
                var command = StringArg(args, 1);
                object? message = args.Length > 2 && args[2].ValueKind != JsonValueKind.Undefined ? args[2].Clone() : null;
                // Wie im Original-Admin: Die Antwort des Adapters wird (max. 30 s) abgewartet und als Callback-Ergebnis geliefert.
                var (delivered, reply) = api.SendToWithReplyAsync(target, command, message, "admin.0", TimeSpan.FromSeconds(30))
                    .GetAwaiter().GetResult();
                return delivered ? reply : null;
            }
            case "getObjectView":
                return GetObjectView(api, args);
            case "getAdapterInstances":
                return GetInstanceDocuments(api, StringArg(args, 0, string.Empty)).Values.ToArray();
            case "getState":
            case "getForeignState":
            {
                var stateId = StringArg(args, 0);
                return SyntheticState(stateId) ?? api.GetStatePayload(stateId);
            }
            case "getStates":
            case "getForeignStates":
            {
                if (args.Length > 0 && args[0].ValueKind == JsonValueKind.Array)
                {
                    var result = new Dictionary<string, object?>(StringComparer.Ordinal);
                    foreach (var item in args[0].EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String))
                    {
                        var itemId = item.GetString()!;
                        var payload = SyntheticState(itemId) ?? api.GetStatePayload(itemId);
                        if (payload is not null) result[itemId] = payload;
                    }

                    return result;
                }

                var states = api.GetStatePayloads(StringArg(args, 0, "*")).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
                return states;
            }
            case "getCompactSystemConfig":
                return api.GetObjectNode("system.config");
            case "getHostByIp":
                return null;
            case "getCompactInstances":
                return GetInstanceDocuments(api, string.Empty).ToDictionary(
                    entry => entry.Key,
                    entry => (object?)new
                    {
                        adminTab = entry.Value?["common"]?["adminTab"]?.DeepClone(),
                        name = entry.Value?["common"]?["name"]?.DeepClone(),
                        icon = entry.Value?["common"]?["icon"]?.DeepClone(),
                        enabled = entry.Value?["common"]?["enabled"]?.DeepClone(),
                        version = entry.Value?["common"]?["version"]?.DeepClone(),
                        host = entry.Value?["common"]?["host"]?.DeepClone()
                    });
            case "getCompactAdapters":
                return (_context.Legacy?.GetInstalled() ?? Array.Empty<LegacyPackageInfo>())
                    .GroupBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => (object?)new { v = group.First().Version, iv = group.First().Version });
            case "getCompactHosts":
            {
                var hostId = $"system.host.{Environment.MachineName}";
                return new Dictionary<string, object?>
                {
                    [hostId] = new { _id = hostId, common = new { name = Environment.MachineName, hostname = Environment.MachineName, address = Array.Empty<string>() } }
                };
            }
            case "getCompactSystemRepositories":
                return new { _id = "system.repositories", native = new { repositories = new Dictionary<string, object?>() } };
            case "getRatings":
                return new Dictionary<string, object?>();
            case "getAdapters":
            {
                var filter = args.Length > 0 && args[0].ValueKind == JsonValueKind.String ? args[0].GetString() : null;
                return (_context.Legacy?.GetInstalled() ?? Array.Empty<LegacyPackageInfo>())
                    .Where(package => string.IsNullOrEmpty(filter) || string.Equals(package.Name, filter, StringComparison.OrdinalIgnoreCase))
                    .Select(package => (object)new
                    {
                        _id = $"system.adapter.{package.Name}",
                        type = "adapter",
                        common = new { name = package.Name, title = package.Title, version = package.Version, installedVersion = package.Version },
                        native = new { }
                    })
                    .ToArray();
            }
            case "getAllObjects":
            {
                var all = api.GetObjectNodes("*", null).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
                foreach (var entry in GetInstanceDocuments(api, string.Empty))
                {
                    all.TryAdd(entry.Key, entry.Value);
                }

                return all;
            }
            case "getObjectsCount":
                return api.GetObjectNodes("*", null).Count;
            case "getHistory":
            case "readDir":
                return Array.Empty<object>();
            case "fileExists":
                return false;
            case "subscribe":
            case "subscribeForeignStates":
            case "subscribeObjects":
            case "subscribeForeignObjects":
            case "unsubscribe":
            case "unsubscribeForeignStates":
            case "unsubscribeObjects":
            case "unsubscribeForeignObjects":
            {
                var forObjects = name.Contains("Objects", StringComparison.Ordinal);
                var add = name.StartsWith("subscribe", StringComparison.Ordinal);
                if (args.Length > 0)
                {
                    var patterns = args[0].ValueKind == JsonValueKind.Array
                        ? args[0].EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)
                        : args[0].ValueKind == JsonValueKind.String ? new[] { args[0].GetString()! } : Array.Empty<string>();
                    foreach (var pattern in patterns)
                    {
                        if (add) subscriptions.Add(forObjects, pattern);
                        else subscriptions.Remove(forObjects, pattern);
                    }
                }

                return null;
            }
            case "requireLog":
            case "subscribeLogs":
                return null;
            default:
                throw new NotSupportedException($"Socket-Methode '{name}' wird nicht unterstützt.");
        }
    }

    private object GetObjectView(NodeAdapterRuntimeApi api, JsonElement[] args)
    {
        var paramsIndex = args.Length > 2 ? 2 : args.Length;
        var parameters = paramsIndex < args.Length && args[paramsIndex].ValueKind == JsonValueKind.Object
            ? args[paramsIndex]
            : default;
        var startKey = PropertyString(parameters, "startkey");
        var endKey = PropertyString(parameters, "endkey");
        var limit = PropertyInt32(parameters, "limit");
        var objectType = args.Length > 1 ? args[1].GetString() : null;
        var documents = api.GetObjectNodes("*", objectType).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        if (string.Equals(objectType, "instance", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var entry in GetInstanceDocuments(api, string.Empty))
            {
                documents.TryAdd(entry.Key, entry.Value);
            }
        }
        else if (string.Equals(objectType, "host", StringComparison.OrdinalIgnoreCase))
        {
            var hostId = $"system.host.{Environment.MachineName}";
            documents.TryAdd(hostId, JsonSerializer.SerializeToNode(new
            {
                _id = hostId,
                type = "host",
                common = new { name = Environment.MachineName, hostname = Environment.MachineName }
            }));
        }

        var rows = documents
            .Where(entry => (startKey is null || string.CompareOrdinal(entry.Key, startKey) >= 0) &&
                            (endKey is null || string.CompareOrdinal(entry.Key, endKey) <= 0))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Take(limit is > 0 ? limit.Value : int.MaxValue)
            .Select(entry => new { id = entry.Key, value = entry.Value })
            .ToArray();
        return new { rows };
    }

    private JsonNode? GetObjectNode(NodeAdapterRuntimeApi api, string id)
    {
        var document = api.GetObjectNode(id) ?? GetInstanceDocuments(api, string.Empty).GetValueOrDefault(id);
        if (document is JsonObject instance && TryGetConfigurableInstance(id, out var registration))
        {
            var configs = _context.AdapterRegistry.Configs;
            instance["native"] = configs.Mask(registration.Name, configs.GetValues(registration.Id, registration.Name));
        }

        return document ?? (id == "system.config" ? DefaultSystemConfig() : null);
    }

    private object? SetObject(NodeAdapterRuntimeApi api, string id, JsonElement document, bool extend)
    {
        if (document.ValueKind != JsonValueKind.Object)
        {
            throw new NotSupportedException($"Objekt '{id}' kann nicht geschrieben werden.");
        }

        if (!TryGetConfigurableInstance(id, out var registration))
        {
            // Nicht-Instanzobjekte (z. B. Skripte des javascript-Adapters) liegen im Objektspeicher der Runtime.
            if (id.StartsWith("system.adapter.", StringComparison.Ordinal))
            {
                throw new NotSupportedException($"Objekt '{id}' kann nicht geschrieben werden.");
            }

            api.SetObjectNode(id, JsonNode.Parse(document.GetRawText())!, extend);
            return null;
        }

        if (!document.TryGetProperty("native", out var native) ||
            JsonNode.Parse(native.GetRawText()) is not JsonObject values)
        {
            throw new NotSupportedException($"Objekt '{id}' kann nicht geschrieben werden.");
        }

        try
        {
            _context.AdapterRegistry.Configs.Save(registration.Id, registration.Name, values);
        }
        catch (ioBroker_NewGen.Core.Adapters.Configuration.AdapterConfigException exception)
        {
            throw new InvalidOperationException(exception.Message, exception);
        }

        if (registration.Status == ioBroker_NewGen.Core.Registry.AdapterStatus.Connected)
        {
            _ = _context.RestartAdapterAsync(registration.Id);
        }

        return null;
    }

    private object? SyntheticState(string stateId)
    {
        const string prefix = "system.adapter.";
        if (!stateId.StartsWith(prefix, StringComparison.Ordinal)) return null;
        var rest = stateId[prefix.Length..];
        var dot = rest.LastIndexOf('.');
        if (dot <= 0) return null;
        var suffix = rest[(dot + 1)..];
        if (suffix is not ("alive" or "connected")) return null;
        var registration = _context.AdapterRegistry.Get(rest[..dot]);
        if (registration is null) return null;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return new Dictionary<string, object?>
        {
            ["val"] = registration.Status == ioBroker_NewGen.Core.Registry.AdapterStatus.Connected,
            ["ts"] = now,
            ["lc"] = now,
            ["ack"] = true,
            ["from"] = "system.host." + Environment.MachineName
        };
    }

    private bool TryGetConfigurableInstance(string objectId, out ioBroker_NewGen.Core.Registry.AdapterRegistration registration)
    {
        const string prefix = "system.adapter.";
        registration = null!;
        if (!objectId.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var found = _context.AdapterRegistry.Get(objectId[prefix.Length..]);
        if (found is null || !_context.AdapterRegistry.Configs.HasConfig(found.Name)) return false;
        registration = found;
        return true;
    }

    private static JsonNode DefaultSystemConfig() => JsonSerializer.SerializeToNode(new
    {
        _id = "system.config",
        type = "config",
        common = new { language = "de", tempUnit = "°C", currency = "€", dateFormat = "DD.MM.YYYY", isFloatComma = true, defaultHistory = "" },
        native = new { secret = "Zgfr56gFe87jJOM" }
    })!;

    private IReadOnlyDictionary<string, JsonNode?> GetInstanceDocuments(NodeAdapterRuntimeApi api, string adapterName)
    {
        var documents = api.GetObjectNodes("system.adapter.*", "instance")
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        foreach (var package in _context.Legacy?.GetInstalled() ?? Array.Empty<LegacyPackageInfo>())
        {
            if (!string.IsNullOrWhiteSpace(adapterName) &&
                !string.Equals(package.Name, adapterName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var id in package.Instances)
            {
                var objectId = $"system.adapter.{id}";
                var hostId = $"system.host.{Environment.MachineName}";
                documents.TryAdd(objectId, JsonSerializer.SerializeToNode(new
                {
                    _id = objectId,
                    type = "instance",
                    common = new
                    {
                        name = package.Name,
                        title = package.Title,
                        version = package.Version,
                        host = hostId,
                        enabled = true
                    },
                    native = new { }
                }));
            }
        }

        return documents;
    }

    private static async Task SendCallbackAsync(
        IWebSocketSession socket,
        SemaphoreSlim sendLock,
        int id,
        string? error,
        object? result,
        CancellationToken cancellationToken)
    {
        object?[] callbackArguments = error is null ? new object?[] { null, result } : new object?[] { error };
        var frame = JsonSerializer.Serialize(new object?[] { 3, id, null, callbackArguments });
        await sendLock.WaitAsync(cancellationToken);
        try { await socket.SendTextAsync(frame, cancellationToken); }
        finally { sendLock.Release(); }
    }

    private static string StringArg(JsonElement[] args, int index, string? fallback = null)
        => index < args.Length && args[index].ValueKind == JsonValueKind.String
            ? args[index].GetString() ?? fallback ?? string.Empty
            : fallback ?? throw new ArgumentException($"Socket-Argument {index + 1} fehlt oder ist ungültig.");

    private static string? PropertyString(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(property, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? PropertyInt32(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(property, out var value) &&
           value.TryGetInt32(out var result)
            ? result
            : null;

    private static string? QueryValue(string query, string name)
    {
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            var key = separator < 0 ? part : part[..separator];
            if (string.Equals(Uri.UnescapeDataString(key.Replace('+', ' ')), name, StringComparison.OrdinalIgnoreCase))
            {
                return separator < 0 ? string.Empty : Uri.UnescapeDataString(part[(separator + 1)..].Replace('+', ' '));
            }
        }

        return null;
    }
}
