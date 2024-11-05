using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Item
{
    public string itemName;
    public Sprite itemSprite;
    //public GameObject itemPrefab;
    public int itemWeight;
    public string itemDescription;
    public bool canBeRemoved = true;

    public override bool Equals(object obj)
    {
        Item other = obj as Item;
        return other != null &&
               itemName == other.itemName &&
               //itemSprite == other.itemSprite &&
               itemWeight == other.itemWeight &&
               itemDescription == other.itemDescription;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(itemName, itemWeight, itemDescription);
    }
}
