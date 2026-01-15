using UnityEngine;

public class Maxhealth : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private float MaxHealthadd = 10f;
   

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            collision.GetComponent<PlayerLimbController>()
                     ?.IncreaseMaxHealth(MaxHealthadd);

            Destroy(gameObject);
        }
    }
}
