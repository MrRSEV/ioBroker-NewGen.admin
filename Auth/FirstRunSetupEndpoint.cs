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

internal sealed class FirstRunSetupEndpoint : AuthEndpointBase
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public FirstRunSetupEndpoint(IAdapterContext context) : base("/api/auth/setup", context)
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
        var password = document is null ? null : Str(document.RootElement, "newPassword");
        if (string.IsNullOrEmpty(password))
        {
            return Json(400, new { error = "Passwort erforderlich" });
        }

        await Gate.WaitAsync();
        try
        {
            if (!await IsFirstRunAsync(security))
            {
                return Json(409, new { error = "Das Administrator-Passwort wurde bereits gesetzt" });
            }

            var username = security.Options.DefaultUsername;
            try
            {
                await security.Users.SetPasswordAsync(username, password, mustChangePassword: false);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Json(400, new { error = ex.Message });
            }

            var login = await security.Authentication.LoginAsync(username, password);
            if (!login.Success || login.Token is null)
            {
                return Json(200, new { success = true, login = false });
            }

            var maxAge = Math.Max(60, security.Options.SessionTimeoutMinutes * 60);
            return Json(200, new { success = true }, BuildSessionCookie(login.Token, maxAge));
        }
        finally
        {
            Gate.Release();
        }
    }
}
