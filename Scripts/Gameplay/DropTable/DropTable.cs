using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityEnums;


public class DropTable : MonoBehaviour
{
    private List<Drop> _drops;

    private void Awake() 
    {
        _drops = GetComponents<Drop>();
    }

    public List<ItemObject> GenerateDrops()
    {
        List<ItemObject> generatedDrops = new List<ItemObject>();
        foreach (var drop in _drops)
            generatedDrops.AddRange(drop.GenerateDrop());
        
        return generatedDrops;
    }
}
