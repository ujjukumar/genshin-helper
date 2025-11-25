using System;
using System.IO;
using System.Text.Json;

namespace AutoSkipper;

// --- 3. Configuration ---
internal class ScreenConfig
{
    public int WIDTH { get; private set; }
    public int HEIGHT { get; private set; }
    public (int x, int y) PLAYING_ICON;
    public (int x, int ly, int hy) DIALOGUE_ICON;
    public (int x, int y) LOADING_PIXEL;
    public string WINDOW_TITLE;

    private static ConfigData _configData = new();

    public static ScreenConfig Load()
    {
        if (File.Exists("config.json"))
        {
            try
            {
                var json = File.ReadAllText("config.json");
                _configData = JsonSerializer.Deserialize(json, JsonContext.Default.ConfigData) ?? new ConfigData();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config.json: {ex.Message}");
                Console.WriteLine("Using default configuration.");
                _configData = new ConfigData();
            }
        }
        else
        {
            Console.WriteLine("config.json not found. Creating with default values.");
            var json = JsonSerializer.Serialize(new ConfigData(), JsonContext.Default.ConfigData);
            File.WriteAllText("config.json", json);
            _configData = new ConfigData();
        }

        int w = Native.GetSystemMetrics(0); // SM_CXSCREEN
        int h = Native.GetSystemMetrics(1); // SM_CYSCREEN
        
        // The logic for asking for resolution can be kept if needed, or removed.
        // For now, I will remove it to simplify the changes.
        // The user can modify the .env file if needed, or we can add it to config.json
        
        return new ScreenConfig(w, h);
    }
    
    public ScreenConfig(int w, int h)
    {
        WIDTH = w; 
        HEIGHT = h;
        WINDOW_TITLE = _configData.WindowTitle;
        PLAYING_ICON = CalcPlayingIcon();
        DIALOGUE_ICON = CalcDialogueIcon();
        LOADING_PIXEL = (Wa(_configData.LoadingPixelX), Ha(_configData.LoadingPixelY));
    }

    private int Wa(int x) => (int)(x / (float)_configData.BaseWidth * WIDTH);
    private int Ha(int y) => (int)(y / (float)_configData.BaseHeight * HEIGHT);

    private int ScalePos(int hd, int doubleHd, float extra = 0.0f)
    {
        if (WIDTH <= 3840) extra = 0.0f;
        int diff = doubleHd - hd;
        return (int)(hd + (WIDTH - _configData.BaseWidth) * ((diff / (float)_configData.BaseWidth) + extra));
    }

    private (int, int) CalcPlayingIcon()
    {
        bool ws = WIDTH > _configData.BaseWidth && Math.Abs((double)HEIGHT/WIDTH - 0.5625) > 0.001;
        int x = ws ? Math.Min(ScalePos(_configData.PlayingIconX, _configData.PlayingIconWsX), _configData.PlayingIconWsX) : Wa(_configData.PlayingIconX);
        return (x, Ha(_configData.PlayingIconY));
    }

    private (int, int, int) CalcDialogueIcon()
    {
        bool ws = WIDTH > _configData.BaseWidth && Math.Abs((double)HEIGHT/WIDTH - 0.5625) > 0.001;
        int x = ws ? ScalePos(_configData.DialogueIconX, _configData.DialogueIconWsX, _configData.DialogueIconWsExtra) : Wa(_configData.DialogueIconX);
        int ly = ws ? Ha(_configData.DialogueIconWsLowY) : Ha(_configData.DialogueIconLowY);
        int hy = ws ? Ha(_configData.DialogueIconWsHighY) : Ha(_configData.DialogueIconHighY);
        return (x, ly, hy);
    }
}
