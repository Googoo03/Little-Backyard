using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Inventory_Slot_Parent : MonoBehaviour, IDropHandler
{
    // Start is called before the first frame update
    [SerializeField] private Item _emptyItem;
    public void OnDrop(PointerEventData event_data)
    {
        Inventory_Slot inventory_item = event_data.pointerDrag.GetComponent<Inventory_Slot>();
        Inventory_Slot current_item = transform.GetChild(0).gameObject.GetComponent<Inventory_Slot>();

        //These are the Item structs for reference later
        Item c_item = current_item.getItem();
        Item i_item = inventory_item.getItem();

        int quantity = inventory_item.getQuantity();

        if (c_item == _emptyItem || c_item == i_item)
        {
            current_item.setItem(inventory_item.getItem());

            //the number of items added is either quantity or something less than quantity if cap is reached
            int items_to_add = 64-current_item.getQuantity();
            int quantity_leftover = items_to_add > 0 ? Mathf.Max(quantity - items_to_add,0) : quantity;

            //If items < 64 -> quantity = 0

            //If items == 64, then quantity == 64

            //Set new quantity
            current_item.setQuantity( quantity_leftover == 0 ? current_item.getQuantity()+quantity: current_item.getQuantity() + quantity-quantity_leftover);

            if(quantity_leftover <= 0) inventory_item.setItem(_emptyItem);
            inventory_item.setQuantity(quantity_leftover);
        }
    }

}
