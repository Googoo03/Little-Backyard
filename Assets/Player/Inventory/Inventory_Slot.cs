using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class Inventory_Slot : MonoBehaviour, IBeginDragHandler, IEndDragHandler,IDragHandler
{
    // Start is called before the first frame update
    [SerializeField] private Item item;
    [SerializeField] private int quantity = 0;
    [SerializeField] private Sprite sprite;

    [SerializeField] Animator animator;
    [SerializeField] Inventory_Slot hold_slot;
    [SerializeField] private Transform drag_transform;

    //NEEDS ANIMATION TO SHOW THAT AN ITEM WAS ADDED

    public void setItem(Item newItem) {
        item = newItem;
        sprite = null;
        if(newItem.getSprite()) sprite = newItem.getSprite();
        transform.GetComponent<UnityEngine.UI.Image>().sprite = sprite;
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
        //hold_slot = GameObject.FindWithTag("Hold_Slot").GetComponent<Inventory_Slot>();
    }

    //DRAGGING ITEMS--------------------------------------------
    public void OnBeginDrag(PointerEventData eventData)
    {
        transform.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        drag_transform = transform.parent;
        transform.SetParent(transform.root);
        //throw new System.NotImplementedException();

    }

    public void OnDrag(PointerEventData eventData) { 
        transform.position = Input.mousePosition;
        //transform.parent = transform.parent.parent;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        //Find the closest box that it's on
        transform.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
        transform.SetParent(drag_transform,false);
        transform.GetComponent<RectTransform>().anchoredPosition = Vector3.zero;
        //throw new System.NotImplementedException();

    }
    //----------------------------------------------------------

    public void setDrag_Parent(Transform drag) {
        drag_transform = drag;
    }

}
