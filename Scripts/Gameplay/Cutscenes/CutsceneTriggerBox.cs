using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityEnums;

public class CutsceneTriggerBox : MonoBehaviour, ISavable
{
    //[SerializeField] private ResourceType resourceToCheck;
    [SerializeField] private CutsceneID cutsceneToTrigger;
    [SerializeField] private bool shouldCheckForRune;
    [SerializeField] private bool isClimbingLevel;

    private bool _hasTriggered = false;

    private void Awake()
    {
        ((ISavable)this).Subscribe();
    }

    private void OnDestroy()
    {
        ((ISavable)this).Unsubscribe();
    }

    private void OnTriggerEnter(Collider other)
    {
        if(_hasTriggered)
            return;
        
        if (other.CompareTag("Player"))
        {
            // For trigger in climbing level, check if player is falling
            if (!isClimbingLevel || (ClimbController.Instance != null && !ClimbController.Instance._isFalling))
            {
                // Check if has family rune
                if (!shouldCheckForRune || InventoryManager.Instance.hasAcquiredRune)
                {
                    CutsceneManager.Instance.KickstartCutscene(cutsceneToTrigger);
                    _hasTriggered = true;
                }
            }
        }
    }

    #region ISavable Implementation

    public List<Tuple<string, dynamic>> SaveElement()
    {
        List<Tuple<string, dynamic>> elements = new List<Tuple<string, dynamic>>();

        elements.Add(new Tuple<string, dynamic>("b_" + gameObject.name, _hasTriggered));
        
        return elements;
    }

    public void LoadElement(SaveData saveData)
    {
        _hasTriggered = (bool)saveData.saveDict["b_" + gameObject.name];
    }

    #endregion
}
