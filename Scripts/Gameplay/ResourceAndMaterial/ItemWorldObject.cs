using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.PlayerLoop;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;
using UtilityEnums;
using UtilityFunc;

public class ItemWorldObject : MonoBehaviour, IRaycastable
{
    [Tooltip("Type of this resource. Mainly used by NPC for searching resources")]
    public ResourceType type;
    
    [Tooltip("What is the action verb used to interact with this object?")]
    public ActionVerbType actionVerb;
    
    [Tooltip("Resource HP for collecting")]
    public float collectHitPointLarge = 3.0f;
    public float collectHitPointSmall = 3.0f;


    [Header("Offset - Large")]
    [SerializeField] private float collectXOffsetForIB = 2.0f;
    [SerializeField] private float collectZOffsetForIB = 2.0f;

    [SerializeField] private float collectXOffsetForA1 = 1.0f;
    [SerializeField] private float collectZOffsetForA1 = 1.0f;
    
    [Header("Offset - Small")]
    [SerializeField] private float collectXOffsetForIBSmall = 2.0f;
    [SerializeField] private float collectZOffsetForIBSmall = 2.0f;

    [SerializeField] private float collectXOffsetForA1Small = 1.0f;
    [SerializeField] private float collectZOffsetForA1Small = 1.0f;


    [Space(15)]
    [Tooltip("How long should the VFX play after each hit")]
    public float vfxPlayTime = 0.4f;

    public float burstForceMultiplier = 1.5f;

    [SerializeField] private GameObject shellPrefab;
    
    [HideInInspector] public float _realHitPoint;
    [HideInInspector] public float _currentMaxHitPoint;
    [HideInInspector] public bool hasBeenCollected = false;

    private AudioSource _audioSource;
    private DropTable _dropTable;
    public ResourceVFXControlHelper _vfxHelper;

    private Coroutine vfxCoroutine = null;

    private bool hasSpawnedShell = false;

    public WorldObjectAudioEnum audioType;
    public GrowWorldObjectAudioEnum growAudioType;
    
    [Header("Regeneration")]
    public WorldObjectRegenerationStage initialLifeStage;
    public bool shouldGrow;

    [SerializeField] private GameObject[] deadRegenStageModels;
    [SerializeField] private GameObject[] smallRegenStageModels;
    [SerializeField] private GameObject[] largeRegenStageModels;
    
    [SerializeField] private Collider smallRegenStageCollider;
    [SerializeField] private Collider largeRegenStageCollider;

    
    [Tooltip("the regeneration time for an object NOT set to grow. This value is useless when the object is set to grow")]
    [SerializeField] private float noGrowRegenTime;
    
    [SerializeField] private float deadToSmallGrowTime;
    [FormerlySerializedAs("smallToMediumGrowTime")] [SerializeField] private float smallToLargeGrowTime;
    
    public WorldObjectRegenerationStage _currentRegenerationStage;
    private Collider _objectCollider;
    private float _lifeTimeCounter;

    private Camera _cam;

    [Header("Conditional Generation")]
    public bool isGeneratedConditionally;
    public CliffElevatorName cliffElevatorCondition;
    private bool _isConditionMet = true;

    private void Awake()
    {
        // spawn own audio source
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;

        _currentRegenerationStage = initialLifeStage;
        _objectCollider = GetComponent<Collider>();
    }

    private void Start()
    {
        if (isGeneratedConditionally)
        {
            _isConditionMet = false;
        }
        
        // set audio mixer group
        _audioSource.outputAudioMixerGroup = AudioManager.Instance.GetSFXAudioSource().outputAudioMixerGroup;
        _audioSource.volume = AudioManager.Instance.GetSFXAudioSource().volume;

        _realHitPoint = _currentMaxHitPoint = collectHitPointLarge;

        _dropTable = GetComponent<DropTable>();
        _vfxHelper = GetComponent<ResourceVFXControlHelper>();

        _cam = GameStateManager.Instance.currentActivePlayerSet == PlayerSetType.Explore? GameObject.FindGameObjectWithTag("ExploreMainCamera").GetComponent<Camera>() : GameObject.FindGameObjectWithTag("ClimbingMainCamera").GetComponent<Camera>();
        
        if(SceneObjectManager.Instance != null)
            SceneObjectManager.Instance.AddToResourceList(this.gameObject);

        if (_isConditionMet)
        {
            // set correct regen stage
            SetCorrectObjectBasedOnRegenStage();
        
            // set correct initial value for life time counter
            SetCorrectInitialLifeTime();
        }
        else
        {
            _currentRegenerationStage = WorldObjectRegenerationStage.Dead;
            SetCorrectObjectBasedOnRegenStage();
            _lifeTimeCounter = 0.0f;
        }

        if (gameObject.name =="Rune" && InventoryManager.Instance.hasAcquiredRune)
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if(SceneObjectManager.Instance != null)
            SceneObjectManager.Instance.RemoveFromResourceList(this.gameObject);
    }

    private void OnEnable()
    {
        if (isGeneratedConditionally)
        {
            GameStateManager.OnCliffElevatorUnlocked += OnCliffElevatorUnlockHandler;
        }
    }

    private void OnDisable()
    {
        if (isGeneratedConditionally)
        {
            GameStateManager.OnCliffElevatorUnlocked -= OnCliffElevatorUnlockHandler;
        }
    }

    private void OnCliffElevatorUnlockHandler(CliffElevatorName elevatorName)
    {
        // if it is the correct elevator being unlocked
        if (elevatorName == cliffElevatorCondition)
        {
            _isConditionMet = true;
            
            // if this object is a growing object, set lifetime to initial life stage's lifetime
            if (shouldGrow)
            {
                switch (initialLifeStage)
                {
                    case WorldObjectRegenerationStage.Dead:
                        _lifeTimeCounter = 0.0f;
                        break;
                    
                    case WorldObjectRegenerationStage.Small:
                        _lifeTimeCounter = deadToSmallGrowTime + 0.1f;
                        break;
                    
                    case WorldObjectRegenerationStage.Large:
                        _lifeTimeCounter = deadToSmallGrowTime + smallToLargeGrowTime + 0.1f;
                        break;
                }
            }
            else
            {
                _lifeTimeCounter = noGrowRegenTime + 0.1f;
            }
        }
    }

    private void Update()
    {
        // debug test
        if (DebugManager.Instance != null && DebugManager.Instance.isDebugMode && Input.GetKeyDown(KeyCode.R))
        {
            CollectItem(ExploreController.Instance.transform);
        }
        
        // count the life time of this object ONLY after cliff tutorial is completed
        if (GameStateManager.Instance && GameStateManager.Instance.hasCliffTutorialLevelCompleted)
        {
            if (_currentRegenerationStage != WorldObjectRegenerationStage.Dead)
            {
                // life time increases normally when object is not dead
                _lifeTimeCounter += Time.deltaTime;
            }
            else
            {
                if (_isConditionMet)
                {
                    _lifeTimeCounter += Time.deltaTime;
                }
            }
        }
        
        // if this object grows
        if (shouldGrow)
        {
            // clamp the life time counter
            float maxLifeTime = deadToSmallGrowTime + smallToLargeGrowTime;
            if (_lifeTimeCounter > maxLifeTime)
            {
                _lifeTimeCounter = maxLifeTime;
            }
            
            // grow the object from dead to small
            if (_currentRegenerationStage == WorldObjectRegenerationStage.Dead)
            {
                if (_lifeTimeCounter >= deadToSmallGrowTime)
                {
                    // sfx
                    switch (growAudioType)
                    {
                        case GrowWorldObjectAudioEnum.Tree:
                            AudioManager.Instance.PlaySFXOneShot3DAtPosition("TreeSpawn", transform.position);
                            break;
                    }
                    
                    _currentRegenerationStage = WorldObjectRegenerationStage.Small;
                    SetCorrectObjectBasedOnRegenStage();
                }
            }
            // from small to medium
            else if (_currentRegenerationStage == WorldObjectRegenerationStage.Small)
            {
                if (_lifeTimeCounter >= deadToSmallGrowTime + smallToLargeGrowTime)
                {
                    // sfx
                    switch (growAudioType)
                    {
                        case GrowWorldObjectAudioEnum.Tree:
                            AudioManager.Instance.PlaySFXOneShot3DAtPosition("TreeGrow", transform.position);
                            break;
                    }
                    
                    _currentRegenerationStage = WorldObjectRegenerationStage.Large;
                    SetCorrectObjectBasedOnRegenStage();
                }
            }
        }
        else
        {
            if (_lifeTimeCounter > noGrowRegenTime)
            {
                _lifeTimeCounter = noGrowRegenTime;
            }

            if (_currentRegenerationStage == WorldObjectRegenerationStage.Dead)
            {
                if (_lifeTimeCounter >= noGrowRegenTime)
                {
                    // sfx
                    switch (growAudioType)
                    {
                        case GrowWorldObjectAudioEnum.FuelFlower:
                            AudioManager.Instance.PlaySFXOneShot3DAtPosition("FuelFlowerSpawn", transform.position);
                            break;
                        case GrowWorldObjectAudioEnum.Rock:
                            AudioManager.Instance.PlaySFXOneShot3DAtPosition("RockSpawn", transform.position);
                            break;
                        case GrowWorldObjectAudioEnum.Bush:
                            AudioManager.Instance.PlaySFXOneShot3DAtPosition("BushSpawn", transform.position);
                            break;
                    }
                    
                    _currentRegenerationStage = initialLifeStage;
                    SetCorrectObjectBasedOnRegenStage();
                }
            }
        }
    }

    /*private void OnMouseOver()
    {
        var mainCanvas = SceneObjectManager.Instance.mainCanvas;
        var raycastInteractor =SceneObjectManager.Instance.mainCanvas.gameObject.GetComponent<UIRaycastInteractor>();
        
        if(mainCanvas.GetComponent<UIMainCanvas>().shouldBlockUI)
            return;
        
        if (raycastInteractor.itemOnCursor.itemData == null && !UIInteractionRing.isMoveOrderActive)
        {
            Vector2 hoverPromptOffset = new Vector2(-20f, 20f);
            
            // show the hover prompt
            GameObject mouseHoverPrompt = SceneObjectManager.Instance.mainCanvas.mouseHoverPrompt;
            mouseHoverPrompt.SetActive(true);
            mouseHoverPrompt.GetComponent<TextMeshProUGUI>().text = actionVerb.ToString();
            
            SceneObjectManager.Instance.mainCanvas.gameObject.GetComponent<UIRaycastInteractor>().mouseOverObject =
                gameObject;
            
            // move the hover prompt to object position with some offset
            mouseHoverPrompt.transform.position = Mouse.current.position.ReadValue() + hoverPromptOffset;
        }
        
    }

    private void OnMouseExit()
    {
        GameObject mouseHoverPrompt = SceneObjectManager.Instance.mainCanvas.mouseHoverPrompt;
        if (mouseHoverPrompt.activeInHierarchy &&
            mouseHoverPrompt.GetComponent<TextMeshProUGUI>().text.CompareTo(actionVerb.ToString()) == 0)
        {
            SceneObjectManager.Instance.mainCanvas.gameObject.GetComponent<UIRaycastInteractor>().mouseOverObject =
                null;
            
            // hide the hover prompt
            mouseHoverPrompt.SetActive(false);
        }
    }*/

    private IEnumerator DoVFXForPeriod(float time, Transform interactor)
    {
        _vfxHelper.PlayVFX_Cont(interactor);
        yield return new WaitForSeconds(time);
        _vfxHelper.StopVFX_Cont(interactor);
        vfxCoroutine = null;
    }

    // dirty af
    // only used for climbing, so we assume only one item will generate
    public ItemData GetItemData()
    {
        List<ItemObject> drops = new List<ItemObject>();
        
        // generate drops that match the current life stage of this object if this object grows
        if (shouldGrow)
        {
            drops = _dropTable.GenerateLifeStageDrops(_currentRegenerationStage);
        }
        // just generate all drops attached to this object if the object does not grow
        else
        {
            drops = _dropTable.GenerateDrops();
        }

        if (drops.Count > 1)
        {
            Debug.LogError("illegal function call");
        }

        return drops[0].itemData;
    }

    public void ReduceCollectHP(float val, Transform interactor)
    {
        bool isInteractorIB = interactor.GetComponent<ExploreController>() || interactor.GetComponent<ClimbController>();
        // SFX
        switch (type)
        {
            case ResourceType.Bush:
                AudioManager.Instance.PlaySFXOneShot3D(isInteractorIB? "IBForageBush":"A1ForageBush", _audioSource);
                break;
            case ResourceType.Mineral:
                AudioManager.Instance.PlaySFXOneShot3D(isInteractorIB? "IBMine":"A1Mine", _audioSource);
                break;
            case ResourceType.Tree:
                AudioManager.Instance.PlaySFXOneShot3D(isInteractorIB? "IBTreeChop":"A1TreeChop", _audioSource);
                break;
        }
        
        _realHitPoint -= val;

        _realHitPoint = Mathf.Clamp(_realHitPoint, 0.0f, _currentMaxHitPoint);
        
        if (_vfxHelper != null && vfxCoroutine == null)
            vfxCoroutine = StartCoroutine(DoVFXForPeriod(vfxPlayTime, interactor));
        
        if (_realHitPoint <= 0.001f)
        {
            CollectItem(interactor);
        }
    }

    private void CollectItem(Transform interactor)
    {
        if(hasBeenCollected)
            return;
            
        hasBeenCollected = true;
        
        List<ItemObject> drops = new List<ItemObject>();
        
        // generate drops that match the current life stage of this object if this object grows
        if (shouldGrow)
        {
            drops = _dropTable.GenerateLifeStageDrops(_currentRegenerationStage);
        }
        // just generate all drops attached to this object if the object does not grow
        else
        {
            drops = _dropTable.GenerateDrops();
        }
        
        
        foreach (var drop in drops)
        {
            drop.type = type;
        }
        
        if (!interactor.CompareTag("Player") || interactor.GetComponentInParent<ClimbController>())
        {
            // if has the upgrade, duplicate all drops
            if (interactor.CompareTag("NPC") && interactor.GetComponent<GirlAI>().doubleGatheringDrop)
            {
                drops.AddRange(drops);
            }
            
            foreach (var drop in drops)
            {
                var go = Instantiate(drop.itemPrefab, transform.position, Quaternion.identity);
                go.GetComponent<ItemObject>().Collect(false);
                
                // A1 collect sfx
                switch (type)
                {
                    case ResourceType.Bush:
                        AudioManager.Instance.PlaySFXOneShot2D("CollectLight");
                        break;
                    case ResourceType.Mineral:
                        AudioManager.Instance.PlaySFXOneShot2D("CollectHeavy");
                        break;
                    case ResourceType.Tree:
                        AudioManager.Instance.PlaySFXOneShot2D("CollectMedium");
                        break;
                }
            }
        }
        else
        {
            foreach (var drop in drops)
            {
                float angle = Random.Range(0.0f, 360.0f);
                Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0.0f, Mathf.Sin(angle * Mathf.Deg2Rad));
                var go = Instantiate(drop.itemPrefab, transform.position + offset, Quaternion.identity);
                Vector3 force = offset;
                force.y = 2.0f;
                go.GetComponent<Rigidbody>().AddForce(force*burstForceMultiplier, ForceMode.Impulse);
            }
            
            // resource collection sfx
            switch (type)
            {
                case ResourceType.Bush:
                    if (audioType == WorldObjectAudioEnum.SmallBush)
                    {
                        AudioManager.Instance.PlaySFXOneShot3DAtPosition("SmallBushSearched", transform.position, 0.6f);
                    }
                    else if (audioType == WorldObjectAudioEnum.LargeBush)
                    {
                        AudioManager.Instance.PlaySFXOneShot3DAtPosition("LargeBushSearched", transform.position, 0.6f);
                    }
                    
                    break;
                case ResourceType.Mineral:
                    AudioManager.Instance.PlaySFXOneShot3DAtPosition("RockShattered", transform.position, 0.6f);
                    break;
                case ResourceType.Tree:
                    _audioSource.Stop();
                    AudioManager.Instance.PlaySFXOneShot3DAtPosition("TreeChoppedDown", transform.position, 0.6f);
                    break;
            }
        }
        
        if (shellPrefab != null && !hasSpawnedShell)
        {
            Instantiate(shellPrefab, transform.position, transform.rotation);
            hasSpawnedShell = true;
        }
        
        if (_vfxHelper != null)
        {
            // dont destroy this world resource
            _vfxHelper.PlayVFX_Final_WithoutDestroy(interactor);
        }
        
        _currentRegenerationStage = WorldObjectRegenerationStage.Dead;
        _lifeTimeCounter = 0.0f;
        SetCorrectObjectBasedOnRegenStage();
    }

    private void SetCorrectInitialLifeTime()
    {
        if (shouldGrow)
        {
            if (initialLifeStage == WorldObjectRegenerationStage.Dead)
            {
                _lifeTimeCounter = 0.0f;
            }
            else if (initialLifeStage == WorldObjectRegenerationStage.Small)
            {
                _lifeTimeCounter = deadToSmallGrowTime;
            }
            else if (initialLifeStage == WorldObjectRegenerationStage.Large)
            {
                _lifeTimeCounter = deadToSmallGrowTime + smallToLargeGrowTime;
            }
        }
        else
        {
            _lifeTimeCounter = noGrowRegenTime;
        }
    }

    private void SetCorrectObjectBasedOnRegenStage()
    {
        // handles objects that don't grow
        if (!shouldGrow)
        {
            // if I'm currently dead, deactivate initial model
            if (_currentRegenerationStage == WorldObjectRegenerationStage.Dead)
            {
                foreach (var go in deadRegenStageModels)
                {
                    go.SetActive(true);
                }
                
                // disable all models and collider
                foreach (var go in smallRegenStageModels)
                {
                    go.SetActive(false);
                }
                if(smallRegenStageCollider != null)
                    smallRegenStageCollider.enabled = false;

                foreach (var go in largeRegenStageModels)
                {
                    go.SetActive(false);
                }
                if(largeRegenStageCollider != null)
                    largeRegenStageCollider.enabled = false;
                
                _objectCollider.enabled = false;
            }
            else
            {
                foreach (var go in deadRegenStageModels)
                {
                    go.SetActive(false);
                }
                
                // enable all models
                foreach (var go in smallRegenStageModels)
                {
                    go.SetActive(true);
                    _realHitPoint = _currentMaxHitPoint= collectHitPointSmall;
                }
                if(smallRegenStageCollider != null)
                    smallRegenStageCollider.enabled = true;
                
                foreach (var go in largeRegenStageModels)
                {
                    go.SetActive(true);
                    _realHitPoint =_currentMaxHitPoint= collectHitPointLarge;
                }
                if(largeRegenStageCollider != null)
                    largeRegenStageCollider.enabled = true;
                
                hasBeenCollected = false;
                _objectCollider.enabled = true;
            }

            return;
        }
        
        switch (_currentRegenerationStage)
        {
            // hide all models when the tree is dead
            case WorldObjectRegenerationStage.Dead:
                foreach (var go in deadRegenStageModels)
                {
                    go.SetActive(true);
                }
                
                foreach (var go in smallRegenStageModels)
                {
                    go.SetActive(false);
                }
                if(smallRegenStageCollider != null)
                    smallRegenStageCollider.enabled = false;

                foreach (var go in largeRegenStageModels)
                {
                    go.SetActive(false);
                }
                if(largeRegenStageCollider != null)
                    largeRegenStageCollider.enabled = false;
                
                // disable the collider
                //_objectCollider.enabled = false;
                
                break;
            case WorldObjectRegenerationStage.Small:
                foreach (var go in deadRegenStageModels)
                {
                    go.SetActive(false);
                }
                
                foreach (var go in smallRegenStageModels)
                {
                    go.SetActive(true);
                }
                if(smallRegenStageCollider != null)
                    smallRegenStageCollider.enabled = true;

                foreach (var go in largeRegenStageModels)
                {
                    go.SetActive(false);
                }
                if(largeRegenStageCollider != null)
                    largeRegenStageCollider.enabled = false;
                
                // reenable the collider
                //_objectCollider.enabled = true;
                
                hasBeenCollected = false;
                _realHitPoint =_currentMaxHitPoint= collectHitPointSmall;
                _vfxHelper?.SwapAnimtor(0);

                break;
            case WorldObjectRegenerationStage.Large:
                foreach (var go in deadRegenStageModels)
                {
                    go.SetActive(false);
                }
                
                foreach (var go in smallRegenStageModels)
                {
                    go.SetActive(false);
                }
                if(smallRegenStageCollider != null)
                    smallRegenStageCollider.enabled = false;

                foreach (var go in largeRegenStageModels)
                {
                    go.SetActive(true);
                }
                if(largeRegenStageCollider != null)
                    largeRegenStageCollider.enabled = true;
                
                // reenable the collider
                //_objectCollider.enabled = true;
                
                hasBeenCollected = false;
                _realHitPoint = _currentMaxHitPoint=collectHitPointLarge;
                _vfxHelper?.SwapAnimtor(2);

                break;
        }
    }

    public Vector2 GetCollectOffset(bool isIB)
    {
        Vector2 res = Vector2.zero;
        if (isIB)
        {
            switch (_currentRegenerationStage)
            {
                case WorldObjectRegenerationStage.Small:
                    res.x = collectXOffsetForIBSmall;
                    res.y = collectZOffsetForIBSmall;
                    break;
                case WorldObjectRegenerationStage.Large:
                    res.x = collectXOffsetForIB;
                    res.y = collectZOffsetForIB;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        else
        {
            switch (_currentRegenerationStage)
            {
                case WorldObjectRegenerationStage.Small:
                    res.x = collectXOffsetForA1Small;
                    res.y = collectZOffsetForA1Small;
                    break;
                case WorldObjectRegenerationStage.Large:
                    res.x = collectXOffsetForA1;
                    res.y = collectZOffsetForA1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        return res;
    }

    #region IRaycastable Implementation

    public string hoverPrompt
    {
        get => LocalizationUtility.GetLocalizedString(actionVerb.ToString());
        set => hoverPrompt = value;
    }
    public bool canClickWithPrompt => true;
    public bool canClickWithoutPrompt => false;

    public Vector2 hoverPromptOffset => new Vector2(-20f, 20f);

    public void OnClickAction(UIMainCanvas mainCanvas, UIRaycastInteractor raycastInteractor, int button)
    {
        // Logic handled in Explore Controller 
    }

    public string GetHoverPrompt()
    {
        return LocalizationUtility.GetLocalizedString(actionVerb.ToString());
    }

    public bool ShouldShowPrompt(UIMainCanvas mainCanvas, UIRaycastInteractor raycastInteractor)
    {
        if (!mainCanvas.shouldBlockUI && raycastInteractor.itemOnCursor.itemData == null && !UIInteractionRing.isMoveOrderActive)
            return true;
        return false;
    }

    #endregion
}
