using RSEV.Utilities.ApiBuilder;
using System.Text.Json;
using ioBroker_NewGen.Core.Adapters;
using static ioBroker_NewGen.Admin.Auth.AuthGuard;
using static ioBroker_NewGen.Admin.Http.EndpointResults;

namespace ioBroker_NewGen.Admin.Monitoring;

/// <summary>Schreibt einen State aus dem Objekte-Tab (ack=false, damit Adapter reagieren).</summary>
internal sealed class StateWriteEndpoint : BaseRuntimeEndpoint
{
    private readonly IAdapterContext _context;

    public StateWriteEndpoint(IAdapterContext context) : base("/api/states/write")
    {
        _context = context;
    }

    protected override Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (request.Body is not { Length: > 0 } || !IsAuthorized(_context, request))
        {
            return Task.FromResult(Unauthorized());
        }

        try
        {
            using var document = JsonDocument.Parse(request.Body);
            var id = document.RootElement.GetProperty("id").GetString();
            var raw = document.RootElement.GetProperty("value");
            object? value = raw.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => raw.TryGetInt64(out var integer) ? integer : raw.GetDouble(),
                JsonValueKind.String => raw.GetString(),
                _ => null
            };

            var api = _context.Legacy?.Api;
            var written = !string.IsNullOrWhiteSpace(id) && api is not null && api.SetStateValue(id, value);
            return Task.FromResult(Response(written ? 200 : 400, $"{{\"success\":{(written ? "true" : "false")}}}", "application/json; charset=utf-8"));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return Task.FromResult(Response(400, "{\"error\":\"Ungültige Anfrage\"}", "application/json; charset=utf-8"));
        }
    }
}
