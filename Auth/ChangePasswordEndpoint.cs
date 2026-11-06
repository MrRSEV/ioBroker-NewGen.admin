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

internal sealed class ChangePasswordEndpoint : AuthEndpointBase
{
    public ChangePasswordEndpoint(IAdapterContext context) : base("/api/auth/change-password", context)
    {
    }

    protected override async Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (!HasCsrfHeader(request))
        {
            return Json(403, new { error = "Ungültige Anfrage" });
        }

        var resolved = await ResolveSessionAsync(Context, request);
        if (resolved is null)
        {
            return Json(401, new { error = "Authentifizierung erforderlich" });
        }

        using var document = ParseBody(request);
        var current = document is null ? null : Str(document.RootElement, "currentPassword");
        var next = document is null ? null : Str(document.RootElement, "newPassword");
        if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(next))
        {
            return Json(400, new { error = "Aktuelles und neues Passwort erforderlich" });
        }

        var (security, _, user) = resolved.Value;
        var verification = await security.Authentication.LoginAsync(user.Username, current);
        if (!verification.Success || verification.Token is null)
        {
            await Task.Delay(500);
            return Json(400, new { error = "Aktuelles Passwort ist falsch" });
        }

        await security.Authentication.LogoutAsync(verification.Token);

        try
        {
            await security.Users.SetPasswordAsync(user.Username, next, mustChangePassword: false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Json(400, new { error = ex.Message });
        }

        // SetPassword widerruft alle Sitzungen, daher direkt neu anmelden und Cookie ersetzen.
        var relogin = await security.Authentication.LoginAsync(user.Username, next);
        if (!relogin.Success || relogin.Token is null)
        {
            return Json(200, new { success = true, relogin = false }, BuildSessionCookie(string.Empty, 0));
        }

        var maxAge = Math.Max(60, security.Options.SessionTimeoutMinutes * 60);
        return Json(200, new { success = true }, BuildSessionCookie(relogin.Token, maxAge));
    }
}
