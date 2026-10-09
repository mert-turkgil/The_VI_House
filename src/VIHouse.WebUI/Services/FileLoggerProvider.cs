using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;

namespace VIHouse.WebUI.Services;

/// <summary>
/// A small rolling file logger for hosts with no log collector — shared IIS hosting, where console
/// output goes nowhere and a failure that is only logged is a failure nobody ever sees. One file per
/// day under <see cref="FileLoggerOptions.Path"/>, written by a single background writer so request
/// threads never wait on the disk, and files older than <see cref="FileLoggerOptions.RetainDays"/>
/// removed as the day rolls over.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly FileLoggerOptions options;
    private readonly Channel<string> queue = Channel.CreateBounded<string>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private readonly ConcurrentDictionary<string, FileLogger> loggers = new();
    private readonly Task writer;

    public FileLoggerProvider(FileLoggerOptions options)
    {
        this.options = options;
        Directory.CreateDirectory(options.Path);
        writer = Task.Run(WriteLoopAsync);
    }

    public ILogger CreateLogger(string categoryName) => loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));

    internal void Enqueue(string line) => queue.Writer.TryWrite(line);

    private async Task WriteLoopAsync()
    {
        string? currentDay = null;
        StreamWriter? stream = null;
        try
        {
            await foreach (var line in queue.Reader.ReadAllAsync())
            {
                var day = DateTime.UtcNow.ToString("yyyyMMdd");
                if (day != currentDay)
                {
                    if (stream is not null) await stream.DisposeAsync();
                    currentDay = day;
                    stream = new StreamWriter(new FileStream(System.IO.Path.Combine(options.Path, $"vihouse-{day}.log"),
                        FileMode.Append, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8) { AutoFlush = false };
                    Prune();
                }

                await stream!.WriteLineAsync(line);
                if (queue.Reader.Count == 0) await stream.FlushAsync();
            }
        }
        catch
        {
            // Logging must never take the process down; a full disk or a permissions problem
            // simply ends file logging.
        }
        finally
        {
            if (stream is not null) await stream.DisposeAsync();
        }
    }

    private void Prune()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-options.RetainDays);
            foreach (var file in Directory.EnumerateFiles(options.Path, "vihouse-*.log"))
                if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file);
        }
        catch { /* best effort */ }
    }

    public void Dispose()
    {
        queue.Writer.TryComplete();
        try { writer.Wait(TimeSpan.FromSeconds(5)); } catch { /* shutting down */ }
    }

    private sealed class FileLogger(string category, FileLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= provider.options.MinimumLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var message = formatter(state, exception);
            var line = $"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss.fff}Z [{Short(logLevel)}] {category}: {message}";
            if (exception is not null) line += Environment.NewLine + exception;
            provider.Enqueue(line);
        }

        private static string Short(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRC", LogLevel.Debug => "DBG", LogLevel.Information => "INF",
            LogLevel.Warning => "WRN", LogLevel.Error => "ERR", LogLevel.Critical => "CRT", _ => "???",
        };
    }
}

/// <summary>Bound from "Logging:File". Off unless Enabled; the deploy switches it on.</summary>
public sealed class FileLoggerOptions
{
    public bool Enabled { get; set; }
    public string Path { get; set; } = "";
    public int RetainDays { get; set; } = 14;

    /// <summary>The floor for this sink, beneath the usual Logging:LogLevel category filters.</summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
}
