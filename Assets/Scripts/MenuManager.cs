using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject gamePanel;

    [Header("Buttons (optional)")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button resumeButton;

    [Header("Game")]
    [SerializeField] private CardController cardController;

    void Awake()
    {
        SetPanels(true);
    }

    void OnEnable()
    {
        if (newGameButton) newGameButton.onClick.AddListener(OnNewGameClicked);
        if (resumeButton) resumeButton.onClick.AddListener(OnResumeClicked);
        RefreshResumeVisibility();
    }

    void OnDisable()
    {
        if (newGameButton) newGameButton.onClick.RemoveListener(OnNewGameClicked);
        if (resumeButton) resumeButton.onClick.RemoveListener(OnResumeClicked);
    }

    public void RefreshResumeVisibility()
    {
        bool hasSave = cardController && cardController.HasSave();
        if (resumeButton) resumeButton.gameObject.SetActive(hasSave);
    }

    public void OnNewGameClicked() => StartCoroutine(StartNewGameRoutine());
    public void OnResumeClicked() => StartCoroutine(ResumeRoutine());
    public void PlaySmart() => StartCoroutine(PlaySmartRoutine());

    IEnumerator StartNewGameRoutine()
    {
        if (!cardController) yield break;
        yield return SwitchToGameAndRun(cardController.StartNewGame);
    }

    IEnumerator ResumeRoutine()
    {
        if (!cardController) yield break;
        yield return SwitchToGameAndRun(cardController.ResumeGame);
    }

    IEnumerator PlaySmartRoutine()
    {
        if (!cardController) yield break;
        yield return SwitchToGameAndRun(() =>
        {
            if (cardController.HasSave()) cardController.ResumeGame();
            else cardController.StartNewGame();
        });
    }

    IEnumerator SwitchToGameAndRun(Action action)
    {
        SetPanels(false);
        Canvas.ForceUpdateCanvases();
        yield return null;
        action?.Invoke();
    }

    public void BackToMenu()
    {
        if (cardController) cardController.ClearBoard();
        SetPanels(true);
        RefreshResumeVisibility();
    }

    public void QuitApp()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void SetPanels(bool menuActive)
    {
        if (mainMenuPanel) mainMenuPanel.SetActive(menuActive);
        if (gamePanel) gamePanel.SetActive(!menuActive);
    }
}
