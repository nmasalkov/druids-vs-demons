using Game._Scripts.Global;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Escape-toggled pause overlay with Restart and Go to map. On defeat (CampaignManager.OnDefeatResolved)
/// the same panel shows *locked* — Escape can't dismiss it, so the player has to pick Restart or Go
/// to map. Any battle restart (button, debug tool) unlocks and hides it. See docs/GameLoop.md.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button goToMapButton;

    private bool _isPaused;
    private bool _isLocked;

    void Start()
    {
        restartButton.onClick.AddListener(RestartEncounter);
        goToMapButton.onClick.AddListener(GoToMap);
        CampaignManager.OnDefeatResolved += ShowLocked;
        GameManager.OnBattleRestart += Hide;
        pausePanel.SetActive(false);
    }

    void OnDestroy()
    {
        restartButton.onClick.RemoveListener(RestartEncounter);
        goToMapButton.onClick.RemoveListener(GoToMap);
        CampaignManager.OnDefeatResolved -= ShowLocked;
        GameManager.OnBattleRestart -= Hide;
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (_isLocked) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    private void TogglePause()
    {
        _isPaused = !_isPaused;
        pausePanel.SetActive(_isPaused);
        Time.timeScale = _isPaused ? 0f : 1f;
    }

    private void ShowLocked()
    {
        _isLocked = true;
        _isPaused = true;
        pausePanel.SetActive(true);
        Time.timeScale = 0f;
    }

    private void Hide()
    {
        _isLocked = false;
        _isPaused = false;
        pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    // RestartBattle() fires OnBattleRestart, which hides/unlocks the panel via Hide().
    private void RestartEncounter() => CampaignManager.Instance.ResetCurrentEncounter();

    private void GoToMap() => CampaignManager.Instance.AbandonEncounter();
}
