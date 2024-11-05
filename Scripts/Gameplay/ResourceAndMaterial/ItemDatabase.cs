using System.Collections;
using System.Collections.Generic;
using UnityEngine;



[CreateAssetMenu(fileName = "ItemDatabase", menuName = "ScriptableObjects/ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemData> itemDatabase;

    public int GetItemIndexByString(string itemName)
    {
        if(itemName =="")
        {
            Debug.LogError("Empty string");
            return -1;
        }
        
        for (int i = 0; i < itemDatabase.Count; i++)
        {
            if (itemDatabase[i].GetItem().itemName == itemName)
            {
                return i;
            }
        }
        
        Debug.LogError("No item exists with the name");
        return -1;
    }

    public ItemData GetItemDataByIndex(int index)
    {
        if(index < 0 || index > itemDatabase.Count)
            Debug.LogError("Index out of range");
        
        return itemDatabase[index];
    }

    public ItemData GetItemDataByString(string itemName)
    {
        return GetItemDataByIndex(GetItemIndexByString(itemName));
    }
}
