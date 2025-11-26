using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace AutoSkipper;

// A high-performance, asynchronous logger designed to minimize impact on the main application thread.
// It uses a background thread to process and write log messages from a concurrent queue.
public static class Logger
{
    private enum LogLevel { Info, Debug, Error, Success, Warning }

    private readonly struct LogEntry
    {
        public readonly DateTime Timestamp;
        public readonly LogLevel Level;
        public readonly string Message;

        public LogEntry(LogLevel level, string message)
        {
            Timestamp = DateTime.Now;
            Level = level;
            Message = message;
        }
    }

    private static readonly BlockingCollection<LogEntry> _logQueue = new(new ConcurrentQueue<LogEntry>());
    private static readonly Task _consumerTask;

    private static bool _verbose = false;
    private static volatile StreamWriter? _logWriter; // Volatile for thread-safe reads
    private static readonly object _fileLock = new(); // Lock for creating/disposing the writer

    static Logger()
    {
        _consumerTask = Task.Run(ProcessQueue);
    }

    private static void ProcessQueue()
    {
        foreach (var entry in _logQueue.GetConsumingEnumerable())
        {
            try
            {
                string timestamp = $"[grey]{entry.Timestamp:HH:mm:ss.fff}[/]";
                string level = entry.Level switch
                {
                    LogLevel.Info => "[blue]INFO[/]",
                    LogLevel.Debug => "[grey]DEBUG[/]",
                    LogLevel.Error => "[red]ERROR[/]",
                    LogLevel.Success => "[green]SUCCESS[/]",
                    LogLevel.Warning => "[yellow]WARN[/]",
                    _ => "[white]LOG[/]"
                };
                
                string message = entry.Message;
                // Basic markup escaping if needed, though we generally trust internal messages
                // message = Markup.Escape(message); 

                AnsiConsole.MarkupLine($"{timestamp} | {level} | {message}");
                
                // File logging remains plain text
                _logWriter?.WriteLine($"{entry.Timestamp:HH:mm:ss.fff} | {entry.Level.ToString().ToUpper()} | {entry.Message}");
            }
            catch (Exception ex)
            {
                // Fallback if Spectre fails
                Console.WriteLine($"[FATAL] Logger failed: {ex.Message}");
            }
        }
    }

    public static void Log(string message)
    {
        _logQueue.TryAdd(new LogEntry(LogLevel.Info, message));
    }
    
    public static void LogSuccess(string message)
    {
        _logQueue.TryAdd(new LogEntry(LogLevel.Success, message));
    }
    
    public static void LogWarning(string message)
    {
        _logQueue.TryAdd(new LogEntry(LogLevel.Warning, message));
    }
    
    public static void LogError(string message)
    {
        _logQueue.TryAdd(new LogEntry(LogLevel.Error, message));
    }

    public static void LogDebug(Func<string> messageFactory)
    {
        if (_verbose)
        {
            _logQueue.TryAdd(new LogEntry(LogLevel.Debug, messageFactory()));
        }
    }

    public static void SetVerbose(bool verbose)
    {
        _verbose = verbose;
        if (verbose) Log("Verbose mode enabled");
    }

    public static void ToggleFileLogging()
    {
        // This is the only place where the writer is assigned or disposed, so we lock here.
        lock (_fileLock)
        {
            if (_logWriter == null)
            {
                try
                {
                    // Use a buffer for performance and manually flush in the shutdown.
                    var stream = new FileStream("autoskip_dialogue.log", FileMode.Append, FileAccess.Write, FileShare.Read);
                    _logWriter = new StreamWriter(stream, Encoding.UTF8, 4096) { AutoFlush = false };
                    LogSuccess("File logging enabled.");
                }
                catch (Exception ex)
                {
                    LogError($"Failed to enable file logging: {ex.Message}");
                }
            }
            else
            {
                Log("File logging disabled.");
                _logWriter.Flush();
                _logWriter.Dispose();
                _logWriter = null;
            }
        }
    }

    public static void Shutdown()
    {
        Log("Logger shutting down...");
        _logQueue.CompleteAdding();

        // Wait for the consumer to finish.
        _consumerTask.Wait(1500);

        // Final flush and close of the file writer.
        lock (_fileLock)
        {
            _logWriter?.Flush();
            _logWriter?.Dispose();
            _logWriter = null;
        }
    }
}
