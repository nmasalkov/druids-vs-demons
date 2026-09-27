using UnityEngine;

/// <summary>
/// Default local ISaveStorage backend — PlayerPrefs. Same key CampaignStateManager used to write
/// directly, so existing dev saves stay valid. See docs/Campaign.md.
/// </summary>
public class PlayerPrefsSaveStorage : ISaveStorage
{
    private const string Key = "DvD_RunState";

    public bool Exists() => PlayerPrefs.HasKey(Key);
    public string Read() => PlayerPrefs.GetString(Key);
    public void Write(string data) => PlayerPrefs.SetString(Key, data);
    public void Delete() => PlayerPrefs.DeleteKey(Key);
}
