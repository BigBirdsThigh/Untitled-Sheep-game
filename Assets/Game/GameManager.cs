using UnityEngine;
using Cinemachine;


public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Player Management")]
    public GameObject playerPrefab;
    public Transform spawnPoint;
    private GameObject currPlayer;

    [Header("Round System")]
    public int currRound = 1; // start at round 1    
    public float startingTime = 180f; // first timer
    private bool roundActive = false; // Ensure rounds don’t check win too early


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


    // private void Start(){
    //     SpawnPlayer();
    //     UIManager.Instance?.UpdateRoundUI(currRound);
    // }

    private void Start()
    {
        StartFirstRound();
    }

    private void StartFirstRound()
    {
        SpawnPlayer();
        roundActive = true;
        UIManager.Instance?.UpdateRoundUI(currRound);

        BoidManager.Instance?.ResetRound(10); // hardcoding 1st boids, for some reason round 1 wouldn't spawn properly
        TimeManager.Instance?.StartTimer(startingTime);
    }


    public void CheckWin()
    {
        if (!roundActive) return; // Avoid early checks before a round starts

        int remainingBoids = BoidManager.Instance?.boids.Count ?? 0;

        if (remainingBoids == 0)
        {
            Debug.Log("All boids eliminated. WIN!");
            roundActive = false;
            TriggerWin();
        }
    }

    private int CalculateBoidCount(int round)
    {
        return Mathf.FloorToInt(10 * Mathf.Pow(1.2f, round) + 5 * round);
    }


    public void CheckLoseCondition()
    {
        if (!roundActive) return; // Only check when the round is active

        float remainingTime = TimeManager.Instance?.GetRemainingTime() ?? 0f;
        if (remainingTime <= 0)
        {
            Debug.Log("Time ran out. LOSE!");
            roundActive = false;
            TriggerLose();
        }
    }



    public void SpawnPlayer(){
        if (playerPrefab == null){
            Debug.LogError("Player prefab or spawnpoint not assigned");
        }

        if (currPlayer != null){ // ensure only 1 player exists
            Destroy(currPlayer); 
        }

        currPlayer = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);

        CinemachineVirtualCamera playerCam = currPlayer.GetComponentInChildren<CinemachineVirtualCamera>();

        if (playerCam != null){
            playerCam.Priority = 30;
        }
        else{
            Debug.LogError("NO CAMERA FOUND");
        }

        BoidManager.Instance?.SetPlayer(currPlayer);
    }


    /*
    This should:
    - Pause the game
    - Pause the timer (if applicable)
    - Tell the UI Manager to enable the upgrade screen (if implemented)
    */
    public void TriggerWin()
    {

        if(gamePaused) return;

        Debug.Log("WIN! Displaying upgrade screen.");
        TimeManager.Instance?.StopTimer();
        UIManager.Instance?.ShowWinScreen();
        currRound++;
        UIManager.Instance?.UpdateRoundUI(currRound);
        gamePaused = true;
    }



    public void TriggerLose()
    {
        if (gamePaused) return;
        
        Debug.Log("GAME OVER! Displaying restart screen.");
        UpgradeManager.Instance.ResetUpgrades(); // Reset upgrades when the game is lost.
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

        if (Input.GetKeyDown(KeyCode.P))
        {
            Cursor.lockState = CursorLockMode.None;  // Unlock cursor when triggering cheat
            Cursor.visible = true;

            BoidManager.Instance?.DestroyAllBoids();
            TriggerWin();
        }


        if (Input.GetKeyDown(KeyCode.L))
        {
            Cursor.lockState = CursorLockMode.None;  // Unlock cursor when triggering cheat
            Cursor.visible = true;
            
            TriggerLose();
        }


        if (roundActive)
        {
            CheckLoseCondition(); // Time-based loss check
        }
    }


    public void StartNextRound(){
        
        Time.timeScale = 1f; // start game
        gamePaused = false;
        roundActive = true;

        int newBoidCount = CalculateBoidCount(currRound);
        BoidManager.Instance?.ResetRound(newBoidCount);
        float currTime = TimeManager.Instance?.GetRemainingTime() ?? startingTime; // default to starting time if the time somehow is not available

        PlayerTest playerScript = currPlayer.GetComponent<PlayerTest>();
        if (playerScript != null)
        {
            playerScript.ApplyUpgrades();
        }        

        TimeManager.Instance?.StartTimer(currTime);
    }

    public void RestartRound()
    {
        Time.timeScale = 1f; // Resume the game
        gamePaused = false;
        roundActive = true;        
        
        currRound = 1;

        UIManager.Instance?.UpdateRoundUI(currRound);
        int newBoidCount = CalculateBoidCount(currRound); // start with initial boid count
        // Respawn the player
        SpawnPlayer();
        
        
        BoidManager.Instance?.ResetRound(newBoidCount);
        TimeManager.Instance?.StartTimer(startingTime);
    }
}
