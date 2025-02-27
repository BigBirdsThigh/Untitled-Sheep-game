using UnityEngine;
using System.Collections;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    public float timeRemaining;
    public float startTime = 180f;
    private bool timerRunning = false;
    private Coroutine timerCoroutine; // Store the running coroutine

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

    void Start()
    {
        StartTimer(2000f);
    }

    /// Starts the timer with the given duration in seconds.
    public void StartTimer(float duration)
    {
        if (duration <= 0) return;

        // If a timer is already running, log and stop it
        if (timerCoroutine != null)
        {
            Debug.LogWarning("[TimeManager] Existing timer detected! Stopping it before starting a new one.");
            StopCoroutine(timerCoroutine);
        }

        timeRemaining = duration;
        timerRunning = true;
        timerCoroutine = StartCoroutine(TimerTick());

        Debug.Log($"[TimeManager] Timer started: {duration} seconds.");
    }

    /// Stops the timer.
    public void StopTimer()
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }
        timerRunning = false;
        Debug.Log("[TimeManager] Timer stopped.");
    }

    /// Adds extra time to the timer.
    public void AddTime(float additionalTime)
    {
        if (additionalTime > 0)
        {
            timeRemaining += additionalTime;            
            Debug.Log($"[TimeManager] Added {additionalTime} seconds. New time: {timeRemaining}");
        }        
    }

    /// Coroutine that decreases time and triggers loss when reaching zero.
    private IEnumerator TimerTick()
    {
        while (timerRunning && timeRemaining > 0)
        {
            yield return new WaitForSeconds(1f); // Normal tick interval

            var (roaming, panicking, regrouping) = BoidManager.Instance.GetBoidStateCounts();
            int totalBoids = BoidManager.Instance.boids.Count;

            if (panicking > 0)
            {
                timeRemaining--; // Normal decrement
                UIManager.Instance?.SetNodeColour(Color.red); // Normal timer (red)
            }
            else if (roaming == totalBoids)
            {
                UIManager.Instance?.SetNodeColour(Color.green); // Timer paused (green)
                continue; // Skip decrement
            }
            else if (panicking == 0 && regrouping > 0) // No panic, but regrouping
            {
                timeRemaining -= 0.5f; // Slow down timer decrement
                UIManager.Instance?.SetNodeColour(Color.blue); // Slower decrement (blue)
            }
            else
            {
                timeRemaining--; // Default decrement
                UIManager.Instance?.SetNodeColour(Color.red);
            }
        }

        timerCoroutine = null; // Reset coroutine reference when finished
    }


    /// Handles what happens when the timer runs out.
    // private void TimerExpired()
    // {
    //     Debug.Log("[TimeManager] Timer expired!");

    //     if (GameManager.Instance != null && !GameManager.Instance.IsGamePaused())
    //     {
    //         GameManager.Instance.TriggerLose();
    //     }
    // }

    /// Returns the remaining time.
    public float GetRemainingTime()
    {
        return timeRemaining;
    }
}
