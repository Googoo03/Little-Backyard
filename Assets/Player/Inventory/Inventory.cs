using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Inven
{
    public class Inventory
    {
        //Uses the Item Class found in Item.cs in Little Backyard

        // Needs slots, each with a quantity and item
        
        private const int hotbar_slots = 8;
        private const int inven_slots = 16;
        private const int num_slots = hotbar_slots+inven_slots; //8 for hotbar, 16 for inventory

        private const int max_quantity = 64; //max items per slot
        private Inventory_Slot[] slots;

        //[SerializeField] private GameObject inventory;

        public Inventory()
        {
            slots = new Inventory_Slot[num_slots];
        }

        public void setInventory_Slot(Inventory_Slot slot, int index) {
            slots[index] = slot;
        }

        public int getHotbarNum_Slots() { return hotbar_slots; }

        public int getInvenNum_Slots() { return inven_slots; }

        public int getNum_Slots() { return num_slots;}

        public bool add_item(Tuple<Item, int> new_item)
        {
            //what happens when the inventory fills up completely while adding items?


            int quantity_to_add = new_item.Item2;
            for (int i = 0; i < num_slots; ++i)
            {
                if (new_item.Item1.getID() != slots[i].getItem().getID() && slots[i].getItem().getID() != 0) continue;

                if (quantity_to_add <= 0) return true; //if no more items to add, break out of loop

                //if the ID matches (or is empty) and theres items to add
                int items_added = Mathf.Min(max_quantity - slots[i].getQuantity(), quantity_to_add);

                slots[i].setQuantity(slots[i].getQuantity() + items_added);
                quantity_to_add -= items_added;

                //If slot is empty, add new item
                if (slots[i].getItem().getID() == 0) slots[i].setItem(new_item.Item1);

            }

            //should return the amount of items that are left as well
            //perhaps through the tuple since its a pointer?
            return false;

        }





    }
}
