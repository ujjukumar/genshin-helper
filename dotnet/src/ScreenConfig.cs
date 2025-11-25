using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace AutoSkipper;

// --- 3. Configuration ---
internal class ScreenConfig
{
    public int WIDTH { get; }
    public int HEIGHT { get; }
    public (int x, int y) PLAYING_ICON { get; }
    public (int x, int ly, int hy) DIALOGUE_ICON { get; }
    public (int x, int y) LOADING_PIXEL { get; }
    public string WINDOW_TITLE { get; }

    private readonly ConfigData _configData;

    public static async Task<ScreenConfig> CreateAsync()
    {
        var configData = await LoadConfigAsync();
        
        int w = Native.GetSystemMetrics(0); // SM_CXSCREEN
        int h = Native.GetSystemMetrics(1); // SM_CYSCREEN
        
        return new ScreenConfig(w, h, configData);
    }
    
    private static async Task<ConfigData> LoadConfigAsync()
    {
        if (File.Exists("config.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync("config.json");
                return JsonSerializer.Deserialize(json, JsonContext.Default.ConfigData) ?? new ConfigData();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error loading config.json: {ex.Message}");
                Logger.Log("Using default configuration.");
                return new ConfigData();
            }
        }
        else
        {
            Logger.Log("config.json not found. Creating with default values.");
            var defaultConfig = new ConfigData();
            var json = JsonSerializer.Serialize(defaultConfig, JsonContext.Default.ConfigData);
            await File.WriteAllTextAsync("config.json", json);
            return defaultConfig;
        }
    }

    private ScreenConfig(int w, int h, ConfigData configData)
    {
        _configData = configData;
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
