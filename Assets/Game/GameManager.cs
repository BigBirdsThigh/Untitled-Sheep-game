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
        Debug.Log("WIN! Game Paused. Press ENTER to start a new round.");
        Time.timeScale = 0f; // Pause the game
        gamePaused = true;
    }

    private void Update()
    {
        // When game is paused, pressing ENTER starts a new round
        if (gamePaused && Input.GetKeyDown(KeyCode.Return))
        {
            RestartRound();
        }
    }

    private void RestartRound()
    {
        int newBoidCount = Random.Range(5, 21); // Random between 5 and 20 boids

        Debug.Log($"New Round Started with {newBoidCount} Boids!");
        Time.timeScale = 1f; // Resume the game
        gamePaused = false;
        
        BoidManager.Instance?.ResetRound(newBoidCount); // Use new method for flexibility
    }
}
