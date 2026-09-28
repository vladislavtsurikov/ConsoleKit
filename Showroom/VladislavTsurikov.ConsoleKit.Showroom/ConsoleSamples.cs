using VladislavTsurikov.ConsoleKit.Avalonia.ViewModels;
using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.Showroom;

public static class ConsoleSamples
{
    public static ConsoleViewModel CreateViewModel()
    {
        ConsoleSettings settings = new();
        LogEntryStore store = new(settings);
        LogSourceRegistry sources = new(store);
        DateTimeOffset now = DateTimeOffset.Now;

        store.Write(new LogEntry(1, now.AddSeconds(-9), LogSeverity.Info,
            "desktop", "Desktop initialized", "Avalonia desktop started successfully."));
        store.Write(new LogEntry(2, now.AddSeconds(-6), LogSeverity.Warning,
            "worker", "Connection retry", "Worker did not respond; retrying."));
        store.Write(new LogEntry(3, now.AddSeconds(-3), LogSeverity.Error,
            "worker", "Endpoint unavailable", "The example endpoint is offline."));
        store.Write(new LogEntry(4, now, LogSeverity.Info,
            "desktop", "Desktop initialized", "Second message for collapse demonstration."));

        ConsoleViewModel viewModel = new(store, sources, settings);
        viewModel.SelectedEntry = viewModel.Entries.FirstOrDefault();
        return viewModel;
    }
}
