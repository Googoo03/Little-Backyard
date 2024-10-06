using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/Resource_Preset")]
public class Resource_Preset : ScriptableObject
{
    // Start is called before the first frame update
    [SerializeField] private Item item;
    [SerializeField] private int quantity;

    public Item GetItem() { return item; }
    public int GetQuantity() { return quantity; }
}
