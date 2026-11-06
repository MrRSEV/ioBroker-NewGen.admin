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

namespace ioBroker_NewGen.Admin.Jobs;

/// <summary>Admin-UI-Task-Manager: führt lange Aktionen parallel im Hintergrund aus und hält ein abrufbares Fortschrittslog vor.</summary>
internal sealed class AdminTask
{
    private readonly object _gate = new();
    private readonly List<string> _log = new();

    public AdminTask(string id, string title)
    {
        Id = id;
        Title = title;
        Started = DateTimeOffset.UtcNow;
    }

    public string Id { get; }
    public string Title { get; }
    public DateTimeOffset Started { get; }
    public string Status { get; private set; } = "running";
    public string? Message { get; private set; }

    public void Log(string line)
    {
        lock (_gate)
        {
            _log.Add($"{DateTime.Now:HH:mm:ss} {line}");
        }
    }

    public void Complete(bool success, string message)
    {
        lock (_gate)
        {
            _log.Add($"{DateTime.Now:HH:mm:ss} {(success ? "✔" : "✖")} {message}");
            Message = message;
            Status = success ? "done" : "error";
        }
    }

    public object Snapshot(int since)
    {
        lock (_gate)
        {
            var from = Math.Clamp(since, 0, _log.Count);
            return new { id = Id, title = Title, status = Status, message = Message, started = Started, total = _log.Count, log = _log.Skip(from).ToArray() };
        }
    }
}
