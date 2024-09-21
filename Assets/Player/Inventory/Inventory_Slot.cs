using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Inventory_Slot : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private Item item;
    [SerializeField] private int quantity = 0;
    [SerializeField] private Sprite sprite;



    public void setItem(Item newItem) {
        item = newItem;
        if(newItem.getSprite()) sprite = newItem.getSprite();
        transform.GetComponent<Image>().sprite = sprite;
        
    }

    public Item getItem() { return item; }

    public int getQuantity() { return quantity; }

    public void setQuantity(int quan) { 
        quantity = quan;
        transform.GetChild(0).GetComponent<Text>().text = quantity.ToString();
    }


    public void Awake()
    {
        
    }

}
