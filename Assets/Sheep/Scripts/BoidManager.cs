using UnityEngine;
using System.Collections.Generic;

public class BoidManager : MonoBehaviour
{
    public Boid boidPrefab;
    public int boidCount = 20;
    public float spawnRadius = 5f;
    public Transform target;
    public float debugPercentage = 0.25f; // 25% of boids will have debug


    [Header("Debugging")]
    public bool DebugPlayerDetection = false;
    public bool DebugObstacleAvoidance = false;
    
    [HideInInspector]
    public List<Boid> boids = new List<Boid>();

    void Start()
    {
        SpawnBoids();
    }


    void FixedUpdate()
    {
        // Clean up any destroyed boids before running logic
        boids.RemoveAll(b => b == null);

        int regroupingBoids = GetRegroupingCount();
        int totalBoids = boids.Count;

        if (regroupingBoids >= totalBoids / 2)
        {
            // Uncomment to actually slow the timer - once it is implemented
            // GameManager.Instance.SlowTimer();
        }

        float requiredRegroupingPercentage = 0.7f;
        int regroupingBoidsNearCenter = GetRegroupingBoidsNearManager();

        if (regroupingBoidsNearCenter >= totalBoids * requiredRegroupingPercentage)
        {
            foreach (Boid boid in boids)
            {
                if (boid != null)
                {
                    boid.ChangeState(BoidState.Roaming);
                    boid.ChangeTarget(target);
                }
            }
        }
    }


    public int GetRegroupingCount()
    {
        return boids.FindAll(b => b.currentState == BoidState.Regrouping).Count;
    }
    
    public int GetRegroupingBoidsNearManager()
    {
        float regroupingRadius = 5f; // Might need tweaked who knows
        return boids.FindAll(b => b.currentState == BoidState.Regrouping &&
                                Vector3.Distance(b.transform.position, transform.position) <= regroupingRadius).Count;
    }


    void SpawnBoids()
    {
        for (int i = 0; i < boidCount; i++)
        {
            Vector3 spawnPos = transform.position + Random.insideUnitSphere * spawnRadius;
            spawnPos.y = 1f;
            Boid newBoid = Instantiate(boidPrefab, spawnPos, Quaternion.identity, transform);
            newBoid.target = target;
            boids.Add(newBoid);
        }

        if (DebugPlayerDetection){
            int debugBoids = Mathf.RoundToInt(boidCount * debugPercentage);
            for (int i = 0; i < debugBoids; i++)
            {            
                
                boids[i].debugPlayerDetection = true;            
            }
        }
        if (DebugObstacleAvoidance){
            int debugBoids = Mathf.RoundToInt(boidCount * debugPercentage);
            for (int i = 0; i < debugBoids; i++){
                boids[i].debugAvoidance = true;
            }
        }
        
    }
    


    public void RemoveBoid(Boid boid)
    {
        boids.Remove(boid);

        // Uncomment to check for win condition - when implemented
        // if (boids.Count == 0) GameManager.Instance.TriggerWin();
    }

    public bool AnyBoidsPanicking()
    {
        return boids.Exists(b => b.currentState == BoidState.Panicking);
    }



}
