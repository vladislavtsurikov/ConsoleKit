using System.Diagnostics;

namespace VladislavTsurikov.ConsoleKit.ProcessLogReader;

/// <summary>
/// Adapts a <see cref="Process"/> to <see cref="IProcessOutput"/>. Attaching turns on exit events and,
/// for redirected streams nobody reads yet, starts reading them.
/// </summary>
public sealed class SystemProcessOutput : IProcessOutput, IDisposable
{
    private readonly Process _process;
    private ProcessStartedInfo? _startedInfo;
    private bool _disposed;

    private SystemProcessOutput(Process process, string displayName)
    {
        _process = process;
        DisplayName = displayName;
    }

    public event Action<string>? OutputReceived;

    public event Action<string>? ErrorReceived;

    public event Action<ProcessStartedInfo>? Started
    {
        add
        {
            _started += value;
            // Attached to a process that is already running: report it to late subscribers too.
            if (_startedInfo is { } info)
            {
                value?.Invoke(info);
            }
        }
        remove => _started -= value;
    }

    public event Action<int?>? Exited;

    private event Action<ProcessStartedInfo>? _started;

    public string DisplayName { get; }

    public int? ProcessId => _startedInfo?.ProcessId;

    /// <summary>
    /// Attaches to a started process. Set <paramref name="beginReading"/> to false when the caller
    /// already called <c>BeginOutputReadLine</c>/<c>BeginErrorReadLine</c> itself.
    /// </summary>
    public static SystemProcessOutput Attach(Process process, string? displayName = null, bool beginReading = true)
    {
        ArgumentNullException.ThrowIfNull(process);
        SystemProcessOutput output = new(process, displayName ?? SafeName(process));
        output.Connect(beginReading);
        return output;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _process.OutputDataReceived -= OnOutput;
        _process.ErrorDataReceived -= OnError;
        _process.Exited -= OnExited;
    }

    private void Connect(bool beginReading)
    {
        _process.OutputDataReceived += OnOutput;
        _process.ErrorDataReceived += OnError;
        _process.EnableRaisingEvents = true;
        _process.Exited += OnExited;

        if (beginReading)
        {
            TryBegin(_process.StartInfo.RedirectStandardOutput, _process.BeginOutputReadLine);
            TryBegin(_process.StartInfo.RedirectStandardError, _process.BeginErrorReadLine);
        }

        try
        {
            ProcessStartInfo startInfo = _process.StartInfo;
            string arguments = startInfo.ArgumentList.Count > 0
                ? string.Join(' ', startInfo.ArgumentList.Select(Quote))
                : startInfo.Arguments;
            string commandLine = string.IsNullOrWhiteSpace(arguments) ? startInfo.FileName : $"{startInfo.FileName} {arguments}";
            _startedInfo = new ProcessStartedInfo(_process.Id, commandLine, NullIfEmpty(startInfo.WorkingDirectory));
            _started?.Invoke(_startedInfo);
        }
        catch (InvalidOperationException)
        {
            // Not started yet or already gone: there is no id to report.
        }

        if (HasExited())
        {
            OnExited(this, EventArgs.Empty);
        }
    }

    private static void TryBegin(bool redirected, Action begin)
    {
        if (!redirected)
        {
            return;
        }

        try
        {
            begin();
        }
        catch (InvalidOperationException)
        {
            // Reading already started elsewhere; the data events still reach us.
        }
    }

    private void OnOutput(object sender, DataReceivedEventArgs args)
    {
        if (args.Data is { } line)
        {
            OutputReceived?.Invoke(line);
        }
    }

    private void OnError(object sender, DataReceivedEventArgs args)
    {
        if (args.Data is { } line)
        {
            ErrorReceived?.Invoke(line);
        }
    }

    private void OnExited(object? sender, EventArgs args)
    {
        int? exitCode = null;
        try
        {
            exitCode = _process.ExitCode;
        }
        catch (InvalidOperationException)
        {
        }

        Exited?.Invoke(exitCode);
    }

    private bool HasExited()
    {
        try
        {
            return _process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string SafeName(Process process)
    {
        try
        {
            return process.ProcessName;
        }
        catch (InvalidOperationException)
        {
            return Path.GetFileNameWithoutExtension(process.StartInfo.FileName);
        }
    }

    private static string Quote(string argument) => argument.Contains(' ') ? $"\"{argument}\"" : argument;

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
