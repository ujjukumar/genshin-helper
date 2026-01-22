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
            MessageBox.Show($"Startup Error: {ex.Message}", "AutoSkipper Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

internal class AutoSkipperContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly AutoSkipper _skipper;
    private readonly GlobalHooks _hooks;
    private readonly Thread _logicThread;

    public AutoSkipperContext(ScreenConfig config)
    {
        // Try to find the icon, fallback to system icon
        Icon appIcon = SystemIcons.Application;
        string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "auto_marker.ico"); // Assuming user might have an ico
        // If we want to use the png from bin/Assets as Icon, we'd need to convert it, but SystemIcons.Application is safer for now.
        
        _notifyIcon = new NotifyIcon
        {
            Icon = appIcon,
            Text = "Genshin AutoSkipper",
            Visible = true
        };

        // Context Menu
        var contextMenu = new ContextMenuStrip();
        
        var startItem = new ToolStripMenuItem("Start/Resume (F8)", null, (s, e) => _skipper.ToggleRun(true));
        var pauseItem = new ToolStripMenuItem("Pause (F9)", null, (s, e) => _skipper.ToggleRun(false));
        var toggleLogItem = new ToolStripMenuItem("Toggle Log File (F7)", null, (s, e) => Logger.ToggleFileLogging());
        var exitItem = new ToolStripMenuItem("Exit (F12)", null, Exit);

        contextMenu.Items.Add(startItem);
        contextMenu.Items.Add(pauseItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(toggleLogItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        
        // Double click to toggle run status
        _notifyIcon.DoubleClick += (s, e) => _skipper.ToggleRun(!_skipper.IsRunning);

        // Initialize Logic
        _skipper = new AutoSkipper(config);
        _hooks = new GlobalHooks(_skipper);

        _logicThread = new Thread(_skipper.Run)
        {
            IsBackground = true
        };
        _logicThread.Start();
        
        // Log startup
        Logger.LogSuccess("AutoSkipper started minimized to tray.");
        Logger.Log("Use F8/F9 or Tray Icon to control.");
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