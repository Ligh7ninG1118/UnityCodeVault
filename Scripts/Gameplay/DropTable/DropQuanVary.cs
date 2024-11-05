using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;



[RequireComponent(typeof(DropTable))]
public class DropQuanVary : Drop
{
    [Serializable]
    public struct DropQuan
    {
        [Tooltip("Drop quantity")]
        [Range(0, 20)] public int dropNum;
        
        [Tooltip("Chance for this much of item to drop")]
        [Range(0.0f, 1.0f)] public float dropChance;
    }
    [Tooltip("Item to drop")]
    [SerializeField] private ItemObject item;
    
    [Tooltip("Drop num with chance pairs")]
    [SerializeField] private DropQuan[] dropNumWithChance;

    public override List<ItemObject> GenerateDrop()
    {
        List<ItemObject> generatedDrops = new List<ItemObject>();
        
        if (Random.Range(0.0f, 1.0f) <= unitDropChance)
        {
            float numChance = Random.Range(0.0f, 1.0f);
            int num = 0;
            float cumulativeChance = 0.0f;
            foreach (var pair in dropNumWithChance)
            {
                cumulativeChance += pair.dropChance;
                if (numChance <= cumulativeChance)
                {
                    num = pair.dropNum;
                    break;
                }
            }
            
            for(int i=0;i<num;i++)
                generatedDrops.Add(item);
        }

        return generatedDrops;
    }
}
