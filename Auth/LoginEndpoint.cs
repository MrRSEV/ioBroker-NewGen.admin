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

internal sealed class LoginEndpoint : AuthEndpointBase
{
    public LoginEndpoint(IAdapterContext context) : base("/api/auth/login", context)
    {
    }

    protected override async Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (!HasCsrfHeader(request))
        {
            return Json(403, new { error = "Ungültige Anfrage" });
        }

        var security = SecurityOf(Context);
        if (security is null)
        {
            return Json(503, new { error = "Anmeldung ist nicht verfügbar (Core.Security nicht initialisiert)" });
        }

        using var document = ParseBody(request);
        var username = document is null ? null : Str(document.RootElement, "username");
        var password = document is null ? null : Str(document.RootElement, "password");
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return Json(400, new { error = "Benutzername und Passwort erforderlich" });
        }

        var result = await security.Authentication.LoginAsync(username, password);
        if (!result.Success || result.Token is null)
        {
            await Task.Delay(500);
            return Json(401, new { error = result.Error ?? "Anmeldung fehlgeschlagen" });
        }

        var maxAge = Math.Max(60, security.Options.SessionTimeoutMinutes * 60);
        return Json(
            200,
            new { username = result.User?.Username, mustChangePassword = result.MustChangePassword },
            BuildSessionCookie(result.Token, maxAge));
    }
}
