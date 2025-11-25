namespace AutoSkipper;

internal class ConfigData
{
    public string WindowTitle { get; set; } = "Genshin Impact";
    public int BaseWidth { get; set; } = 1920;
    public int BaseHeight { get; set; } = 1080;

    public int PlayingIconX { get; set; } = 84;
    public int PlayingIconY { get; set; } = 46;

    public int DialogueIconX { get; set; } = 1301;
    public int DialogueIconLowY { get; set; } = 808;
    public int DialogueIconHighY { get; set; } = 790;
    public int DialogueIconWsLowY { get; set; } = 810;
    public int DialogueIconWsHighY { get; set; } = 792;
    
    public int LoadingPixelX { get; set; } = 1200;
    public int LoadingPixelY { get; set; } = 700;
    
    public int PlayingIconWsX { get; set; } = 230;
    public int DialogueIconWsX { get; set; } = 2770;
    public float DialogueIconWsExtra { get; set; } = 0.02f;
}
