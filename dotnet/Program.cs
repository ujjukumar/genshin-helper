using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Spectre.Console;

namespace AutoSkipper;

internal class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Parse command line arguments
        bool verbose = false;
        foreach (var arg in args)
        {
            if (arg == "-v" || arg == "--verbose") verbose = true;
        }
        
        Logger.SetVerbose(verbose);
        
        ApplicationConfiguration.Initialize();

        try
        {
            // Initialize configuration (synchronously for startup)
            var config = ScreenConfig.CreateAsync().GetAwaiter().GetResult();
            
            // Run the application context
            using var context = new AutoSkipperContext(config);
            Application.Run(context);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "AutoSkipper Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

internal class AutoSkipperContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly AutoSkipper _skipper;
    private readonly GlobalHooks _hooks;
    private readonly Thread _logicThread;
    private readonly LogForm _logForm;
    private Icon _defaultIcon = SystemIcons.Application;
    private Icon _activeIcon = SystemIcons.Asterisk;

    public AutoSkipperContext(ScreenConfig config)
    {
        LoadIcons();
        
        _notifyIcon = new NotifyIcon
        {
            Icon = _defaultIcon,
            Text = "Genshin AutoSkipper - Ready",
            Visible = true
        };

        _logForm = new LogForm();

        var contextMenu = new ContextMenuStrip();
        
        var startItem = new ToolStripMenuItem("Start/Resume (F8)", null, (s, e) => _skipper!.ToggleRun(true));
        var pauseItem = new ToolStripMenuItem("Pause (F9)", null, (s, e) => _skipper!.ToggleRun(false));
        var toggleLogItem = new ToolStripMenuItem("Toggle Log File (F7)", null, (s, e) => Logger.ToggleFileLogging());
        var showLogsItem = new ToolStripMenuItem("Show Logs", null, (s, e) => ShowLogs());
        var exitItem = new ToolStripMenuItem("Exit", null, Exit);

        contextMenu.Items.Add(startItem);
        contextMenu.Items.Add(pauseItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(showLogsItem);
        contextMenu.Items.Add(toggleLogItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        
        _notifyIcon.DoubleClick += (s, e) => _skipper!.ToggleRun(!_skipper!.IsRunning);

        _skipper = new AutoSkipper(config);
        _hooks = new GlobalHooks(_skipper);

        _skipper.OnDialogueStateChanged += OnDialogueStateChanged;
        _skipper.OnRunningStateChanged += OnRunningStateChanged;

        _logicThread = new Thread(_skipper.Run)
        {
            IsBackground = true
        };
        _logicThread.Start();
        
        Logger.LogSuccess("AutoSkipper started minimized to tray.");
        Logger.Log("Use F8/F9 or Tray Icon to control.");
    }

    private void LoadIcons()
    {
        string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoSkipper");
        string iconPath = Path.Combine(appDataPath, "auto_marker.ico");
        if (!File.Exists(iconPath))
        {
            iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "auto_marker.ico");
        }
        if (File.Exists(iconPath))
        {
            try
            {
                using var stream = File.OpenRead(iconPath);
                _defaultIcon = new Icon(stream);
                _activeIcon = _defaultIcon;
            }
            catch { }
        }
    }

    private void OnDialogueStateChanged(bool inDialogue)
    {
        if (_notifyIcon == null) return;
        _notifyIcon.Icon = inDialogue ? _activeIcon : _defaultIcon;
        UpdateTooltip();
    }

    private void OnRunningStateChanged(bool running)
    {
        if (_notifyIcon == null) return;
        UpdateTooltip();
    }

    private void UpdateTooltip()
    {
        if (_skipper == null || _notifyIcon == null) return;
        string status = _skipper.IsRunning ? "Running" : "Paused";
        _notifyIcon.Text = $"Genshin AutoSkipper - {status}";
    }

    private void ShowLogs()
    {
        if (_logForm.Visible)
        {
            _logForm.Hide();
        }
        else
        {
            _logForm.Show();
            _logForm.BringToFront();
        }
    }

    private void Exit(object? sender, EventArgs e)
    {
        // This will break the Application.Run loop eventually
        // But we manually clean up first
        Cleanup();
        Application.Exit();
    }

    private void Cleanup()
    {
        _notifyIcon.Visible = false;
        
        if (!_logForm.IsDisposed)
        {
            _logForm.ForceClose();
        }

        _skipper.ShouldExit = true;
        _skipper.Wake();

        if (_logicThread.IsAlive)
        {
            _logicThread.Join(500);
        }

        _hooks.Dispose();
        _skipper.Dispose();
        Logger.Shutdown();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Cleanup();
            _notifyIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}