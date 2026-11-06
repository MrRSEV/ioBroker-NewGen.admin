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

/// <summary>Gemeinsame Antwort-Helfer der Endpunkte.</summary>
internal static class EndpointResults
{
    internal static EndpointResponse Response(int statusCode, string body, string contentType)
    {
        return EndpointResponse.FromText(statusCode, body, contentType);
    }
}
