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
            // Girl
            case UpgradeEffect.HungerCap:
                GirlAI.Instance?.GetComponent<CharacterStatus>().hungerRef.IncreaseMaxValue(upgrade.effectValue);
                if(shouldPlayVFX)
                    NPCLevelUpVFX.Instance?.PlayNPCUpVFX();
                break;
            case UpgradeEffect.HealthCap:
                GirlAI.Instance?.GetComponent<CharacterStatus>().healthRef.IncreaseMaxValue(upgrade.effectValue);
                if(shouldPlayVFX)
                    NPCLevelUpVFX.Instance?.PlayNPCUpVFX();
                break;
            case UpgradeEffect.SanityCap:
                GirlAI.Instance?.GetComponent<CharacterStatus>().sanityRef.IncreaseMaxValue(upgrade.effectValue);
                if(shouldPlayVFX)
                    NPCLevelUpVFX.Instance?.PlayNPCUpVFX();
                break;
            case UpgradeEffect.GirlMovementSpeed:
                GirlAI.Instance?.movingSpeedAttribute.UpgradeValue(upgrade.effectValue);
                NPCUpgradeVFX.BootsUpgrade++;
                if(shouldPlayVFX)
                {
                    NPCLevelUpVFX.Instance?.PlayNPCUpVFX();
                }
                break;
            case UpgradeEffect.MiningDamage:
                GirlAI.Instance?.miningDamageAttribute.UpgradeValue(upgrade.effectValue);
                ResourceVFXControlHelper.MineUpgrade++;
                if(shouldPlayVFX)
                {
                    NPCLevelUpVFX.Instance?.PlayNPCUpVFX();
                }
                break;
            case UpgradeEffect.WoodCuttingDamage:
                GirlAI.Instance?.woodChoppingDamageAttribute.UpgradeValue(upgrade.effectValue);
                ResourceVFXControlHelper.ChopUpgrade++;
                if(shouldPlayVFX)
                {
                    NPCLevelUpVFX.Instance?.PlayNPCUpVFX();
                }
                break;
            case UpgradeEffect.ForagingDamage:
                GirlAI.Instance?.foragingDamageAttribute.UpgradeValue(upgrade.effectValue);
                ResourceVFXControlHelper.ForageUpgrade++;
                if(shouldPlayVFX)
                {
                    NPCLevelUpVFX.Instance?.PlayNPCUpVFX();
                }
                break;
            case UpgradeEffect.GirlAutoConsume:
                GirlAI.Instance.shouldAutoUseConsumable = true;
                if(shouldPlayVFX)
                    NPCLevelUpVFX.Instance?.PlayNPCUpVFX();
                break;
            case UpgradeEffect.HarvestDamage:
                GirlAI.Instance?.miningDamageAttribute.UpgradeValue(upgrade.effectValue);
                GirlAI.Instance?.woodChoppingDamageAttribute.UpgradeValue(upgrade.effectValue);
                GirlAI.Instance?.foragingDamageAttribute.UpgradeValue(upgrade.effectValue);
                ResourceVFXControlHelper.MineUpgrade++;
                ResourceVFXControlHelper.ChopUpgrade++;
                ResourceVFXControlHelper.ForageUpgrade++;
                if(shouldPlayVFX)
                {
                    NPCLevelUpVFX.Instance?.PlayNPCUpVFX();
                }
                break;
            case UpgradeEffect.HarvestExtraLoot:
                GirlAI.Instance.doubleGatheringDrop = true;
                if(shouldPlayVFX)
                {
                    NPCLevelUpVFX.Instance?.PlayNPCUpVFX();
                }
                break;
            // Spider
            case UpgradeEffect.EnergyCap:
                if(ExploreController.Instance !=null)
                    ExploreController.Instance.GetComponent<CharacterStatus>().fuelRef.IncreaseMaxValue(upgrade.effectValue);
                if(ClimbController.Instance != null)
                    ClimbController.Instance.GetComponent<CharacterStatus>().fuelRef.IncreaseMaxValue(upgrade.effectValue);
                if(shouldPlayVFX)
                {
                    PlayerLevelUpVFXControl.Instance?.PlayPlayerUpVFX();
                }
                break;
            case UpgradeEffect.LoadCap:                     // Deprecated
                InventoryManager.Instance.totalLoadCapacity += upgrade.effectValue;
                InventoryManager.Instance?.CheckLoadStateChange();
                break;
            case UpgradeEffect.SpiderClawSpeed:
                if(ExploreController.Instance !=null)
                {
                    ExploreController.Instance.clawSpeedMultiplier += upgrade.effectValue * 0.01f;
                    ExploreController.Instance.movingSpeedAttribute.UpgradeValue(upgrade.effectValue);
                }
                if(ClimbController.Instance != null)
                    ClimbController.Instance.legMovingSpeedAttribute.UpgradeValue(upgrade.effectValue);
                if(shouldPlayVFX)
                    PlayerLevelUpVFXControl.Instance?.PlayPlayerUpVFX();
                break;
            case UpgradeEffect.SpiderLegLength:
                if(ExploreController.Instance !=null)
                    ExploreController.Instance.legExtentAdd += upgrade.effectValue;
                if (ClimbController.Instance != null)
                    ClimbController.Instance.IncreaseLegExtent(upgrade.effectValue);
                if(shouldPlayVFX)
                    PlayerLevelUpVFXControl.Instance?.PlayPlayerUpVFX();
                break;
            case UpgradeEffect.OverclockCooldown:
                if(ExploreController.Instance !=null)
                    ExploreController.Instance.overclockCooldownDelta -= upgrade.effectValue;
                if (ClimbController.Instance != null)
                    ClimbController.Instance.overclockCooldownAttribute.currentVal -= upgrade.effectValue;
                if(shouldPlayVFX)
                    PlayerLevelUpVFXControl.Instance?.PlayPlayerUpVFX();
                break;
            /*case UpgradeEffect.MonitorThreshold:
                UIMonitorSystem.Instance.SetMonitorLevel(1);
                break;
            case UpgradeEffect.MonitorOnHurt:
                UIMonitorSystem.Instance.SetMonitorLevel(2);
                break;
            case UpgradeEffect.MonitorRealtime:
                UIMonitorSystem.Instance.SetMonitorLevel(3);
                break;*/
            case UpgradeEffect.TeleportUnlock:
                GameStateManager.Instance.hasUnlockedTeleport = true;
                SceneObjectManager.Instance.mainCanvas.ToggleTeleportHomeUI(true);
                if(shouldPlayVFX)
                    PlayerLevelUpVFXControl.Instance?.PlayPlayerUpVFX();
                break;
            case UpgradeEffect.TeleportCooldown:
                GameStateManager.Instance.teleportHomeCooldown.currentVal -= upgrade.effectValue;
                if(shouldPlayVFX)
                    PlayerLevelUpVFXControl.Instance?.PlayPlayerUpVFX();
                break;
            case UpgradeEffect.FuelConsumptionRate:
                ExploreController.Instance.fuelConsumptionRateMultiplier -= upgrade.effectValue * 0.01f;
                if(shouldPlayVFX)
                    PlayerLevelUpVFXControl.Instance?.PlayPlayerUpVFX();
                break;
            case UpgradeEffect.EmergencyFuel:
                ExploreController.Instance.UnlockEmergencyFuel();
                if(shouldPlayVFX)
                    PlayerLevelUpVFXControl.Instance?.PlayPlayerUpVFX();
                break;
            case UpgradeEffect.DashAbility:
                ExploreController.Instance.UnlockDash();
                if(shouldPlayVFX)
                    PlayerLevelUpVFXControl.Instance?.PlayPlayerUpVFX();
                break;
            // Spider Weapon System
            case UpgradeEffect.WeaponFireRate:
                ExploreController.Instance.GetComponent<PlayerShooter>().shootIntervalAttribute.UpgradeValue(-upgrade.effectValue);
                PlayerWeaponVFX.WeaponFireRate++;
                if(shouldPlayVFX)
                {
                    PlayerLevelUpVFXControl.Instance?.PlayWeaponUpVFX();
                }
                break;
            case UpgradeEffect.WeaponRange:
                ExploreController.Instance.GetComponent<PlayerShooter>().projectileRangeAttribute.UpgradeValue(upgrade.effectValue);
                PlayerWeaponVFX.WeaponRange++;
                if(shouldPlayVFX)
                {
                    PlayerLevelUpVFXControl.Instance?.PlayWeaponUpVFX();
                }
                break;
            case UpgradeEffect.WeaponPower:
                ExploreController.Instance.GetComponent<PlayerShooter>().projectileDamageAttribute.UpgradeValue(upgrade.effectValue);
                PlayerWeaponVFX.WeaponPower++;
                if(shouldPlayVFX)
                {
                    PlayerLevelUpVFXControl.Instance?.PlayWeaponUpVFX();
                }
                break;
            
            // Base
            case UpgradeEffect.BaseHouse:
                ShelterSpawner.Instance.SpawnShelter();
                break;
            case UpgradeEffect.BaseShield:
                HouseShelter.Instance.GetComponent<CharacterStatus>().healthRef.IncreaseMaxValue(upgrade.effectValue);
                HouseShelter.Instance.UpgradeHouseLevel();
                ShelterVFX.ShelterLevel++;
                if(shouldPlayVFX)
                {
                    ShelterLevelUpVFX.Instance?.PlayShelterUpVFX();
                }
                break;
            case UpgradeEffect.BasePot:
                CookingPotSpawner.Instance.SpawnCookingPot(isLoading);
                CookingPotVFX.CookingPotLevel++;
                // mark the tutorials as completed
                GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1Pot] = true;
                GameStateManager.Instance.hasTaughtPot = true;
                
                break;
            case UpgradeEffect.BaseProcessSpeed:
                float perc = 100.0f / (1.0f + upgrade.effectValue * 0.01f);
                CookingPot.Instance.cookingTimeAttribute.UpgradeValue(-perc);

                CookingPotVFX.CookingPotLevel++;
                if(shouldPlayVFX)
                {
                    CookingPotLevelUpVFX.Instance?.PlayPotUpVFX();
                }
                break;
            case UpgradeEffect.BaseInvincible:
                HouseShelter.Instance.canTriggerInvincible = true;
                HouseShelter.Instance.UpgradeHouseLevel();
                ShelterVFX.ShelterLevel++;
                if(shouldPlayVFX)
                {
                    ShelterLevelUpVFX.Instance?.PlayShelterUpVFX();
                }
                break;
            case UpgradeEffect.PotExtraFood:
                CookingPot.Instance.canTriggerDuplicate = true;
                CookingPotVFX.CookingPotLevel++;
                if(shouldPlayVFX)
                    CookingPotLevelUpVFX.Instance?.PlayPotUpVFX();
                break;
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
