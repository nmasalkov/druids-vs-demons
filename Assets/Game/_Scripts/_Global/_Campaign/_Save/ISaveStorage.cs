/// <summary>
/// Single-slot save storage backend. CampaignStateManager/RunState never touch PlayerPrefs (or any
/// other storage API) directly — they only ever go through SaveStorage.Backend, so a future
/// platform build (CrazyGames, Poki, ...) can swap the backend without touching the save data
/// model. See docs/Campaign.md.
/// </summary>
public interface ISaveStorage
{
    bool Exists();
    string Read();
    void Write(string data);
    void Delete();
}
