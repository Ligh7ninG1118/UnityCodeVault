using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UtilityEnums;

public class HouseShelter : MonoBehaviour
{
    public static HouseShelter Instance;

    [Serializable]
    public struct HouseAssets
    {
        public GameObject assetNormal;
        public GameObject assetDamaged;
        public GameObject assetDestroyed;
    }
    
    [Header("Model Assets")]
    [Tooltip("Ordered by Level")]
    [SerializeField] private List<HouseAssets> houseAssets; 

    [Header("Misc")]
    [Tooltip("Rate per second")]
    [SerializeField] private float shelterHPRecoveryRate = 1.0f;
    public Transform entranceTransform;
    [SerializeField] private GameObject girlSprite;
    [SerializeField] private float invincibleDuration = 10.0f;
    
    [HideInInspector] public bool isInDamagedState = false;
    [HideInInspector] public bool isInDestroyedState = false;
    [HideInInspector] public bool hasGirlSheltered = false;

    public bool canTriggerInvincible = false;
    
    private CharacterStatus _status;
    private Combatant _combatant;
    private GameObject _currentModel;

    private int _houseLevel = 0;
    private bool _hasTriggeredInvincibleThisWave = false;
    private float _invincibleTimer = -1.0f;
    
    public static event Action<bool> OnGirlIsInShelter;

    //vfx
    [Header("ShelterVFX")]
    public ShelterVFX ShelterVFXControl;


    [HideInInspector] public bool hasInit = false;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("Two HouseShelter in the scene");
        }
        Instance = this;
    }

    private void Start()
    {
        _status = GetComponent<CharacterStatus>();
        
        _status.healthRef.OnEnteringLowState += OnHealthEnteringLowStateEventHandler;
        _status.healthRef.OnExitingLowState += OnHealthExitingLowStateEventHandler;
        _status.healthRef.OnDepleting += OnHealthDepletingEventHandler;
        _status.healthRef.OnRecovering += OnHealthRecoveringEventHandler;
        
        _combatant = GetComponent<Combatant>();
        _combatant.OnBeingHit += BeingHitEventHandler;

        foreach (var houseAsset in houseAssets)
        {
            houseAsset.assetDestroyed.SetActive(false);
            houseAsset.assetDamaged.SetActive(false);
            houseAsset.assetNormal.SetActive(false);
        }
        
        _currentModel = houseAssets[_houseLevel].assetNormal;
        _currentModel.SetActive(true);
        
        girlSprite.SetActive(false);

        hasInit = true;
    }

    private void OnDestroy()
    {
        _status.healthRef.OnEnteringLowState -= OnHealthEnteringLowStateEventHandler;
        _status.healthRef.OnExitingLowState -= OnHealthExitingLowStateEventHandler;
        _status.healthRef.OnDepleting -= OnHealthDepletingEventHandler;
        _status.healthRef.OnRecovering -= OnHealthRecoveringEventHandler;
        
        _combatant.OnBeingHit -= BeingHitEventHandler;
    }

    void Update()
    {
        if (SceneObjectManager.Instance != null && SceneObjectManager.Instance.IsSpawnedMobEmpty())
        {
            ShelterAutoRepair();
            if (hasGirlSheltered)
            {
                UnshelterGirl();
            }

            _hasTriggeredInvincibleThisWave = false;
        }

        if (_invincibleTimer > 0.0f)
            _invincibleTimer -= Time.deltaTime;
    }

    public void UpgradeHouseLevel()
    {
        StartCoroutine((Util.ConditionalCallbackTimer(
            () => hasInit,
            () =>
            {
                _houseLevel++;
        
                if (isInDestroyedState)
                {
                    _currentModel.SetActive(false);
                    _currentModel = houseAssets[_houseLevel].assetDestroyed;
                    _currentModel.SetActive(true);
                }
                else if (isInDamagedState)
                {
                    _currentModel.SetActive(false);
                    _currentModel = houseAssets[_houseLevel].assetDamaged;
                    _currentModel.SetActive(true);
                }
                else
                {
                    _currentModel.SetActive(false);
                    _currentModel = houseAssets[_houseLevel].assetNormal;
                    _currentModel.SetActive(true);
                }

                ShelterVFXControl.EnableShelterVFX(_houseLevel);
            }
        )));
    }
    
    public void ShelterGirl()
    {
        hasGirlSheltered = true;
        girlSprite.SetActive(true);
        OnGirlIsInShelter?.Invoke(true);
    }

    public void UnshelterGirl()
    {
        hasGirlSheltered = false;
        girlSprite.SetActive(false);
        OnGirlIsInShelter?.Invoke(false);
        
        GirlAI.Instance.transform.position = entranceTransform.position;
        GirlAI.Instance.TryGiveTask(GirlTask.Idle);
    }

    private void OnHealthEnteringLowStateEventHandler()
    {
        isInDamagedState = true;
        //Swap Model Asset
        _currentModel.SetActive(false);
        _currentModel = houseAssets[_houseLevel].assetDamaged;
        _currentModel.SetActive(true);

        switch (_houseLevel)
        {
            case 0:
                AudioManager.Instance.PlaySFXOneShot3DAtPosition("HouseBrokenTent", transform.position);
                break;
            case 1:
                AudioManager.Instance.PlaySFXOneShot3DAtPosition("HouseBrokenWood", transform.position);
                break;
            default:
                AudioManager.Instance.PlaySFXOneShot3DAtPosition("HouseBrokenConcrete", transform.position);
                break;
        }
    }
    
    private void OnHealthExitingLowStateEventHandler()
    {
        isInDamagedState = false;
        //Swap Model Asset
        _currentModel.SetActive(false);
        _currentModel = houseAssets[_houseLevel].assetNormal;
        _currentModel.SetActive(true);
    }
    
    private void OnHealthDepletingEventHandler()
    {
        isInDestroyedState = true;
        if(hasGirlSheltered)
            UnshelterGirl();
        
        //Swap Model Asset
        _currentModel.SetActive(false);
        _currentModel = houseAssets[_houseLevel].assetDestroyed;
        _currentModel.SetActive(true);
        
        switch (_houseLevel)
        {
            case 0:
                AudioManager.Instance.PlaySFXOneShot3DAtPosition("HouseGoneTent", transform.position);
                break;
            case 1:
                AudioManager.Instance.PlaySFXOneShot3DAtPosition("HouseGoneWood", transform.position);
                break;
            default:
                AudioManager.Instance.PlaySFXOneShot3DAtPosition("HouseGoneConcrete", transform.position);
                break;
        }
    }
    
    private void OnHealthRecoveringEventHandler()
    {
        isInDestroyedState = false;
        
        //Swap Model Asset
        _currentModel.SetActive(false);
        _currentModel = houseAssets[_houseLevel].assetDamaged;
        _currentModel.SetActive(true);
    }
    

    private void BeingHitEventHandler(float damage, Vector3 attackerPos)
    {
        if(isInDestroyedState)
            return;
        
        
        // Override by Upgradable Invincible
        if (canTriggerInvincible)
        {
            if (!_hasTriggeredInvincibleThisWave)
            {
                _invincibleTimer = invincibleDuration;
                _hasTriggeredInvincibleThisWave = true;
            }
            
            if(_invincibleTimer > 0.0f)
                return;
        }
        
        
        if (!_combatant.isInvincible)
        {
            _combatant.isInvincible = true;
            _status.healthRef.ModifyValue(damage);
            Invoke(nameof(DisableInvincibility), _combatant.invincibleDuration);

            ShelterVFXControl.PlayCurrentShelterVFX();
            
            switch (_houseLevel)
        {
            case 0:
                AudioManager.Instance.PlaySFXOneShot3DAtPosition("HouseHitTent", transform.position);
                break;
            case 1:
                AudioManager.Instance.PlaySFXOneShot3DAtPosition("HouseHitWood", transform.position);
                break;
            default:
                AudioManager.Instance.PlaySFXOneShot3DAtPosition("HouseHitConcrete", transform.position);
                break;
        }
        }
    }
    
    private void DisableInvincibility()
    {
        _combatant.isInvincible = false;
    }

    private void ShelterAutoRepair()
    {
        if (_status.healthRef.GetMaxValue() - _status.healthRef.GetValue() > 0.001f)
        {
            _status.healthRef.ModifyValue(-shelterHPRecoveryRate * Time.deltaTime);
        }
    }
    
}
