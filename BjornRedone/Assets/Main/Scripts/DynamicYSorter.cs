using UnityEngine;
using UnityEngine.Rendering; // We need this for SortingGroup

/// <summary>
/// This script automatically updates the SortingGroup order based on Y-position.
/// Attach this to any object that needs to be sorted dynamically (Player, pickups, etc.)
/// Requires a SortingGroup component on the same GameObject.
/// </summary>
[RequireComponent(typeof(SortingGroup))]
public class DynamicYSorter : MonoBehaviour
{
    private SortingGroup sortingGroup;

    void Awake()
    {
        sortingGroup = GetComponent<SortingGroup>();
    }

    /// <summary>
    /// LateUpdate is best for visual-only updates like sorting.
    /// It runs after all game logic (Update) has finished.
    /// </summary>
    void LateUpdate()
    {
        // We multiply by -100 to get a precise integer.
        // A lower Y-pos (e.g., -5) becomes a higher sort order (500),
        // which means it renders IN FRONT.
        sortingGroup.sortingOrder = (int)(transform.position.y * -100);
    }
}