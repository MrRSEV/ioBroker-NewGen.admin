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
using ioBroker_NewGen.Admin.Auth;
using static ioBroker_NewGen.Admin.Auth.AuthGuard;

namespace ioBroker_NewGen.Admin.Settings;

internal sealed class I18nEndpoint : AuthEndpointBase
{
    private readonly string _mode;

    public I18nEndpoint(string mode, IAdapterContext context) : base("/api/i18n/" + mode, context)
    {
        _mode = mode;
    }

    protected override Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        var i18n = Context.Localization;
        if (i18n is null)
        {
            return Task.FromResult(Json(503, new { error = "Lokalisierung nicht verfügbar" }));
        }

        using var document = ParseBody(request);
        var root = document?.RootElement ?? default;
        var hasBody = document is not null && root.ValueKind == JsonValueKind.Object;
        var language = hasBody ? Str(root, "language") : null;

        switch (_mode)
        {
            case "languages":
                return Task.FromResult(Json(200, new { language = i18n.Language, fallback = i18n.FallbackLanguage, languages = i18n.AvailableLanguages.Select(l => new { code = l.Code, name = l.Name }).ToArray() }));
            case "translations":
                var resolved = i18n.ResolveLanguage(language) ?? i18n.Language;
                return Task.FromResult(Json(200, new { language = resolved, fallback = i18n.FallbackLanguage, translations = i18n.GetAll(resolved) }));
            case "translate":
                var key = hasBody ? Str(root, "key") : null;
                if (string.IsNullOrEmpty(key))
                {
                    return Task.FromResult(Json(400, new { error = "key erforderlich" }));
                }

                var text = i18n.Translate(key, language);
                if (hasBody && root.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Array)
                {
                    try
                    {
                        text = string.Format(i18n.Culture, text, args.EnumerateArray().Select(a => (object?)a.ToString()).ToArray());
                    }
                    catch (FormatException)
                    {
                    }
                }

                return Task.FromResult(Json(200, new { key, text }));
            case "extend":
                if (!IsAuthorized(Context, request))
                {
                    return Task.FromResult(Unauthorized());
                }

                if (!hasBody || language is null || !root.TryGetProperty("translations", out var texts) || texts.ValueKind != JsonValueKind.Object)
                {
                    return Task.FromResult(Json(400, new { error = "language und translations erforderlich" }));
                }

                return Task.FromResult(Json(200, new { added = i18n.ExtendFromJson(language, texts.GetRawText()) }));
            default:
                return Task.FromResult(Json(404, new { error = "Unbekannt" }));
        }
    }
}
