using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button restartButton;

    private bool _isPaused;

    void Start()
    {
        restartButton.onClick.AddListener(HandleRestartClicked);
        pausePanel.SetActive(false);
    }

    void OnDestroy()
    {
        restartButton.onClick.RemoveListener(HandleRestartClicked);
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    private void TogglePause()
    {
        _isPaused = !_isPaused;
        pausePanel.SetActive(_isPaused);
        Time.timeScale = _isPaused ? 0f : 1f;
    }

    private void HandleRestartClicked()
    {
        Game._Scripts.Global.GameManager.Instance.RestartBattle();
        _isPaused = false;
        pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }
}
