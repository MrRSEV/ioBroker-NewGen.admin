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

namespace ioBroker_NewGen.Admin.AdapterManagement;

/// <summary>Liest und schreibt die Autostart-Konfiguration (adapter.enabledAdapters).</summary>
internal static class AutoStartSettings
{
    internal static IReadOnlyList<string>? ReadEnabledAdapters(IAdapterContext context)
    {
        try
        {
            var configured = context.Config.Get<string[]>("adapter.enabledAdapters");
            return configured is null
                ? Array.Empty<string>()
                : configured.Where(entry => !string.IsNullOrWhiteSpace(entry)).ToArray();
        }
        catch
        {
            // Ohne Filter in der Konfiguration starten alle geladenen Adapter.
            return null;
        }
    }

    internal static bool IsAutoStart(IReadOnlyList<string>? enabled, AdapterRegistration adapter)
    {
        var primary = adapter.Id.EndsWith(".0", StringComparison.Ordinal);
        return enabled is null || enabled.Any(entry =>
            string.Equals(entry, adapter.Id, StringComparison.OrdinalIgnoreCase) ||
            (primary && string.Equals(entry, adapter.Name, StringComparison.OrdinalIgnoreCase)));
    }

    internal static string[] SetAutoStart(IAdapterContext context, AdapterRegistration adapter, bool enabled)
    {
        var primary = adapter.Id.EndsWith(".0", StringComparison.Ordinal);
        var current = ReadEnabledAdapters(context)
            ?? context.AdapterRegistry.GetAll().Where(a => a.Type == AdapterType.Custom).Select(a => a.Id).ToArray();
        var updated = current
            .Where(entry => !string.Equals(entry, adapter.Id, StringComparison.OrdinalIgnoreCase) &&
                            !(primary && string.Equals(entry, adapter.Name, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (enabled)
        {
            updated.Add(adapter.Id);
        }

        var array = updated.ToArray();
        context.Config.Set("adapter.enabledAdapters", array);
        ioBroker_NewGen.Startup.ConfigFileWriter.Update(
            Path.Combine(AppContext.BaseDirectory, "config.toml"),
            new Dictionary<string, object?> { ["adapter.enabledAdapters"] = array });
        return array;
    }
}
