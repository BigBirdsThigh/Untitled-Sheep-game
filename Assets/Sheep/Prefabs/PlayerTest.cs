using UnityEngine;

public class PlayerTest : MonoBehaviour
{
    public float damage = 2f; // Damage dealt per hit
    public float impactForce = 5f; // Force applied to boid on collision

    void OnCollisionEnter(Collision collision)
    {
        // Check if we collided with a Boid
        Boid boid = collision.collider.GetComponent<Boid>();
        if (boid != null)
        {
            // Apply damage
            boid.TakeDamage(damage);

            // Apply force to push the boid away
            Rigidbody boidRb = boid.GetComponent<Rigidbody>();
            if (boidRb != null)
            {
                Vector3 forceDirection = (boid.transform.position - transform.position).normalized;
                boidRb.AddForce(forceDirection * impactForce, ForceMode.Impulse);
            }
        }
    }
}
