using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityEnums;

[Serializable]
public class Consumable : Item
{
    public ConsumableEffect effect;
    public ConsumableType type;
    public float consumableValue;

    public bool isProcessed = false;

    public bool canCook = true;
    
    public bool canBeUsedByPlayer = false;
    
    public bool Use(CharacterStatus status, bool isTesting = false)
    {
        //check if value is already max, then dont
        //need to have the modify value function return bool, also this one too

        Status affectedStatus = null;
        
        switch (effect)
        {
            case ConsumableEffect.RecoverHealth:
                affectedStatus = status.healthRef;
                break;
            case ConsumableEffect.RecoverHunger:
                affectedStatus = status.hungerRef;
                break;
            case ConsumableEffect.RecoverSanity:
                affectedStatus = status.sanityRef;
                break;
            case ConsumableEffect.RecoverFuel:
                affectedStatus = status.fuelRef;
                break;
        }

        if (affectedStatus != null)
        {
            if (Mathf.Abs(affectedStatus.GetValue() - affectedStatus.GetMaxValue()) > Mathf.Epsilon)
            {
                if(!isTesting)
                {
                    if (affectedStatus == status.sanityRef && consumableValue > 0f)
                    {
                        // mark sanity ore tutorial as completed
                        GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1SanityTutorial] = true;
                    }
                    
                    affectedStatus.ModifyValue(-consumableValue);

                    // eating raw food, decrease sanity & bark
                    if (status.sanityRef != null && !isProcessed && effect != ConsumableEffect.RecoverSanity)
                    {
                        status.sanityRef.ModifyValue(5.0f);
                        GirlAI.Instance._barkSpawner.SpawnBark(20);
                        
                        // eat raw food sfx
                        AudioManager.Instance.PlaySFXOnUIOneShot("A1RawFood", true);
                        
                        // trigger dialogue
                        if (GameStateManager.Instance && !GameStateManager.Instance.isPlayerInGroundCombat &&
                            !GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1Pot])
                        {
                            GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1Pot] = true;
                            DialogueQuestManager.Instance.PlayYarnDialogue("A1Pot");
                            
                            TutorialManager.Instance.ShowOpenUpgradeMenuTutorial();
                        }
                    }
                }
                return true;
            }
            return false;
        }
        return false;
    }
}
