using ioBroker_NewGen.Core.Web;
using RSEV.Utilities.ApiBuilder;
using static ioBroker_NewGen.Admin.Http.EndpointResults;

namespace ioBroker_NewGen.Admin.Ui;

/// <summary>Liefert die Admin-Oberfläche (HTML, CSS und JavaScript aus <c>src/</c>).</summary>
internal sealed class DashboardEndpoint : BaseRuntimeEndpoint
{
    public DashboardEndpoint(string route) : base(route)
    {
    }

    protected override Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
        => Task.FromResult(Response(200, DashboardPage.Render(), "text/html; charset=utf-8"));
}
