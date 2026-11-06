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

namespace ioBroker_NewGen.Admin.Http;

/// <summary>Hilfsfunktionen zum Lesen von JSON-Anfragen.</summary>
internal static class RequestJson
{
    internal static IReadOnlyList<string> StringList(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var element) && element.ValueKind == JsonValueKind.Array
            ? element.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToArray()
            : Array.Empty<string>();
    }
}
