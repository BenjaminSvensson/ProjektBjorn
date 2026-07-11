using UnityEngine;

public class HealingPickup : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] float addhealth = 50f;
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerLimbController plc =
                collision.GetComponent<PlayerLimbController>();

            plc?.Heal(addhealth);

            Destroy(gameObject);
        }
    }

}
