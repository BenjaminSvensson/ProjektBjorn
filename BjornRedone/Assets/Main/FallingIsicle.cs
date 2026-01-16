using UnityEngine;

public class FallingIcicle : MonoBehaviour
{
    public float damage = 15f;
    public float targetY; // The Y position where it hits the floor
    public GameObject shatterEffect; // Optional particle effect

    void Update()
    {
        // If we fall past the target Y, destroy
        if (transform.position.y <= targetY)
        {
            if (shatterEffect) Instantiate(shatterEffect, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            var limb = other.GetComponent<PlayerLimbController>();
            if (limb) limb.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}