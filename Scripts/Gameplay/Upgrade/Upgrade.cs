using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityEnums;

[Serializable]
public class Upgrade
{
    [Serializable]
    public struct Require
    {
        public MaterialType material;
        [Range(1, 30)] public int quantity;
    }

    // If this value is 0, this upgrade is unlocked and available for crafting
    // Or, if it's 3 for example, then this upgrade still has 3 prerequisites
    // -1 means upgrade is already crafted
    [Tooltip("Number of prerequisite this upgrade has. 0 means unlocked.")]
    [Range(-1, 5)] public int baseUnlockValue = 0;
    
    [Tooltip("List of materials to craft this upgrade, and its quantity.")]
    public Require[] requireMaterials;
    
    public UpgradeEffect upgradeEffect;

    public float effectValue;

    public string upgradeName;

    public string upgradeDescription;

    public int upgradeLevel = 1;

    [HideInInspector] public int unlockValue;
}
