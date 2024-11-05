using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(DropTable))]
public class DropSimple : Drop
{
    [Tooltip("Item to drop")]
    [SerializeField] private ItemObject item;
    
    [Tooltip("Minimum drop quantity for the item. Chance is uniform.")]
    [SerializeField][Range(0, 20)] private int minDropNum;
    
    [Tooltip("Maximum drop quantity for the item. Chance is uniform.")]
    [SerializeField][Range(0, 20)] private int maxDropNum;

    public override List<ItemObject> GenerateDrop()
    {
        List<ItemObject> generatedDrops = new List<ItemObject>();

        if (Random.Range(0.0f, 1.0f) <= unitDropChance)
        {
            int num = Random.Range(minDropNum, maxDropNum + 1);
            for(int i=0;i<num;i++)
                generatedDrops.Add(item);
        }

        return generatedDrops;
    }
}
