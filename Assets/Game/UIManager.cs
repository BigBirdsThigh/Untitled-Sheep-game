using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Timer UI")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI addedTimeText;

    [Header("Win UI")]
    public GameObject winScreen;
    public Button nextRoundButton;

    [Header("Round UI")]
    public TextMeshProUGUI roundText; // Displays the round number

    [Header("Game Over UI")]
    public GameObject gameOverScreen;
    public Button restartButton;

    private float addedTimeValue = 0f;
    private Coroutine timerCoroutine;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        winScreen.SetActive(false);
        gameOverScreen.SetActive(false);
        addedTimeText.gameObject.SetActive(false);

        nextRoundButton.onClick.AddListener(StartNextRound);
        restartButton.onClick.AddListener(RestartGame);

        // Start timer update as a coroutine (updates every second)
        timerCoroutine = StartCoroutine(UpdateTimerUI());
    }

    IEnumerator UpdateTimerUI()
    {
        while (true)
        {
            if (TimeManager.Instance != null)
            {
                float timeRemaining = TimeManager.Instance.GetRemainingTime();
                timerText.text = $"Time: {Mathf.Ceil(timeRemaining)}";
            }
            yield return new WaitForSeconds(1f); // Update every second
        }
    }


    public void UpdateRoundUI(int round)
    {
        if (roundText != null)
        {
            roundText.text = $"Round: {round}";
        }
    }

    private Coroutine fadeCoroutine;

    public void AddTimeEffect(float time)
    {
        addedTimeValue += time;
        addedTimeText.text = $"+{addedTimeValue}";
        addedTimeText.alpha = 1f; // Ensure it's fully visible
        addedTimeText.gameObject.SetActive(true);

        // If already fading, reset the coroutine
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeAddedTime());
    }

    IEnumerator FadeAddedTime()
    {
        yield return new WaitForSeconds(1.5f);

        float fadeDuration = 0.5f;
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            addedTimeText.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        addedTimeText.gameObject.SetActive(false);
        addedTimeValue = 0; // Reset accumulated time
    }


    public void ShowWinScreen()
    {
        winScreen.SetActive(true);
        Time.timeScale = 0f; // Pause game (only once)
    }

    public void ShowGameOverScreen()
    {
        gameOverScreen.SetActive(true);
        Time.timeScale = 0f;
    }

    public void StartNextRound()
    {
        winScreen.SetActive(false);
        Time.timeScale = 1f; // Resume game
        GameManager.Instance?.StartNextRound();
    }

    public void RestartGame()
    {
        gameOverScreen.SetActive(false);
        Time.timeScale = 1f;
        GameManager.Instance?.RestartRound();
    }
}
