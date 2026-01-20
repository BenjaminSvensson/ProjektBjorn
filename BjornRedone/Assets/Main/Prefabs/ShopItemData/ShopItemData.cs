using UnityEngine;

[CreateAssetMenu(fileName = "New Shop Item", menuName = "Shop/Item Data")]
public class ShopItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    public int price;
    public GameObject itemPrefab; // The object that spawns when you leave
}