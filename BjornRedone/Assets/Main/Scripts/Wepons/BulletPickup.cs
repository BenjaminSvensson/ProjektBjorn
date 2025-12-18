using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class BulletPickup : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Amount of reserve bullets to add.")]
    [SerializeField] private int ammoAmount = 10;
    [SerializeField] private AudioClip pickupSound;
    
    [Header("Visuals")]
    [SerializeField] private bool rotate = true;
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float bobAmount = 0.1f;

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        if (rotate)
        {
            transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
        }
        
        float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            WeaponSystem ws = other.GetComponent<WeaponSystem>();
            if (ws != null)
            {
                ws.AddReserveAmmo(ammoAmount);
                
                if (pickupSound != null)
                {
                    AudioSource.PlayClipAtPoint(pickupSound, transform.position);
                }
                
                Destroy(gameObject);
            }
        }
    }
}