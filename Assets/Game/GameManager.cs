using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private bool gamePaused = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /*
    This should:
    - Pause the game
    - Pause the timer (if applicable)
    - Tell the UI Manager to enable the upgrade screen (if implemented)
    */
    public void TriggerWin()
    {
        Debug.Log("WIN! Displaying upgrade screen.");
        TimeManager.Instance?.StopTimer();
        UIManager.Instance?.ShowWinScreen();
    }



    public void TriggerLose()
    {
        if (gamePaused) return;
        
        Debug.Log("GAME OVER! Displaying restart screen.");
        Time.timeScale = 0f;
        gamePaused = true;
        UIManager.Instance?.ShowGameOverScreen();
    }


    public bool IsGamePaused()
    {
        return gamePaused;
    }



    private void Update()
    {

        //Debug.Log("GameManager Update Running");
        // When game is paused, pressing ENTER starts a new round
        // if (gamePaused && Input.GetKeyDown(KeyCode.Return))
        // {
        //     float remainingTime = TimeManager.Instance?.GetRemainingTime() ?? 0f;
        //     TimeManager.Instance?.StartTimer(remainingTime);
        //     RestartRound();
        // }
    }

    public void RestartRound()
    {
        int newBoidCount = Random.Range(5, 21); // Random between 5 and 20 boids

        Debug.Log($"New Round Started with {newBoidCount} Boids!");
        Time.timeScale = 1f; // Resume the game
        gamePaused = false;
        
        BoidManager.Instance?.ResetRound(newBoidCount); // Use new method for flexibility
        TimeManager.Instance?.StartTimer(20f);
    }
}
