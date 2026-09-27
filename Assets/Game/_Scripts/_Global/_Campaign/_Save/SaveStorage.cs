/// <summary>
/// Swappable static locator for the active ISaveStorage backend. Defaults to PlayerPrefs; a
/// future platform build swaps SaveStorage.Backend for a platform-specific implementation before
/// CampaignStateManager.Awake() runs. Plain C# (no Unity lifecycle), so it's safe to use from both
/// runtime and Editor code. See docs/Campaign.md.
/// </summary>
public static class SaveStorage
{
    public static ISaveStorage Backend { get; set; } = new PlayerPrefsSaveStorage();
}
