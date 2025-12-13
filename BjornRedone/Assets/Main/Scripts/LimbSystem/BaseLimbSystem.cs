using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public abstract class BaseLimbController : MonoBehaviour
{
    [Header("Base Limb Stats")]
    public float maxHealth = 50f;
    protected float currentHealth;
    
    // Define the slots here so both scripts share them
    [Header("Base Slots")]
    public Transform visualsHolder;
    public Transform headSlot;
    // ... other slots

    protected virtual void Start() {
        currentHealth = maxHealth;
    }

    // Shared logic
    public virtual void TakeDamage(float amount) {
        currentHealth -= amount;
        // ... shared flash/sound logic
        if (currentHealth <= 0) Die();
    }

    protected abstract void Die();
    protected abstract void UpdateStats();
}