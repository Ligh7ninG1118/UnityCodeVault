using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UtilityEnums;


[CreateAssetMenu(fileName = "UpgradeData", menuName = "ScriptableObjects/UpgradeData")]
public class UpgradeData : ScriptableObject
{
    public Upgrade upgrade;

    [Tooltip("Subsequent upgrades this upgrade unlocks, after crafted")]
    public UpgradeData[] subsequentUpgradeData;
    
    private void OnEnable()
    {
        #if UNITY_EDITOR
            EditorApplication.playModeStateChanged += ResetRuntimeValue;
        #endif
    }

    private void OnDisable()
    {
        #if UNITY_EDITOR
                EditorApplication.playModeStateChanged -= ResetRuntimeValue;
        #endif
    }
    
#if UNITY_EDITOR
    private void ResetRuntimeValue(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            ResetUnlockValue();
        }
    }
#endif

    public void ResetUnlockValue()
    {
        upgrade.unlockValue = upgrade.baseUnlockValue;
    }

    public bool TryCraftUpgrade()
    {
        if (upgrade.unlockValue != 0 && !DebugManager.Instance.isDebugMode)
        {
            Debug.Log("Upgrade has unfinished prerequisite(s) or is already crafted");
            return false;
        }
        else if(CheckMaterials() || DebugManager.Instance.isDebugMode)
        {
            Debug.Log("Upgrade crafted. Applying effect");
            ApplyUpgradeEffect();
            upgrade.unlockValue = -1;
            UnlockSubsequentUpgrades();
            return true;
        }
        else
        {
            Debug.Log("Not enough materials to craft upgrade");
            return false;
        }
    }

    [ContextMenu("Craft Upgrade")]
    public void DebugCraftUpgrade(bool shouldPlayVFX = true, bool shouldUnlockSubsequent = true)
    {
        ApplyUpgradeEffect(false, shouldPlayVFX, !shouldUnlockSubsequent);
        upgrade.unlockValue = -1;
        if(shouldUnlockSubsequent)
            UnlockSubsequentUpgrades();
    }

    public bool CheckMaterials(bool isChecking = false)
    {
        //Debug.Log("Checking materials");

        var inventoryContent = InventoryManager.Instance.inventoryContent;

        List<KeyValuePair<Item, int>> potentialMaterialList = new List<KeyValuePair<Item, int>>();

        int enoughMatNum = 0;
        
        //TODO: yeah yeah i know it's rly costly time wise, will fix later
        foreach (var require in upgrade.requireMaterials)
        {
            foreach(var itemPair in inventoryContent)
            {
                if(itemPair.Key is not Resource)
                    continue;
                
                var mat = itemPair.Key as Resource;
                if (require.material == mat.materialType)
                {
                    //Debug.Log("has required material: " + require.material);
                    if (require.quantity > itemPair.Value)
                    {
                        //Debug.Log("but not enough!");
                        break;
                    }
                    else
                    {
                        KeyValuePair<Item, int> newEntry = new KeyValuePair<Item, int>(mat, require.quantity);
                        potentialMaterialList.Add(newEntry);
                        enoughMatNum++;
                    }
                }
            }
        }
        
        if (enoughMatNum == upgrade.requireMaterials.Length)
        {
            if (!isChecking)
            {
                List<UISlotv1> uiSlots = SceneObjectManager.Instance.mainCanvas.gameObject
                    .GetComponent<UIRaycastInteractor>().uiSlots;
                
                foreach (var item in potentialMaterialList)
                {
                    InventoryManager.Instance.DiscardItem(item.Key, item.Value);
                    int quantityToDiscard = item.Value;
                    foreach (var slot in uiSlots)
                    {
                        if (slot.itemInSlot.itemData != null && slot.itemInSlot.itemData.GetItem().Equals(item.Key))
                        {
                            if (slot.itemInSlot.itemQuantity >= quantityToDiscard)
                            {
                                slot.itemInSlot.itemQuantity -= quantityToDiscard;
                                slot.RefreshItemDisplay();
                                break;
                            }
                            else
                            {
                                quantityToDiscard -= slot.itemInSlot.itemQuantity;
                                slot.itemInSlot.itemQuantity = 0;
                                slot.RefreshItemDisplay();
                            }
                        }
                    }
                }
            }
            return true;
        }
        else
        {
            return false;
        }

    }

    private void ApplyUpgradeEffect(bool shouldMarkCrafted = false, bool shouldPlayVFX = true, bool isLoading = false)
    {
        switch (upgrade.upgradeEffect)
        {
            // Put upgrade effect script here
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void UnlockSubsequentUpgrades()
    {
        foreach (var upgradeData in subsequentUpgradeData)
        {
            upgradeData.upgrade.unlockValue--;
        }
    }
    
}
