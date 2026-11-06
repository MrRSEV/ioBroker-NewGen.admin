using RSEV.Utilities.ApiBuilder;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ioBroker.Core.Security;
using ioBroker_NewGen.Controllers;
using ioBroker_NewGen.Core.Adapters;
using ioBroker_NewGen.Core.Adapters.Configuration;
using ioBroker_NewGen.Core.Bus;
using ioBroker_NewGen.Core.Matrix;
using ioBroker_NewGen.Core.Registry;
using ioBroker_NewGen.Core.Routing;
using ioBroker_NewGen.Core.Web;
using static ioBroker_NewGen.Admin.Http.EndpointResults;

namespace ioBroker_NewGen.Admin.Auth;

internal abstract class AuthEndpointBase : BaseRuntimeEndpoint
{
    protected readonly IAdapterContext Context;

    protected AuthEndpointBase(string route, IAdapterContext context) : base(route)
    {
        Context = context;
    }

    protected static EndpointResponse Json(int status, object payload, string? cookie = null)
    {
        var response = Response(status, JsonSerializer.Serialize(payload), "application/json; charset=utf-8");
        response.Headers["Cache-Control"] = "no-store";
        if (cookie is not null)
        {
            response.Headers["Set-Cookie"] = cookie;
        }

        return response;
    }

    protected static string? Str(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }

    protected static JsonDocument? ParseBody(EndpointRequest request)
    {
        try
        {
            return request.Body is { Length: > 0 } ? JsonDocument.Parse(request.Body) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
