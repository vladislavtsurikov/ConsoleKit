using System.Text;
using VladislavTsurikov.ConsoleKit.Core;

namespace VladislavTsurikov.ConsoleKit.ProcessLogReader;

/// <summary>
/// Follows a log file while another process writes it, like <c>tail -f</c>: for background processes
/// and for processes this one did not start, whose stdout it cannot read. The file is opened shared,
/// the read position is remembered, and a file that was truncated or recreated is read from the start.
/// </summary>
public sealed class FileTailLineStream : ILogLineStream, IDisposable
{
    private readonly object _lock = new();
    private readonly string _path;
    private readonly Timer _timer;
    private readonly StringBuilder _partialLine = new();
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
    private readonly byte[] _buffer = new byte[16 * 1024];
    private readonly char[] _chars = new char[Encoding.UTF8.GetMaxCharCount(16 * 1024)];
    private long _position;
    private bool _disposed;

    /// <param name="path">The file to follow; it may not exist yet.</param>
    /// <param name="fromEnd">Skip what is already in the file and read only new lines.</param>
    /// <param name="pollInterval">How often the file is checked for new data.</param>
    public FileTailLineStream(string path, bool fromEnd = false, TimeSpan? pollInterval = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        if (fromEnd && File.Exists(path))
        {
            _position = new FileInfo(path).Length;
        }

        TimeSpan interval = pollInterval ?? TimeSpan.FromMilliseconds(250);
        _timer = new Timer(_ => Poll(), null, interval, interval);
    }

    public event EventHandler<string>? LineReceived;

    public string Path => _path;

    /// <summary>Reads whatever was appended since the last check. Called by the timer; public for tests.</summary>
    public void Poll()
    {
        List<string> lines = new();
        lock (_lock)
        {
            if (_disposed || !File.Exists(_path))
            {
                return;
            }

            try
            {
                using FileStream stream = new(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length < _position)
                {
                    // Truncated or replaced by a new file: start over.
                    _position = 0;
                    _partialLine.Clear();
                    _decoder.Reset();
                }

                stream.Position = _position;
                int read;
                while ((read = stream.Read(_buffer, 0, _buffer.Length)) > 0)
                {
                    _position += read;
                    int charCount = _decoder.GetChars(_buffer, 0, read, _chars, 0);
                    Split(_chars.AsSpan(0, charCount), lines);
                }
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }

        foreach (string line in lines)
        {
            LineReceived?.Invoke(this, line);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
        }

        _timer.Dispose();
    }

    private void Split(ReadOnlySpan<char> chars, List<string> lines)
    {
        foreach (char character in chars)
        {
            if (character == '\n')
            {
                int length = _partialLine.Length;
                if (length > 0 && _partialLine[length - 1] == '\r')
                {
                    _partialLine.Length = length - 1;
                }

                lines.Add(_partialLine.ToString());
                _partialLine.Clear();
            }
            else
            {
                _partialLine.Append(character);
            }
        }
    }
}
