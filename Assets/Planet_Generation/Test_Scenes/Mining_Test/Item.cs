using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(menuName ="ScriptableObject/Item")]
public class Item : ScriptableObject
{
    // Start is called before the first frame update
    [SerializeField] private Color color = Color.red;

    [SerializeField] private Sprite sprite;

    [Header("Object Name")]
    [SerializeField] private string _name = string.Empty;
    [SerializeField] private UInt16 ID = 0; //0 signifies empty

    public UInt16 getID() {
        return ID;
    }

    public Sprite getSprite() {
        return sprite;
    }

    public Color getColor() { return color; }

    public string getName() { return _name; }
}
