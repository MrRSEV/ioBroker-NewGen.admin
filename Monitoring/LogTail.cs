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

namespace ioBroker_NewGen.Admin.Monitoring;

/// <summary>Liest die letzten Zeilen einer (ggf. noch geöffneten) Protokolldatei.</summary>
internal static class LogTail
{
    internal static string[] ReadTail(string path, int maxLines)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        var lines = new Queue<string>(maxLines);
        while (reader.ReadLine() is { } line)
        {
            if (lines.Count == maxLines)
            {
                lines.Dequeue();
            }

            lines.Enqueue(line);
        }

        return lines.ToArray();
    }
}
