using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ConsumableData", menuName = "ScriptableObjects/ConsumableData")]
public class ConsumableData : ItemData
{
    public Consumable consumable;
    
    public override Item GetItem()
    {
        return consumable;
    }
}
