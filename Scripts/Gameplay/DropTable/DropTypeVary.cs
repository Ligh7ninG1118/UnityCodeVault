using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;


[RequireComponent(typeof(DropTable))]
public class DropTypeVary : Drop
{
    [Serializable]
    public struct DropType
    {
        [Tooltip("Dropped item")]
        public ItemObject item;
        
        [Tooltip("Chance for this type of item to drop")]
        [Range(0.0f, 1.0f)] public float dropChance;
    }

    [Tooltip("Dropped item with chance pairs")]
    [SerializeField] private DropType[] itemWithChance;
    
    public override List<ItemObject> GenerateDrop()
    {
        List<ItemObject> generatedDrops = new List<ItemObject>();
        
        if (Random.Range(0.0f, 1.0f) <= unitDropChance)
        {
            float numChance = Random.Range(0.0f, 1.0f);
            float cumulativeChance = 0.0f;
            foreach (var pair in itemWithChance)
            {
                cumulativeChance += pair.dropChance;
                if (numChance <= cumulativeChance)
                {
                    generatedDrops.Add(pair.item);
                    break;
                }
            }
        }

        return generatedDrops;
    }
}
