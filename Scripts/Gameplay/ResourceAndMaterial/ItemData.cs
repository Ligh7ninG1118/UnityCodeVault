using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public abstract class ItemData : ScriptableObject, IItem
{
    public abstract Item GetItem();
    public GameObject itemPrefab;
}
