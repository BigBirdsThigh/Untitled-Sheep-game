using UnityEngine;

public class BoidCulling : MonoBehaviour
{
    public float cullDistance = 50f; // Distance beyond which boids are culled

    private void Update()
    {
        foreach (Boid boid in BoidManager.Instance.boids)
        {
            if (boid == null) continue;
            
            float distance = Vector3.Distance(transform.position, boid.transform.position);
            boid.gameObject.SetActive(distance < cullDistance);
        }
    }
}
