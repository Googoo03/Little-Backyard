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

    [SerializeField] Animator animator;

    //NEEDS ANIMATION TO SHOW THAT AN ITEM WAS ADDED

    public void setItem(Item newItem) {
        item = newItem;
        if(newItem.getSprite()) sprite = newItem.getSprite();
        transform.GetComponent<Image>().sprite = sprite;
        animator.SetTrigger("Pop");
        
    }

    public Item getItem() { return item; }

    public int getQuantity() { return quantity; }

    public void setQuantity(int quan) { 
        quantity = quan;
        transform.GetChild(0).GetComponent<Text>().text = quantity.ToString();
        animator.SetTrigger("Pop");
    }


    public void Awake()
    {
        animator = GetComponent<Animator>(); //Get the animator before the scene starts
    }

}
