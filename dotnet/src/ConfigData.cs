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

    // --- Timing & Probabilities (Simplified) ---
    public double StandardDelayMin { get; set; } = 0.13;
    public double StandardDelayMax { get; set; } = 0.17;
    
    public double FastBurstChance { get; set; } = 1.0 / 40.0; // ~2.5%
    
    public double BreakChance { get; set; } = 0.015; // 1.5%
    public double BreakDurationMin { get; set; } = 2.0;
    public double BreakDurationMax { get; set; } = 8.0;

    public double SpaceKeyChance { get; set; } = 0.1;
    
    public int BurstModeDelayMs { get; set; } = 120;
}
