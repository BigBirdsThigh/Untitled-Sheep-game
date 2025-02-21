using UnityEngine;
using System.Collections.Generic;

public class BoidManager : MonoBehaviour
{
    public Boid boidPrefab;
    public int boidCount = 20;
    public float spawnRadius = 5f;
    public Transform target;
    
    [HideInInspector]
    public List<Boid> boids = new List<Boid>();

    void Start()
    {
        SpawnBoids();
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

        // Enable debugAvoidance for 5 random boids
        for (int i = 0; i < Mathf.Min(5, boids.Count); i++)
        {
            boids[i].debugAvoidance = true;
        }
    }
}
