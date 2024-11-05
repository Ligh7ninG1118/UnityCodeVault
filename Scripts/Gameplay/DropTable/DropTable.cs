using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityEnums;


public class DropTable : MonoBehaviour
{
    private List<Drop> drops;


    public List<ItemObject> GenerateDrops()
    {
        List<ItemObject> generatedDrops = new List<ItemObject>();
        var drops = GetComponents<Drop>();
        foreach (var drop in drops)
            generatedDrops.AddRange(drop.GenerateDrop());
        
        return generatedDrops;
    }
    
    public List<ItemObject> GenerateLifeStageDrops(WorldObjectRegenerationStage stage)
    {
        List<ItemObject> generatedDrops = new List<ItemObject>();
        var drops = GetComponents<Drop>();
        foreach (var drop in drops)
        {
            if (drop.dropLifeStage == stage)
            {
                generatedDrops.AddRange(drop.GenerateDrop());
            }
        }
        
        return generatedDrops;
    }
}
