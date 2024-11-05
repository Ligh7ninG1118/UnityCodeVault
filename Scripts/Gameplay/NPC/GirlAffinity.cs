using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityEnums;
using Random = UnityEngine.Random;

public class GirlAffinity : MonoBehaviour
{
    [Serializable]
    public struct AffinityLevelSetting
    {
        [Tooltip("Dont have to change. Will assign to the correct level on start.")]
        public GirlAffinityLevel affinityLevel;
        [Tooltip("Relationship string displayed in status")]
        public string displayString;
        [Tooltip("In percentage")]
        public float taskResistChance;

        public UpgradeData unlockUpgrade;
    }
    [Tooltip("Starting affinity value for the girl")]
    [SerializeField][Range(0, 100)] private int startingAffinity = 20;
    [Tooltip("Various settings including task resist chance per affinity level")]
    [SerializeField] private AffinityLevelSetting[] affinityLevelSettings;

    [Tooltip("Affinity decrease rate when hungry. Per minute.")]
    [SerializeField][Range(0, 10)] private int affinityDecreaseRateWhenHungry = 1;
    [Tooltip("How much time to wait when first entering hunger low state to start decreasing affinity")]
    [SerializeField][Range(0.0f, 60.0f)] private float affinityDecreaseWaitTimeWhenHungry = 15.0f;
    [Tooltip("Affinity decrease rate when the girl is hurt by certain value")]
    [SerializeField] [Range(0, 10)] private int affinityDecreaseRateWhenHurt = 1;
    [Tooltip("Percentage of the girl's max health to trigger affinity decrease")]
    [SerializeField] [Range(0.0f, 1.0f)] private float affinityDecreaseTriggerPercentageWhenHurt = 0.05f;

    [SerializeField] private int affinityIncByPatting = 1;
    [SerializeField] private int affinityIncByTalking = 1;
    [SerializeField] private float pattingIncCooldownTime = 300.0f;
    [SerializeField] private float talkingIncCooldownTime = 300.0f;

    [Header("VFX")]
    [SerializeField] private NPCVFXControlHelper NPCVFXControl;

    private int _currentAffinity;
    private int _currentAffinityLowerLimit;
    private int _currentAffinityUpperLimit;
    
    private GirlAffinityLevel _currentAffinityLevel;
    private GirlAffinityLevel _maximumReachedLevel;
    private GirlAffinityLevel _maximumUnlockedLevel; 

    private bool _hasPatForCurrentLevel = false;

    private float _healthLostTriggerValue = 0.0f;
    private float _accumulateHealthLost = 0.0f;
    private Coroutine _affinityDebuffWhenHungry = null;

    private float pattingCooldownTimer = -1.0f;
    private float talkingCooldownTimer = -1.0f;

    private bool hasSetPatSprite = false;
    private bool hasSetTalkSprite = false;
    
    private CharacterStatus _status;

    private void Awake()
    {
        // Init var based on startingAffinity
        _currentAffinity = startingAffinity;
        _currentAffinityLevel = (GirlAffinityLevel)Mathf.FloorToInt(startingAffinity / 20.0f);
        _maximumReachedLevel = _currentAffinityLevel;
        _maximumUnlockedLevel = _currentAffinityLevel;

        _currentAffinityLowerLimit = (int)_currentAffinityLevel * 20;
        _currentAffinityUpperLimit = Mathf.Clamp(_currentAffinityLowerLimit + 19, 0, 100);

        
        if(affinityLevelSettings.Length != 6)
            Debug.LogError("Incorrect number of element set in Affinity Level Settings");
        for (int i = 0; i < affinityLevelSettings.Length; i++)
        {
            affinityLevelSettings[i].affinityLevel = (GirlAffinityLevel)i;
        }
        
        _hasPatForCurrentLevel = false;
    }

    private void Start()
    {
        _status = GetComponent<CharacterStatus>();
        _status.healthRef.OnValueDecreased += HealthDecreaseEventHandler;
        _status.hungerRef.OnEnteringLowState += HungerEnteringLowStateEventHandler;
        _status.hungerRef.OnExitingLowState += HungerExitingLowStateEventHandler;

        _healthLostTriggerValue = _status.healthRef.GetMaxValue() * affinityDecreaseTriggerPercentageWhenHurt;
    }

    private void OnDestroy()
    {
        _status.healthRef.OnValueDecreased -= HealthDecreaseEventHandler;
        _status.hungerRef.OnEnteringLowState -= HungerEnteringLowStateEventHandler;
        _status.hungerRef.OnExitingLowState -= HungerExitingLowStateEventHandler;
    }

    private void Update()
    {
        /*if (pattingCooldownTimer > 0.0f)
            pattingCooldownTimer -= Time.deltaTime;
        else
        {
            if (!hasSetPatSprite)
            {
                UIInteractionRing.Instance.SwapPatSprites(false);
                hasSetPatSprite = true;
            }
        }*/

        if (talkingCooldownTimer > 0.0f)
            talkingCooldownTimer -= Time.deltaTime;
        else
        {
            if (!hasSetTalkSprite)
            {
                UIInteractionRing.Instance.SwapTalkSprites(false);
                hasSetTalkSprite = true;
            }
        }
    }

    public void FeedFood(ItemData itemData)
    {
        var item = itemData.GetItem();
        var consumable = item as Consumable;
        if(consumable == null)
        {
            Debug.LogError("Illegal function call");
            return;
        }
        
        if(consumable.isProcessed)
        {
            DebugManager.LogDebugString("GirlAffinity: Processed food fed. But did you know it's healthier eating unprocessed foods?");
            ModifyAffinity(1);
        }
        else
        {
            DebugManager.LogDebugString("GirlAffinity: IT'S FUCKING RAW (In Ramsay's voice)");
            ModifyAffinity(-1);
        }
        
    }

    /*public bool PatHead()
    {
        if(pattingCooldownTimer > 0.0f)
        {
            DebugManager.LogDebugString("GirlAffinity: Pat failed. On cooldown.");
            return false;
        }
        else
        {
            DebugManager.LogDebugString("GirlAffinity: Pat succeed.");
            ModifyAffinity(affinityIncByPatting);
            pattingCooldownTimer = pattingIncCooldownTime;
            hasSetPatSprite = false;
            UIInteractionRing.Instance.SwapPatSprites(true);
            return true;
        }
    }*/

    public bool Dialogue(int val)
    {
        DebugManager.LogDebugString("GirlAffinity: Talk");
        if (talkingCooldownTimer > 0.0f)
        {
            return false;
        }
        else
        {
            ModifyAffinity(affinityIncByTalking);
            talkingCooldownTimer = talkingIncCooldownTime;
            hasSetTalkSprite = false;
            UIInteractionRing.Instance.SwapTalkSprites(true);
            return true;
        }
    }

    public bool GetIfTaskResisted()
    {
        float chance = affinityLevelSettings[(int)_currentAffinityLevel].taskResistChance;
        float roll = Random.Range(0.0f, 1.0f);
        if (roll < chance)
        {
            DebugManager.LogDebugString("GirlAffinity: Task resisted!");
            return true;
        }
        DebugManager.LogDebugString("GirlAffinity: Task accepted!");
        return false;
    }

    public void ModifyAffinity(int val)
    {
        int modifiedVal = _currentAffinity + val;
        DebugManager.LogDebugString("GirlAffinity: Affinity changes from " + _currentAffinity + " -> " + modifiedVal);
        // not clamping to lower/upper limit since there might be level changes
        _currentAffinity = Mathf.Clamp(modifiedVal, 0, 100);
        RecalculateAffinityLevel();

        //vfx
        if (val > 0)
        {
            // sfx
            AudioManager.Instance.PlaySFXOneShot3DAtPosition("GirlAffinityLevelUp", transform.position, 0.6f);
            NPCVFXControl.Play_NPCVFX(NPCVFXControlHelper.NPCVFXType.FriendShipUp);
        }
    }

    private void RecalculateAffinityLevel()
    {
        // downgrade level
        if (_currentAffinity < _currentAffinityLowerLimit)
        {
            _currentAffinityLevel--;
            _currentAffinityLowerLimit = (int)_currentAffinityLevel * 20;
            _currentAffinityUpperLimit = Mathf.Clamp(_currentAffinityLowerLimit + 19, 0, 100);
            DebugManager.LogDebugString("GirlAffinity: Affinity level decreased to " + _currentAffinityLevel);
        }
        // potential upgrade or clamp val
        else if(_currentAffinity > _currentAffinityUpperLimit)
        {
            // Next level is unlocked, increase self affinity level
            if (_currentAffinityLevel + 1 <= _maximumUnlockedLevel)
            {
                _currentAffinityLevel++;
                _maximumReachedLevel = _currentAffinityLevel;
                _currentAffinityLowerLimit = (int)_currentAffinityLevel * 20;
                _currentAffinityUpperLimit = Mathf.Clamp(_currentAffinityLowerLimit + 19, 0, 100);
                DebugManager.LogDebugString("GirlAffinity: Affinity level increased to " + _currentAffinityLevel);

                if(affinityLevelSettings[(int)_currentAffinityLevel].unlockUpgrade)
                    affinityLevelSettings[(int)_currentAffinityLevel].unlockUpgrade.upgrade.unlockValue--;
            }
            // not unlock, clamp affinity val to upper limit
            else
            {
                _currentAffinity = Mathf.Clamp(_currentAffinity, 0, _currentAffinityUpperLimit);
                DebugManager.LogDebugString("GirlAffinity: Affinity level failed to increase, next level requirement not met");
            }
        }
    }

    public void UnlockAffinityLevel(GirlAffinityLevel levelUnlocked)
    {
        if(levelUnlocked < _maximumUnlockedLevel)
            Debug.LogError("Illegal function call");

        _maximumUnlockedLevel = levelUnlocked;
    }
    
    private void HungerEnteringLowStateEventHandler()
    {
        if(_affinityDebuffWhenHungry == null)
            _affinityDebuffWhenHungry = StartCoroutine(ReduceAffinityWhenHungry());
    }

    private void HungerExitingLowStateEventHandler()
    {
        StopCoroutine(_affinityDebuffWhenHungry);
        _affinityDebuffWhenHungry = null;
    }

    private IEnumerator ReduceAffinityWhenHungry()
    {
        yield return new WaitForSeconds(affinityDecreaseWaitTimeWhenHungry);
        while (true)
        {
            ModifyAffinity(-affinityDecreaseRateWhenHungry);
            yield return new WaitForSeconds(60.0f);
        }
    }

    private void HealthDecreaseEventHandler(float val)
    {
        _accumulateHealthLost += Mathf.Abs(val);
        if (_accumulateHealthLost >= _healthLostTriggerValue)
        {
            ModifyAffinity(-affinityDecreaseRateWhenHurt);
            _accumulateHealthLost = 0.0f;
        }
    }

    public int GetCurrentAffinity()
    {
        return _currentAffinity;
    }
    
    public GirlAffinityLevel GetCurrentAffinityLevel()
    {
        return _currentAffinityLevel;
    }

    public int GetCurrentAffinityLevelAsInt()
    {
        return (int)_currentAffinityLevel;
    }

    #region Editor Debug Functions

    [ContextMenu("Unlock Next Affinity Level")]
    private void UnlockNextAffinityLevel()
    {
        UnlockAffinityLevel(_currentAffinityLevel + 1);
    }
    
    [ContextMenu("Increase Affinity by 5")]
    private void IncreaseAffinityBy5()
    {
        for (int i = 0; i < 5; i++)
        {
            ModifyAffinity(1);
        }
    }
    
    [ContextMenu("Increase Affinity by 1")]
    private void IncreaseAffinityBy1()
    {
        ModifyAffinity(1);
    }
    
    [ContextMenu("Decrease Affinity by 5")]
    private void DecreaseAffinityBy5()
    {
        for (int i = 0; i < 5; i++)
        {
            ModifyAffinity(-1);
        }
    }
    
    [ContextMenu("Decrease Affinity by 1")]
    private void DecreaseAffinityBy1()
    {
        ModifyAffinity(-1);
    }

    #endregion
}
