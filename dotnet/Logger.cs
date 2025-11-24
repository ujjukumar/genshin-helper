using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace AutoSkipper;

/// <summary>
/// A high-performance, asynchronous logger designed to minimize impact on the main application thread.
/// It uses a background thread to process and write log messages from a concurrent queue.
/// </summary>
public static class Logger
{
    private static readonly BlockingCollection<string> _logQueue = new(new ConcurrentQueue<string>());
    private static readonly Task _consumerTask;

    private static bool _verbose = false;
    private static bool _fileLogging = false;
    private static StreamWriter? _logWriter;
    private static readonly Lock _fileLock = new(); // Lock for toggling file logging

    /// <summary>
    /// Static constructor to initialize the logger and start the background consumer task.
    /// </summary>
    static Logger()
    {
        _consumerTask = Task.Run(ProcessQueue);
    }

    /// <summary>
    /// The main processing loop for the logger. Runs on a background thread.
    /// Dequeues messages and writes them to the console and/or a file.
    /// </summary>
    private static void ProcessQueue()
    {
        // This loop will run until the queue is marked as complete for adding.
        foreach (string message in _logQueue.GetConsumingEnumerable())
        {
            try
            {
                Console.WriteLine(message);

                if (_fileLogging)
                {
                    lock (_fileLock)
                    {
                        // Note: StreamWriter is not thread-safe, but since this is the only
                        // thread writing and _fileLock protects it during toggling, it's safe.
                        _logWriter?.WriteLine(message);
                    }
                }
            }
            catch (Exception ex)
            {
                // If logging itself fails, write an error to the console.
                Console.WriteLine($"[FATAL] Logger failed: {ex.Message}");
            }
        }

        // After the loop, ensure the writer is flushed and disposed.
        lock (_fileLock)
        {
            _logWriter?.Flush();
            _logWriter?.Dispose();
            _logWriter = null;
        }
    }

    /// <summary>
    /// Adds a message to the logging queue. This method returns immediately.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Log(string message)
    {
        // Don't block the caller if the queue is full (though unlikely with default unbounded size).
        _logQueue.TryAdd($"{DateTime.Now:HH:mm:ss.fff} | {message}");
    }

    /// <summary>
    /// Adds a debug message to the queue if verbose mode is enabled.
    /// The message factory is only evaluated if needed, saving performance.
    /// </summary>
    /// <param name="messageFactory">A function that produces the message to log.</param>
    public static void LogDebug(Func<string> messageFactory)
    {
        if (_verbose)
        {
            Log($"[DEBUG] {messageFactory()}");
        }
    }

    public static void SetVerbose(bool verbose)
    {
        _verbose = verbose;
        if (verbose) Log("Verbose mode enabled");
    }

    /// <summary>
    /// Enables or disables file logging. This operation is thread-safe.
    /// </summary>
    public static void ToggleFileLogging()
    {
        lock (_fileLock)
        {
            _fileLogging = !_fileLogging;
            if (_fileLogging)
            {
                try
                {
                    // Initialize the StreamWriter. AutoFlush is false for performance.
                    // The consumer task will handle flushing periodically.
                    _logWriter = new StreamWriter("autoskip_dialogue.log", true, Encoding.UTF8) { AutoFlush = false };
                    Log("File logging enabled.");
                }
                catch (Exception ex)
                {
                    Log($"[ERROR] Failed to enable file logging: {ex.Message}");
                    _fileLogging = false;
                }
            }
            else
            {
                Log("File logging disabled.");
                // The consumer will see _fileLogging is false. Flush and null out the writer.
                _logWriter?.Flush();
                _logWriter?.Dispose();
                _logWriter = null;
            }
        }
    }

    /// <summary>
    /// Signals the logger to process any remaining messages and shut down gracefully.
    /// This should be called before the application exits.
    /// </summary>
    public static void Shutdown()
    {
        Log("Logger shutting down...");
        // Mark the queue as "complete", which will cause GetConsumingEnumerable to exit
        // once the queue is empty.
        _logQueue.CompleteAdding();

        // Wait for the consumer task to finish processing all items.
        // A timeout is added as a safeguard against unforeseen hangs.
        _consumerTask.Wait(2000);
    }
}
