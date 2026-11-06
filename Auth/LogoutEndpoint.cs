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
using static ioBroker_NewGen.Admin.Auth.AuthGuard;

namespace ioBroker_NewGen.Admin.Auth;

internal sealed class LogoutEndpoint : AuthEndpointBase
{
    public LogoutEndpoint(IAdapterContext context) : base("/api/auth/logout", context)
    {
    }

    protected override async Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (!HasCsrfHeader(request))
        {
            return Json(403, new { error = "Ungültige Anfrage" });
        }

        var token = ReadSessionToken(request);
        var security = SecurityOf(Context);
        if (security is not null && token is not null)
        {
            await security.Authentication.LogoutAsync(token);
        }

        return Json(200, new { success = true }, BuildSessionCookie(string.Empty, 0));
    }
}
