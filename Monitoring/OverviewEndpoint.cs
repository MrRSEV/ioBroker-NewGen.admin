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
using static ioBroker_NewGen.Admin.AdapterManagement.AutoStartSettings;
using static ioBroker_NewGen.Admin.Auth.AuthGuard;
using static ioBroker_NewGen.Admin.Http.EndpointResults;

namespace ioBroker_NewGen.Admin.Monitoring;

internal sealed class OverviewEndpoint : BaseRuntimeEndpoint
{
    private readonly IAdapterContext _context;
    private readonly object _cpuLock = new();
    private TimeSpan _lastCpu;
    private DateTime _lastCpuAt = DateTime.UtcNow;
    private double _cpuPercent;

    public OverviewEndpoint(IAdapterContext context) : base("/api/overview")
    {
        _context = context;
        _lastCpu = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;
    }

    protected override Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (!IsAuthorized(_context, request))
        {
            return Task.FromResult(Unauthorized());
        }

        var adapters = _context.AdapterRegistry.GetAll();
        var active = adapters.Count(a => a.Status == AdapterStatus.Connected);
        var faulted = adapters.Count(a => a.Status == AdapterStatus.Faulted);
        var enabledList = ReadEnabledAdapters(_context);
        var instances = adapters.Count(a => a.Status == AdapterStatus.Connected || IsAutoStart(enabledList, a));

        int objects = 0, states = 0;
        try
        {
            using var document = JsonDocument.Parse(_context.Matrix.DisplayAsJson());
            foreach (var item in document.RootElement.EnumerateArray())
            {
                objects++;
                if (item.TryGetProperty("State", out var state) && state.ValueKind == JsonValueKind.Object)
                {
                    states++;
                }
            }
        }
        catch
        {
            // Zähler bleiben 0, wenn der Objektbaum nicht lesbar ist.
        }

        var process = System.Diagnostics.Process.GetCurrentProcess();
        double cpu;
        lock (_cpuLock)
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastCpuAt).TotalMilliseconds;
            if (elapsed >= 1000)
            {
                var used = process.TotalProcessorTime;
                _cpuPercent = Math.Clamp((used - _lastCpu).TotalMilliseconds / elapsed / Environment.ProcessorCount * 100, 0, 100);
                _lastCpu = used;
                _lastCpuAt = now;
            }

            cpu = _cpuPercent;
        }

        var totalMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var payload = new
        {
            Status = faulted == 0 ? "OK" : "WARN",
            Adapters = new { Total = adapters.Count, Active = active, Inactive = adapters.Count - active, Faulted = faulted, Instances = instances },
            Objects = objects,
            States = states,
            System = new
            {
                Platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                Architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
                Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                Host = Environment.MachineName,
                Cores = Environment.ProcessorCount,
                SystemUptimeSeconds = Environment.TickCount64 / 1000,
                ProcessUptimeSeconds = (long)(DateTime.Now - process.StartTime).TotalSeconds,
                MemoryUsedBytes = process.WorkingSet64,
                MemoryTotalBytes = totalMemory,
                CpuPercent = Math.Round(cpu, 1)
            }
        };
        return Task.FromResult(Response(200, JsonSerializer.Serialize(payload), "application/json; charset=utf-8"));
    }
}
