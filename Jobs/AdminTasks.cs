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

internal static class AdminTasks
{
    private const int MaxFinished = 30;
    private static readonly object Gate = new();
    private static readonly List<AdminTask> Tasks = new();

    public static AdminTask Start(string title, Func<AdminTask, Task<string>> work)
    {
        var task = new AdminTask(Guid.NewGuid().ToString("N"), title);
        lock (Gate)
        {
            Tasks.Add(task);
            var finished = Tasks.Where(t => t.Status != "running").ToList();
            foreach (var old in finished.Take(Math.Max(0, finished.Count - MaxFinished)))
            {
                Tasks.Remove(old);
            }
        }

        _ = Task.Run(async () =>
        {
            try
            {
                task.Complete(true, await work(task));
            }
            catch (Exception ex)
            {
                task.Complete(false, ex.Message);
            }
        });
        return task;
    }

    public static IReadOnlyList<AdminTask> All()
    {
        lock (Gate)
        {
            return Tasks.ToList();
        }
    }

    public static AdminTask? Find(string id)
    {
        lock (Gate)
        {
            return Tasks.FirstOrDefault(t => t.Id == id);
        }
    }

    public static bool Dismiss(string id)
    {
        lock (Gate)
        {
            var task = Tasks.FirstOrDefault(t => t.Id == id);
            return task is not null && task.Status != "running" && Tasks.Remove(task);
        }
    }
}
