using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.Files;

/// <summary>
/// Journals a <see cref="LogHub"/> to disk for the whole run of an application.
/// </summary>
/// <remarks>
/// <para>
/// Entries are written while the application runs, from a background queue, so a crash still leaves
/// them on disk. When the writer is disposed (or the process exits) the session is finalized: its
/// files are renamed with the end time, a Markdown summary is written for people and LLMs, and
/// index.json is updated. Sessions left behind by processes that died are recovered as "crashed"
/// the next time any application opens the folder.
/// </para>
/// <para>
/// Old sessions are kept up to <see cref="LogFileOptions.MaxSessions"/>, <see cref="LogFileOptions.MaxAgeDays"/>
/// and <see cref="LogFileOptions.MaxTotalSizeMb"/>; a large session continues in part files of
/// <see cref="LogFileOptions.MaxFileSizeMb"/>. Mirrored entries (copied from another process's journal)
/// are not written again.
/// </para>
/// </remarks>
public sealed class LogSessionWriter : ILogSink
{
    private readonly LogFileOptions _options;
    private readonly Channel<LogEntry> _queue = Channel.CreateUnbounded<LogEntry>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Task _writerTask;
    private readonly LogSessionHeader _header;
    private readonly string _currentBaseName;
    private readonly List<string> _parts = new();
    private readonly LogSessionStatistics _statistics = new();
    private readonly object _finalizeLock = new();
    private StreamWriter _writer;
    private long _bytesInPart;
    private long _lineInPart;
    private int _finalized;

    public LogSessionWriter(LogFileOptions? options = null)
    {
        _options = options ?? new LogFileOptions();
        Directory = _options.ResolveDirectory();
        System.IO.Directory.CreateDirectory(Directory);

        int processId = Environment.ProcessId;
        _header = new LogSessionHeader(
            _options.ProductName,
            _options.AppName,
            _options.AppVersion,
            processId,
            DateTimeOffset.Now,
            Environment.MachineName);
        _currentBaseName = LogSessionNames.CurrentBaseName(_options.AppName, processId, _header.Started);

        LogSessionCatalog.WithFolderLock(Directory, () =>
        {
            LogSessionCatalog.RecoverCrashed(Directory, processId);
            LogSessionCatalog.ApplyRetention(Directory, _options);
        });

        _writer = OpenPart(1);
        _writerTask = Task.Run(WriteLoopAsync);

        if (_options.FinalizeOnProcessExit)
        {
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }
    }

    /// <summary>The log folder.</summary>
    public string Directory { get; }

    /// <summary>The file being written now. After the session ends, see <see cref="FinishedFiles"/>.</summary>
    public string CurrentFilePath => Path.Combine(Directory, _parts[^1]);

    /// <summary>The session's files after it was finalized, and its summary.</summary>
    public IReadOnlyList<string> FinishedFiles { get; private set; } = [];

    public string? SummaryFilePath { get; private set; }

    public LogSessionHeader Header => _header;

    public void Emit(LogEntry entry)
    {
        if (entry.IsMirrored || entry.Level < _options.MinimumLevel || Volatile.Read(ref _finalized) != 0)
        {
            return;
        }

        _queue.Writer.TryWrite(entry);
    }

    /// <summary>Waits until everything emitted so far is on disk.</summary>
    public async Task FlushAsync()
    {
        TaskCompletionSource flushed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _flushRequests.Enqueue(flushed);
        _queue.Writer.TryWrite(FlushMarker);
        await flushed.Task.ConfigureAwait(false);
    }

    public void Dispose()
    {
        FinalizeSession(TimeSpan.FromSeconds(5));
    }

    private static readonly LogEntry FlushMarker = new(0, default, LogSeverity.Info, "\0flush", string.Empty, string.Empty);

    private readonly System.Collections.Concurrent.ConcurrentQueue<TaskCompletionSource> _flushRequests = new();

    private void OnProcessExit(object? sender, EventArgs e)
    {
        // ProcessExit gives a few seconds at most.
        FinalizeSession(TimeSpan.FromSeconds(2));
    }

    private void FinalizeSession(TimeSpan timeout)
    {
        lock (_finalizeLock)
        {
            if (Interlocked.Exchange(ref _finalized, 1) != 0)
            {
                return;
            }

            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            _queue.Writer.TryComplete();
            try
            {
                _writerTask.Wait(timeout);
            }
            catch (AggregateException)
            {
            }

            _writer.Flush();
            _writer.Dispose();

            DateTimeOffset ended = DateTimeOffset.Now;
            string baseName = LogSessionNames.FinishedBaseName(_options.AppName, _header.Started, ended);
            LogSessionCatalog.WithFolderLock(Directory, () =>
            {
                List<string> files = new();
                for (int index = 0; index < _parts.Count; index++)
                {
                    string target = LogSessionNames.PartFileName(baseName, index + 1);
                    try
                    {
                        File.Move(Path.Combine(Directory, _parts[index]), Path.Combine(Directory, target), overwrite: true);
                        files.Add(target);
                    }
                    catch (IOException)
                    {
                        files.Add(_parts[index]);
                    }
                }

                // Line references in the summary point at the final file names.
                string summary = LogSessionNames.SummaryFileName(baseName);
                string text = _statistics.RenderSummary(_header, ended, crashed: false, files);
                for (int index = 0; index < _parts.Count; index++)
                {
                    text = text.Replace($"`{_parts[index]}`", $"`{files[index]}`", StringComparison.Ordinal);
                }

                File.WriteAllText(Path.Combine(Directory, summary), text);
                LogSessionCatalog.Upsert(Directory, LogSessionCatalog.Record(
                    baseName, _header, ended, LogSessionStatus.Finished, files, summary, _statistics));
                LogSessionCatalog.ApplyRetention(Directory, _options);

                FinishedFiles = files.Select(file => Path.Combine(Directory, file)).ToArray();
                SummaryFilePath = Path.Combine(Directory, summary);
            });
        }
    }

    private async Task WriteLoopAsync()
    {
        ChannelReader<LogEntry> reader = _queue.Reader;
        Stopwatch sinceFlush = Stopwatch.StartNew();
        bool dirty = false;

        while (true)
        {
            Task<bool> waitForData = reader.WaitToReadAsync().AsTask();
            Task completed = dirty
                ? await Task.WhenAny(waitForData, Task.Delay(_options.FlushInterval)).ConfigureAwait(false)
                : waitForData;

            if (completed == waitForData && !await waitForData.ConfigureAwait(false))
            {
                break;
            }

            bool flushNow = false;
            while (reader.TryRead(out LogEntry? entry))
            {
                if (ReferenceEquals(entry, FlushMarker))
                {
                    flushNow = true;
                    continue;
                }

                WriteEntry(entry);
                dirty = true;
                flushNow |= entry.Level >= LogEntryLevel.Error;
            }

            if (dirty && (flushNow || sinceFlush.Elapsed >= _options.FlushInterval))
            {
                _writer.Flush();
                dirty = false;
                sinceFlush.Restart();
            }

            if (flushNow)
            {
                while (_flushRequests.TryDequeue(out TaskCompletionSource? request))
                {
                    request.TrySetResult();
                }
            }
        }

        _writer.Flush();
        while (_flushRequests.TryDequeue(out TaskCompletionSource? request))
        {
            request.TrySetResult();
        }
    }

    private void WriteEntry(LogEntry entry)
    {
        try
        {
            LogEntry written = _options.Redactor?.Invoke(entry) ?? entry;
            string line = LogJsonFormat.WriteEntry(written);
            long size = Encoding.UTF8.GetByteCount(line) + 1;
            if (_bytesInPart + size > (long)_options.MaxFileSizeMb * 1024 * 1024 && _lineInPart > 1)
            {
                _writer.Flush();
                _writer.Dispose();
                _writer = OpenPart(_parts.Count + 1);
            }

            _writer.Write(line);
            _writer.Write('\n');
            _bytesInPart += size;
            _lineInPart++;
            _statistics.Add(written, _parts[^1], _lineInPart);
        }
        catch (IOException)
        {
            // The disk journal is best effort: the console and the app keep working without it.
        }
    }

    private StreamWriter OpenPart(int part)
    {
        string fileName = LogSessionNames.PartFileName(_currentBaseName, part);
        _parts.Add(fileName);
        FileStream stream = new(Path.Combine(Directory, fileName), FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        string header = LogJsonFormat.WriteHeader(_header);
        writer.Write(header);
        writer.Write('\n');
        writer.Flush();
        _bytesInPart = Encoding.UTF8.GetByteCount(header) + 1;
        _lineInPart = 1;
        return writer;
    }
}
