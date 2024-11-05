using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UtilityEnums;
using UtilityFunc;
using UnityEngine.VFX;
using Random = UnityEngine.Random;

public class GirlAI : MonoBehaviour, ISavable, IRaycastable
{
    public static GirlAI Instance;
    
    [Header("Movement")]
    [SerializeField] private float baseMovingSpeed;
    [Tooltip("Backward movement speed when this NPC is hit")]
    [SerializeField] [Range(0.1f, 1.0f)] private float pushSpeedFromBeingHit = 1.0f;

    [Header("AI Behavior")] 
    [Tooltip("How long to wait in seconds before the girl heads back to spawn.")]
    [SerializeField] [Range(0.0f, 60.0f)] private float waitTimeBeforeIdleAtSpawn = 30.0f;
    [SerializeField] private Transform spawnPoint;
    [Space(10)]
    [SerializeField] [Range(0.0f, 40.0f)] private float detectingRangeForEnemy = 35.0f;
    [SerializeField] private Vector2 followOffset = new Vector2(-1.0f, -0.5f);
    
    [Header("Resource Gathering")]
    [Tooltip("Range for NPC to search for resources")]
    [SerializeField][Range(0.1f, 50.0f)] private float resourceScanRange = 30.0f;
    [Tooltip("Cooldown time in seconds while collecting ")]
    [SerializeField] private float collectInterval = 0.5f;
    [Tooltip("Collect radius")]
    [SerializeField] private float collectRadius = 1.0f;
    
    
    [SerializeField][Range(0.01f, 3.0f)] private float baseMiningDamage = 0.2f;
    [SerializeField][Range(0.01f, 3.0f)] private float baseWoodChoppingDamage = 0.2f;
    [SerializeField][Range(0.01f, 3.0f)] private float baseForagingDamage = 0.2f;
    
    
    [Header("VFX")]
    [SerializeField] private NPCVFXControlHelper NPCVFXControl;
    [Tooltip("Blood VFX when NPC is in low health state")]
    [SerializeField] private GameObject NPCLowHealthBloodVFX;
    [SerializeField] private VisualEffect NPCBoostPlayerFuelVFX;
    [SerializeField] private NPCHurtPPEffect NPCHurt;

    private CharacterStatus _status;
    private NavMeshAgent _agent;
    private AnimationControllerGirl _animator;
    //private GirlAffinity _girlAffinity;
    private Combatant _combatant;
    [HideInInspector] public IBBarkSpawner _barkSpawner;

    [Header("DEBUG DONT CHANGE")]
    public GirlTask currentTask;
    public GirlTask _prevTask;
    public Vector3 _destination;
    //public ResourceType resourceType;
    public GameObject resourceTarget;
    public Vector3 movingVec;
    public bool doubleGatheringDrop = false;
    
    [HideInInspector] public UpgradableAttribute movingSpeedAttribute;
    [HideInInspector] public UpgradableAttribute miningDamageAttribute;
    [HideInInspector] public UpgradableAttribute woodChoppingDamageAttribute;
    [HideInInspector] public UpgradableAttribute foragingDamageAttribute;
    
    private GameObject _playerRef;
    private Vector3 _resourceGatheringStartingPos;
    
    private float _collectTimer = 0.0f;
    private float idleWaitTimer = 0.0f;

    private Coroutine randomWalkingAtSpawn;
    
    private List<GameObject> resourceList;

    private bool _isNPCStiff;

    //public bool isCommanding = false;

    private bool isCtrlPressed = false;
    private bool isBeingInteracted = false;
    private bool isBeingAttacked = false;

    private bool canCheckHoldInput = false;
    private bool holdingLeftInput = false;
    private bool holdingRightInput = false;

    private short _usingConsumable = 0;

    private enum UseConsumableFlag : short
    {
        Health = 1,
        Hunger = 2,
        Sanity = 4,
    }
    
    [HideInInspector] public bool shouldAutoUseConsumable = false;

    private ItemWorldObject _collectingTarget = null;
    private float _collectingDamage = 0.0f;
    public bool hasPlayedCollectAnimation = false;

    private bool firstTimeBeingHitThisWave = false;
    private bool hasBarkedAboutAlertMid = false;
    private bool hasBarkedAboutAlertHigh = false;

    private Vector3 _savedDestination = Vector3.zero;

    public event Action<bool, Vector3> OnHoldingRMB;
    
    public static event Action OnGirlInteractedWithIB;


    private ItemData feedFood = null;

    private InputMaster _input;

    private bool _isDoingDyingCoroutine = false;
    
    private Camera _mainCam;

    [Header("Narrative")]
    public float initialFuelFlowerDialogueRange = 4.0f;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("Two GirlAI in the scene");
        }
        Instance = this;

        movingSpeedAttribute = new(baseMovingSpeed);
        miningDamageAttribute = new(baseMiningDamage);
        foragingDamageAttribute = new(baseForagingDamage);
        woodChoppingDamageAttribute = new(baseWoodChoppingDamage);

        movingSpeedAttribute.OnValueUpgraded += MovingSpeedAttributeValueUpgradedEventHandler;

        _input = new InputMaster();
        _input.Enable();
        _input.Gameplay.HoldLMB.performed += HoldLeftEventHandler;
        _input.Gameplay.HoldLMB.canceled += HoldLeftEventHandler;
        /*_input.Gameplay.HoldRMB.performed += HoldRightEventHandler;
        _input.Gameplay.HoldRMB.canceled += HoldRightEventHandler;*/
    }

    private void Start()
    {
        _status = GetComponent<CharacterStatus>();
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<AnimationControllerGirl>();
        //_girlAffinity = GetComponent<GirlAffinity>();
        _barkSpawner = GetComponent<IBBarkSpawner>();
        _combatant = GetComponent<Combatant>();
        _combatant.shouldDoAttack = false;
        _combatant.OnBeingHit += BeingHitEventHandler;
        _mainCam = GameObject.FindGameObjectWithTag("ExploreMainCamera").GetComponent<Camera>();

        
        _playerRef = ExploreController.Instance.gameObject;

        _agent.speed = baseMovingSpeed;
        resourceList = SceneObjectManager.Instance.resourceList;
        
        // event handler for health deplete
        _status.healthRef.OnDepleting += HealthDepleteEventHandler;
        _status.healthRef.OnEnteringLowState += HealthEnteringLowStateEvnetHandler;
        _status.healthRef.OnExitingLowState += HealthExitingLowStateEvnetHandler;
        _status.hungerRef.OnEnteringLowState += HungerEnteringLowStateEvnetHandler;
        _status.hungerRef.OnExitingLowState += HungerExitingLowStateEvnetHandler;
        _status.sanityRef.OnEnteringLowState += SanityEnteringLowStateEvnetHandler;
        _status.sanityRef.OnExitingLowState += SanityExitingLowStateEvnetHandler;

        _status.healthRef.OnRecovering += HealthRecoverEventHandler;

        _status.hungerRef.OnDepleting += HungerDepleteEventHandler;
        _status.hungerRef.OnRecovering += HungerRecoverEventHandler;

        _status.sanityRef.OnDepleting += SanityDepleteEventHandler;
        _status.sanityRef.OnRecovering += SanityRecoverEventHandler;

        _animator.OnInteractFinished += FinishedInteraction;
        _animator.OnCollectHit += CollectHitEventHandler;

        ExploreController.AllEnemiesCleared += AllEnemiesClearedEventHandler;
        Monster.OnMonsterDied += MonsterDiedEventHandler;

        _status.healthRef.OnValueIncreased += HealthIncreaseEventHandler;
        _status.sanityRef.OnValueIncreased += SanityIncreaseEventHandler;
        _status.hungerRef.OnValueIncreased += HungerIncreaseEventHandler;
        
        ((ISavable)this).Subscribe();

    }


    private void OnDisable()
    {
    }

    private void OnDestroy()
    {
        SceneObjectManager.Instance.RemoveFromNPCList(this.gameObject);
        
        _status.healthRef.OnDepleting -= HealthDepleteEventHandler;
        _status.hungerRef.OnDepleting -= HungerDepleteEventHandler;
        _status.hungerRef.OnRecovering -= HungerRecoverEventHandler;
        _status.sanityRef.OnDepleting -= SanityDepleteEventHandler;
        _status.sanityRef.OnRecovering -= SanityRecoverEventHandler;
        
        _status.healthRef.OnRecovering -= HealthRecoverEventHandler;
        
        ExploreController.AllEnemiesCleared -= AllEnemiesClearedEventHandler;
        
        _status.healthRef.OnValueIncreased -= HealthIncreaseEventHandler;
        _status.sanityRef.OnValueIncreased -= SanityIncreaseEventHandler;
        _status.hungerRef.OnValueIncreased -= HungerIncreaseEventHandler;
        
        ((ISavable)this).Unsubscribe();

    }

    /*private void OnMouseDown()
    {
        if (Input.GetMouseButton(0))
        {
            if (GameStateManager.Instance && !GameStateManager.Instance.isPlayerInGroundCombat &&
                GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.PlayerFirstTab])
            {
                SceneObjectManager.Instance.mainCanvas.DisableAllGroundTutorialPanels();
                
                // re-enable gameplay player control
                ExploreController.Instance.ToggleInputMasterGameplay(true);
            }
            
            var raycastInteractor = SceneObjectManager.Instance.mainCanvas.gameObject.GetComponent<UIRaycastInteractor>();
            if (raycastInteractor.itemOnCursor.itemData != null && raycastInteractor.itemOnCursor.itemData.GetItem() is Consumable )
            {
                var consumable = raycastInteractor.itemOnCursor.itemData.GetItem() as Consumable;
                if (!consumable.canBeUsedByPlayer)
                {
                    feedFood = raycastInteractor.itemOnCursor.itemData;
                    if (InventoryManager.Instance.TestIfCanUseItemOnCharacter(feedFood.GetItem(), _status))
                    {
                        // Can feed food now (task wise)
                        if (TryGiveTask(GirlTask.Feed))
                        {
                            // Girl can eat now (hunger status wise)
                            if (InventoryManager.Instance.UseItemOnCharacter(feedFood.GetItem(), _status))
                            {
                                raycastInteractor.UsedItemOnCursor();
                                //_girlAffinity.FeedFood(feedFood);

                                //vfx
                                StatsRecoveringVFXBasedOnConsumable(consumable);

                                if (raycastInteractor.itemOnCursor.itemData == null)
                                {
                                    GameObject mouseHoverPrompt = SceneObjectManager.Instance.mainCanvas.mouseHoverPrompt;
                                    mouseHoverPrompt.SetActive(false);
                                }
                            }
                            else
                            {
                                DebugManager.LogDebugString("GirlAI: Failed trying to feed girl, already full");
                                _barkSpawner.SpawnBark(16);
                            }
                        }
                        else
                        {
                            DebugManager.LogDebugString("GirlAI: Failed trying to feed girl, blocked by task (in theory should never happens)");
                        }
                    }
                    else
                    {
                        _barkSpawner.SpawnBark(16);
                        raycastInteractor.ReturnItemToPrevSlot();
                    }
                }
                else
                {
                    
                    feedFood = raycastInteractor.itemOnCursor.itemData;

                    // Process fuel flower
                    if(InventoryManager.Instance.TestIfCanUseItemOnCharacter(feedFood.GetItem(), ExploreController.Instance.GetComponent<CharacterStatus>()))
                    {
                        // can use, need process
                        if (TryGiveTask(GirlTask.ProcessFuel))
                        {
                            raycastInteractor.UsedItemOnCursor();
                            if (raycastInteractor.itemOnCursor.itemData == null)
                            {
                                GameObject mouseHoverPrompt = SceneObjectManager.Instance.mainCanvas.mouseHoverPrompt;
                                mouseHoverPrompt.SetActive(false);
                            }
                            
                            // if fuel flower is processed but dialogue pt1 has not triggered yet, mark fuel flower tutorials as completed
                            if (GameStateManager.Instance && !GameStateManager.Instance.A1SpecialDialoguesUnlockState[
                                    A1SpecialDialogues.InitialFuelFlowerProcessingPt1])
                            {
                                GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.InitialFuelFlowerProcessingPt1] = true;
                                GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.InitialFuelFlowerProcessingPt2] = true;
                            }
                            
                            // fuel flower processing dialogue pt2
                            if (GameStateManager.Instance && !GameStateManager.Instance.isPlayerInGroundCombat &&
                                GameStateManager.Instance.A1SpecialDialoguesUnlockState[
                                    A1SpecialDialogues.InitialFuelFlowerProcessingPt1] &&
                                !GameStateManager.Instance.A1SpecialDialoguesUnlockState[
                                    A1SpecialDialogues.InitialFuelFlowerProcessingPt2])
                            {
                                GameStateManager.Instance.A1SpecialDialoguesUnlockState[
                                    A1SpecialDialogues.InitialFuelFlowerProcessingPt2] = true;
                                DialogueQuestManager.Instance.PlayYarnDialogue("InitialFuelFlowerPt2");
                            }

                            //vfx
                            NPCBoostPlayerFuelVFX.SendEvent("OnBoost");
                        }
                        else
                        {
                            
                        }
                    }
                    else
                    {
                        //bark
                        _barkSpawner.SpawnBark(18);
                        raycastInteractor.ReturnItemToPrevSlot();
                    }
                }
            }
            else
            {
                UIInteractionRing.Instance.ShowRing();
            }
        }
        
    }*/

    private void OnMouseEnter()
    {
        //holdingRightInput = false;
    }

    /*private void OnMouseOver()
    {
        canCheckHoldInput = true;
        
        var raycastInteractor =SceneObjectManager.Instance.mainCanvas.gameObject.GetComponent<UIRaycastInteractor>();
        
        raycastInteractor.mouseOverObject = gameObject;
        
        // only show FEED prompt if item on cursor is consumable
        if (raycastInteractor.itemOnCursor.itemData != null && raycastInteractor.itemOnCursor.itemData.GetItem() is Consumable)
        {
            var consumable = raycastInteractor.itemOnCursor.itemData.GetItem() as Consumable;
            if (!consumable.canBeUsedByPlayer)
            {
                Vector2 hoverPromptOffset = new Vector2(-20f, 60f);
                
                // show the hover prompt
                GameObject mouseHoverPrompt = SceneObjectManager.Instance.mainCanvas.mouseHoverPrompt;
                mouseHoverPrompt.SetActive(true);
                mouseHoverPrompt.GetComponent<TextMeshProUGUI>().text = "FEED";
                
                // move the hover prompt to object position with some offset
                mouseHoverPrompt.transform.position = Mouse.current.position.ReadValue() + hoverPromptOffset;
            }
            else
            {
                Vector2 hoverPromptOffset = new Vector2(-20f, 60f);
                
                // show the hover prompt
                GameObject mouseHoverPrompt = SceneObjectManager.Instance.mainCanvas.mouseHoverPrompt;
                mouseHoverPrompt.SetActive(true);
                mouseHoverPrompt.GetComponent<TextMeshProUGUI>().text = "PROCESS";
                
                // move the hover prompt to object position with some offset
                mouseHoverPrompt.transform.position = Mouse.current.position.ReadValue() + hoverPromptOffset;
            }
        }
        else
        {
            GameObject mouseHoverPrompt = SceneObjectManager.Instance.mainCanvas.mouseHoverPrompt;
            mouseHoverPrompt.SetActive(false);
            
            if (holdingLeftInput)
            {
            
            }
            if (holdingRightInput)
            {
                if (OnHoldingRMB != null)
                    OnHoldingRMB(true, transform.position);
            
                UIInteractionRing.Instance.HideRing();
            }
            if (!holdingLeftInput)
            {
            
            }
            if (!holdingRightInput)
            {
                if (OnHoldingRMB != null)
                    OnHoldingRMB(false, transform.position);
            }
        }
        
        // Right Click
        if (Input.GetMouseButtonUp(1) && !UIInteractionRing.isMoveOrderActive)
        {
            UIInteractionRing.isMoveOrderActive = true;
            UIInteractionRing.Instance.SpawnMovePointer();
        }
    }

    private void OnMouseExit()
    {
        canCheckHoldInput = false;
        SceneObjectManager.Instance.mainCanvas.gameObject.GetComponent<UIRaycastInteractor>().mouseOverObject =
            null;
        
        GameObject mouseHoverPrompt = SceneObjectManager.Instance.mainCanvas.mouseHoverPrompt;
        mouseHoverPrompt.SetActive(false);
    }*/

    private void MovingSpeedAttributeValueUpgradedEventHandler()
    {
        _agent.speed = movingSpeedAttribute.currentVal;
    }
    
    private void BeingHitEventHandler(float damage, Vector3 attackerPos)
    {
        Debug.Log("girl being hit");
        if (!_combatant.isInvincible)
        {
            _combatant.isInvincible = true;
            _isNPCStiff = true;
            _agent.isStopped = true;
        
            _status.healthRef.ModifyValue(damage);
            _animator.SetAnimatorTrigger("hurt");
            
            isBeingInteracted = false;
            
            Vector3 pushDirection = transform.position - attackerPos;
            pushDirection.y = 0f;

            StartCoroutine(NPCPushedBack(pushDirection, _combatant.beingHitPushBackDuration));
            Invoke(nameof(DisableInvincibility), _combatant.invincibleDuration);

            //screen post processing vfx
            NPCHurt.PlayAlphaHurt();
        }

        if (!firstTimeBeingHitThisWave)
        {
            _barkSpawner.SpawnBark(4);
            firstTimeBeingHitThisWave = true;
        }
    }

    private void MonsterDiedEventHandler()
    {
        if (Random.Range(0.0f, 1.0f) < 0.3f)
        {
            _barkSpawner.SpawnBark(3);
        }
    }
    
    protected void DisableInvincibility()
    {
        _combatant.isInvincible = false;
    }
    
    protected IEnumerator NPCPushedBack(Vector3 pushDirection, float pushDuration)
    {
        float timer = 0f;
        while (timer < pushDuration)
        {
            timer += Time.deltaTime;
            
            // only keep pushing if the next position is on navmesh
            Vector3 futurePos = transform.position + pushDirection * (pushSpeedFromBeingHit * Time.deltaTime);
            if (NavMeshUtility.isPositionOnNavMesh(futurePos))
            {
                transform.position = futurePos;
            }

            yield return null;
        }

        _isNPCStiff = false;
        // re-enable nav mesh agent
        _agent.isStopped = false;
    }


    private void HealthDepleteEventHandler()
    {
        if(!_isDoingDyingCoroutine)
            StartCoroutine(ProcessGameOver());
    }

    private IEnumerator ProcessGameOver()
    {
        _isDoingDyingCoroutine = true;
        AudioManager.Instance.StopAllSFXLoop();
        AudioManager.Instance.StopMusic();
        
        // SFX
        AudioSource ad = GetComponentInChildren<AudioSource>();
        if (ad)
        {
            AudioManager.Instance.PlaySFXOneShot3DAtPosition("NPCDie", transform.position);
        }

        _animator.SetAnimatorTrigger("die");
        yield return new WaitForSeconds(GameStateManager.Instance.gameOverDelayTime);
        
        Time.timeScale = 0f;

        if (SceneObjectManager.Instance)
        {
            SceneObjectManager.Instance.mainCanvas.SetGameOverMenuActive(true, true);
        }
    }

    private void HealthEnteringLowStateEvnetHandler()
    {
        AudioManager.Instance.PlaySFXOneShot3DAtPosition("A1LowHealth", transform.position, 0.6f);
        ExploreController.TriggerGirlEnterLowHealthEvent();
        DialogueQuestManager.Instance.isGirlLowHealth = true;
        NPCVFXControl.Play_NPCVFX(NPCVFXControlHelper.NPCVFXType.LowHealth);
        _barkSpawner.SpawnBark(5);

        _usingConsumable |= (short)UseConsumableFlag.Health;
    }
    
    private void HealthExitingLowStateEvnetHandler()
    {
        DialogueQuestManager.Instance.isGirlLowHealth = false;
        NPCVFXControl.Stop_NPCVFX(NPCVFXControlHelper.NPCVFXType.LowHealth);

        _usingConsumable ^= (short)UseConsumableFlag.Health;
    }
    
    private void HungerEnteringLowStateEvnetHandler()
    {
        DialogueQuestManager.Instance.isGirlLowHunger = true;
        AudioManager.Instance.PlaySFXOneShot3DAtPosition("A1LowHunger", transform.position, 0.6f);
        NPCVFXControl.Play_NPCVFX(NPCVFXControlHelper.NPCVFXType.LowHunger);
        _barkSpawner.SpawnBark(12);
        
        _usingConsumable |= (short)UseConsumableFlag.Hunger;
    }
    
    private void HungerExitingLowStateEvnetHandler()
    {
        DialogueQuestManager.Instance.isGirlLowHunger = false;
        NPCVFXControl.Stop_NPCVFX(NPCVFXControlHelper.NPCVFXType.LowHunger);
        
        _usingConsumable ^= (short)UseConsumableFlag.Hunger;
    }
    
    private void SanityEnteringLowStateEvnetHandler()
    {
        DialogueQuestManager.Instance.isGirlLowSanity = true;
        AudioManager.Instance.PlaySFXOneShot3DAtPosition("A1LowSanity", transform.position, 0.6f);
        NPCVFXControl.Play_NPCVFX(NPCVFXControlHelper.NPCVFXType.LowSanity);
        _barkSpawner.SpawnBark(14);
        
        _usingConsumable |= (short)UseConsumableFlag.Sanity;
    }
    
    private void SanityExitingLowStateEvnetHandler()
    {
        DialogueQuestManager.Instance.isGirlLowSanity = false;
        NPCVFXControl.Stop_NPCVFX(NPCVFXControlHelper.NPCVFXType.LowSanity);
        
        _usingConsumable ^= (short)UseConsumableFlag.Sanity;
    }

    private void SanityDepleteEventHandler()
    {
        AudioManager.Instance.PlaySFXOneShot3DAtPosition("A1SanityDeplete", transform.position, 0.6f);
        DialogueQuestManager.Instance.isGirlLowSanityExtreme = true;
        _barkSpawner.SpawnBark(15);

    }

    private void SanityRecoverEventHandler()
    {
        AudioManager.Instance.PlaySFXOneShot3DAtPosition("A1SanityRecover", transform.position, 0.6f);
        DialogueQuestManager.Instance.isGirlLowSanityExtreme = false;
    }

    private void HungerDepleteEventHandler()
    {
        AudioManager.Instance.PlaySFXOneShot3DAtPosition("A1HungerDeplete", transform.position, 0.6f);
        DialogueQuestManager.Instance.isGirlLowHungerExtreme = true;
        _barkSpawner.SpawnBark(13);

    }

    private void HungerRecoverEventHandler()
    {
        AudioManager.Instance.PlaySFXOneShot3DAtPosition("A1HungerRecover", transform.position, 0.6f);
        DialogueQuestManager.Instance.isGirlLowHungerExtreme = false;
    }

    private void HealthRecoverEventHandler()
    {
        AudioManager.Instance.PlaySFXOneShot3DAtPosition("A1HealthRecover", transform.position, 0.6f);
    }

    private void SanityIncreaseEventHandler(float val)
    {
        AudioManager.Instance.PlaySFXOneShot3DAtPosition("A1SanityRecover", transform.position, 0.6f);
    }

    private void HungerIncreaseEventHandler(float val)
    {
        AudioManager.Instance.PlaySFXOneShot3DAtPosition("A1HungerRecover", transform.position, 0.6f);
    }

    private void HealthIncreaseEventHandler(float val)
    {
        AudioManager.Instance.PlaySFXOneShot3DAtPosition("A1HealthRecover", transform.position, 0.6f);
    }

    private void AllEnemiesClearedEventHandler()
    {
        /*GirlAffinityLevel affinityLevel = _girlAffinity.GetCurrentAffinityLevel();
        switch (affinityLevel)
        {
            case GirlAffinityLevel.Level1:
            case GirlAffinityLevel.Level2:
                break;
            case GirlAffinityLevel.Level3:
            case GirlAffinityLevel.Level4:
            case GirlAffinityLevel.Level5:
            case GirlAffinityLevel.Level6:
                _barkSpawner.SpawnBark(7);
                break;
        }*/
        _barkSpawner.SpawnBark(6);

        firstTimeBeingHitThisWave = false;
        hasBarkedAboutAlertMid = false;
        hasBarkedAboutAlertHigh = false;
    }

    private void StatsRecoveringVFXBasedOnConsumable(Consumable consumable)
    {
        switch (consumable.effect)
        {
            case ConsumableEffect.RecoverSanity:
                NPCVFXControl.Play_NPCVFX(NPCVFXControlHelper.NPCVFXType.SanityUp);
                break;
            case ConsumableEffect.RecoverHunger:
                NPCVFXControl.Play_NPCVFX(NPCVFXControlHelper.NPCVFXType.HungerUp);
                break;
            case ConsumableEffect.RecoverHealth:
                NPCVFXControl.Play_NPCVFX(NPCVFXControlHelper.NPCVFXType.HealthUp);
                break;
        }
    }

    // debug function for killing the girl (why would u do such thing?)
    [ContextMenu("Kill Girl")]
    public void KillGirl()
    {
        _status.healthRef.ModifyValue(_status.healthRef.GetMaxValue());
    }
    
    private void Update()
    {
        if (_isDoingDyingCoroutine)
        {
            _agent.isStopped = true;
            return;
        }
        
        // if not in shelter, adjust pos to always be on ground level (y = 0)
        if (currentTask != GirlTask.InShelter)
        {
            Vector3 adjustedPos = transform.position;
            adjustedPos.y = 0.0f;
            transform.position = adjustedPos;
        }
        // send girl away from scene when inside shelter
        else
        {
            transform.position = new Vector3(0.0f, 200.0f, 0.0f);
        }
        
        UIInteractionRing.Instance?.SetPos(transform.position);
        
        if (DialogueQuestManager.Instance && DialogueQuestManager.Instance.isInDialogue && !_isNPCStiff)
        {
            _status.shouldPauseDebuff = true;
            _agent.isStopped = true;
            movingVec = Vector3.zero;
            return;
        }
        
        // decide if the girl is inside camera sight
        if (_mainCam.isActiveAndEnabled)
        {
            Vector3 viewPos = _mainCam.WorldToViewportPoint(transform.position);
            bool isInView = viewPos.x is > 0f and < 1f && viewPos.y is > 0f and < 1f;
            DialogueQuestManager.Instance.isGirlInPlayerSight = isInView;
        }
        else
        {
            DialogueQuestManager.Instance.isGirlInPlayerSight = false;
        }
        
        isCtrlPressed = Input.GetKey(KeyCode.LeftControl);


        // dont reset debuff pause when in tutorial
        if (GameStateManager.Instance && !GameStateManager.Instance.isInCliffTutorial)
        {
            _status.shouldPauseDebuff = false;
        }
        
        movingVec = _agent.velocity;

        if (currentTask != GirlTask.MovingToPlace)
        {
            DetectEnemy();
        }
        
        ExecuteTask();
        
        if (shouldAutoUseConsumable && _usingConsumable > 0)
        {
            UISlotv1 slot = null;
            float highest = -1.0f;
            
            var raycastInteractor = SceneObjectManager.Instance.mainCanvas.gameObject.GetComponent<UIRaycastInteractor>();

            // one consumable per tick
            ConsumableEffect effectToLookFor = 0;
            if ((_usingConsumable & (short)UseConsumableFlag.Health) == (short)UseConsumableFlag.Health)
            {
                effectToLookFor = ConsumableEffect.RecoverHealth;
            }
            else if ((_usingConsumable & (short)UseConsumableFlag.Hunger) == (short)UseConsumableFlag.Hunger)
            {
                effectToLookFor = ConsumableEffect.RecoverHunger;
            }
            else if((_usingConsumable & (short)UseConsumableFlag.Sanity) == (short)UseConsumableFlag.Sanity)
            {
                effectToLookFor = ConsumableEffect.RecoverSanity;
            }
            
            foreach (var uiSlot in raycastInteractor.uiSlots)
            {
                if (uiSlot.itemInSlot.itemData != null && uiSlot.itemInSlot.itemData.GetItem() is Consumable)
                {
                    var tempConsumable = uiSlot.itemInSlot.itemData.GetItem() as Consumable;
                    
                    if (tempConsumable.effect == effectToLookFor)
                    {
                        if (tempConsumable.consumableValue > highest)
                        {
                            highest = tempConsumable.consumableValue;
                            slot = uiSlot;
                        }
                    }
                }
            }

            if (slot != null)
            {
                AudioManager.Instance.PlaySFXOnUIOneShot("A1AutoConsume");
                InventoryManager.Instance.UseItemOnCharacter(slot.itemInSlot.itemData.GetItem(), _status);
                slot.TakeItemByQuantity(1);
            }

        }

        if (DialogueQuestManager.Instance.isEnemyAlertMed && !hasBarkedAboutAlertMid)
        {
            _barkSpawner.SpawnBark(1);
            hasBarkedAboutAlertMid = true;
        }

        if (DialogueQuestManager.Instance.isEnemyAlertHigh && !hasBarkedAboutAlertHigh)
        {
            _barkSpawner.SpawnBark(2);
            hasBarkedAboutAlertHigh = true;
        }
        
        // initial fuel flower processing dialogue
        if (GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.PlayerGetsClose] && !GameStateManager.Instance.isPlayerInGroundCombat &&
            !GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.InitialFuelFlowerProcessingPt1] && GameStateManager.Instance.currentActivePlayerSet == PlayerSetType.Explore)
        {
            Vector3 IBPos = ExploreController.Instance.transform.position;
            float distToIB = (IBPos - transform.position).magnitude;
            
            if (InventoryManager.Instance.HasFuelFlower() && distToIB < initialFuelFlowerDialogueRange && ExploreController.Instance.IsFuelBelowVal(ExploreController.Instance.GetMaxFuel() * 0.9f))
            {
                GameStateManager.Instance.A1SpecialDialoguesUnlockState[
                    A1SpecialDialogues.InitialFuelFlowerProcessingPt1] = true;
                DialogueQuestManager.Instance.PlayYarnDialogue("InitialFuelFlowerPt1");
                DialogueQuestManager.Instance.girlAwaitingContinue = true;
                GiveTask(GirlTask.InDialogue, true);
            }
        }
        
        // A1 Rune dialogue
        if (!GameStateManager.Instance.isPlayerInGroundCombat && GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.PlayerGetsClose] &&
            !GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1Rune] && GameStateManager.Instance.currentActivePlayerSet == PlayerSetType.Explore)
        {
            Vector3 IBPos = ExploreController.Instance.transform.position;
            float distToIB = (IBPos - transform.position).magnitude;

            if (InventoryManager.Instance.hasAcquiredRune && distToIB < initialFuelFlowerDialogueRange)
            {
                GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1Rune] = true;
                DialogueQuestManager.Instance.PlayYarnDialogue("A1AfterCollectedRune");
                DialogueQuestManager.Instance.girlAwaitingContinue = true;
                GiveTask(GirlTask.InDialogue, true);
            }
        }
    }

    public bool TryGiveTask(GirlTask newTask, bool couldResist = false)
    {

        if (currentTask == GirlTask.BeingAttacked)
        {
            if (newTask == GirlTask.InDialogue)
                return false;
            
            if (newTask == GirlTask.FindingResource)
                return false;
            
            if (newTask == GirlTask.Pat)
                return false;

            if (newTask == GirlTask.ProcessFuel)
                return false;
        }
        else if (currentTask == GirlTask.FindingResource || currentTask == GirlTask.MovingToResource ||
                 currentTask == GirlTask.GatheringResource)
        {
            if (newTask == GirlTask.InDialogue)
                return false;
            
            if (newTask == GirlTask.Pat)
                return false;
        }
        else if (currentTask == GirlTask.Feed || currentTask == GirlTask.Pat ||
                 currentTask == GirlTask.InDialogue || currentTask == GirlTask.ProcessFuel)
        {
            if (isBeingInteracted)
                return false;
        }

        if (newTask == GirlTask.MovingToShelter)
        {
            if (SceneObjectManager.Instance.IsSpawnedMobEmpty())
            {
                _barkSpawner.SpawnBark(19);
                return false;
            }
        }
        
        GiveTask(newTask);
        return true;
    }

    private void GiveTask(GirlTask newTask, bool dialogueOverride = false)
    {
        _animator.SetAnimatorBool("chop tree", false);
        _animator.SetAnimatorBool("pull grass", false);
        _animator.SetAnimatorBool("chisel rock", false);
        
        
        if(currentTask == GirlTask.FindingResource || currentTask == GirlTask.MovingToResource || 
           currentTask == GirlTask.GatheringResource || currentTask == GirlTask.Idle || 
           currentTask == GirlTask.IdleAtSpawn || currentTask == GirlTask.Follow ||
           currentTask == GirlTask.MovingToPlace)
        {
            if (currentTask == GirlTask.MovingToPlace)
                _savedDestination = _destination;
            _prevTask = currentTask;
        }
        currentTask = newTask;

        switch (newTask)
        {
            case GirlTask.Pat:
                Debug.LogError("Deprecated Task. Should never happen");
                break;
                /*isBeingInteracted = true;
                OnGirlInteractedWithIB?.Invoke();
                _girlAffinity.PatHead();
                hasPlayedCollectAnimation = false;
                _animator.SetAnimatorTrigger("pat");
                break;*/
            case GirlTask.Feed:
                isBeingInteracted = true;
                hasPlayedCollectAnimation = false;
                _animator.SetAnimatorTrigger("eat");
                break;
            case GirlTask.InDialogue:
                isBeingInteracted = true;
                hasPlayedCollectAnimation = false;
                OnGirlInteractedWithIB?.Invoke();
                if(!dialogueOverride)
                    DialogueQuestManager.Instance.PlayCorrectGirlDialogue();
                break;
            case GirlTask.ProcessFuel:
                isBeingInteracted = true;
                hasPlayedCollectAnimation = false;
                _animator.SetAnimatorTrigger("eat");
                break;
            case GirlTask.FindingResource:
                _resourceGatheringStartingPos = transform.position;
                break;
        }

        if (_prevTask == GirlTask.GatheringResource)
            _prevTask = GirlTask.MovingToResource;
        if(randomWalkingAtSpawn != null)
            StopCoroutine(randomWalkingAtSpawn);
        randomWalkingAtSpawn = null;
        idleWaitTimer = 0.0f;
    }

    private void ExecuteTask()
    {
        switch (currentTask)
        {
            case GirlTask.Idle:                                                         // default idle, will switch to IdleAtSpawn
                idleWaitTimer += Time.deltaTime;
                if(idleWaitTimer > waitTimeBeforeIdleAtSpawn)
                    GiveTask(GirlTask.IdleAtSpawn);
                break;
            case GirlTask.IdleAtSpawn:                                                  // idle at spawn and random walking
                if (randomWalkingAtSpawn == null)
                    randomWalkingAtSpawn = StartCoroutine(RandomWalkingAtSpawn());
                break;
            case GirlTask.FindingResource:
                resourceTarget = null;
                resourceTarget = FindNearestResource();
                if (resourceTarget == null)
                {
                    _barkSpawner.SpawnBark(17);
                    GiveTask(GirlTask.IdleAtSpawn);
                }
                else
                    GiveTask(GirlTask.MovingToResource);
                break;
            case GirlTask.MovingToResource:
                if(resourceTarget == null || resourceTarget.GetComponent<ItemWorldObject>().hasBeenCollected)
                    GiveTask(GirlTask.FindingResource);

                _agent.isStopped = false;
                
                Vector3 leftOffsetPos, rightOffsetPos;
                leftOffsetPos = rightOffsetPos = resourceTarget.transform.position;

                Vector2 offset = resourceTarget.GetComponent<ItemWorldObject>().GetCollectOffset(false);
                
                float collectXOffset = offset.x;
                leftOffsetPos.x -= collectXOffset;
                leftOffsetPos.z += offset.y;
                rightOffsetPos.x += collectXOffset;
                rightOffsetPos.z += offset.y;
                
                Vector3 targetPos =
                    (leftOffsetPos - transform.position).magnitude > (rightOffsetPos - transform.position).magnitude
                        ? rightOffsetPos
                        : leftOffsetPos;
                
                _agent.SetDestination(targetPos);
                _agent.stoppingDistance = collectRadius;
                
                //Reached target
                if(CheckIfReachedDestination())
                    GiveTask(GirlTask.GatheringResource);
                break;
            case GirlTask.GatheringResource:
                if(resourceTarget == null || resourceTarget.GetComponent<ItemWorldObject>().hasBeenCollected)
                {
                    _animator.SetAnimatorBool("chop tree", false);
                    _animator.SetAnimatorBool("pull grass", false);
                    _animator.SetAnimatorBool("chisel rock", false);
                    GiveTask(GirlTask.FindingResource);
                    _collectingTarget = null;
                }

                if (_collectTimer > 0.0f)
                {
                    _collectTimer -= Time.deltaTime;
                }
                else
                {
                    _collectingTarget = resourceTarget.GetComponent<ItemWorldObject>();
                    
                    string triggerStr = "";
                    switch (_collectingTarget.type)
                    {
                        case ResourceType.Tree:
                            _collectingDamage = woodChoppingDamageAttribute.currentVal;
                            triggerStr = "chop tree";
                            break;
                        case ResourceType.Bush:
                            _collectingDamage = foragingDamageAttribute.currentVal;
                            triggerStr = "pull grass";
                            break;
                        case ResourceType.Mineral:
                            _collectingDamage = miningDamageAttribute.currentVal;
                            triggerStr = "chisel rock";
                            break;
                    }

                    if (!hasPlayedCollectAnimation)
                    {
                        _animator.CalculateAnimatorFlip(_collectingTarget.transform.position);
                        _animator.SetAnimatorBool(triggerStr, true);
                        hasPlayedCollectAnimation = true;
                    }
                }
                break;
            case GirlTask.BeingInteracted:
                _agent.isStopped = true;
                break;
            case GirlTask.MovingToPlace:
                _agent.isStopped = false;
                _agent.SetDestination(_destination);
                
                if(CheckIfReachedDestination())
                    GiveTask(GirlTask.Idle);
                break;
            case GirlTask.Follow:
                _agent.isStopped = false;
                Vector3 dest = _playerRef.transform.position;
                dest.x += followOffset.x;
                dest.z += followOffset.y;
                _agent.SetDestination(dest);
                break;
            case GirlTask.BeingAttacked:
                _agent.isStopped = true;
                break;
            case GirlTask.Feed:
                _agent.isStopped = true;
                break;
            case GirlTask.Pat:
                _agent.isStopped = true;
                break;
            case GirlTask.InDialogue:
                _agent.isStopped = true;
                break;
            case GirlTask.ProcessFuel:
                _agent.isStopped = true;
                break;
            case GirlTask.MovingToShelter:
                _agent.isStopped = false;
                _agent.SetDestination(_destination);
                _agent.stoppingDistance = 2.0f;
                
                if(CheckIfReachedDestination())
                {
                    GiveTask(GirlTask.InShelter);
                    HouseShelter.Instance.ShelterGirl();
                }
                break;
            case GirlTask.InShelter:
                _agent.isStopped = true;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private GameObject FindNearestResource()
    {
        GameObject nearestResource = null;
        float nearestDistance = Mathf.Infinity;
        
        foreach (var item in resourceList)
        {
            if(item == null)
                continue;
            
            var resource = item.GetComponent<ItemWorldObject>();
            
            /*if(resource.type != rType)
                continue;*/
            
            if(resource.hasBeenCollected)
                continue;

            float dis = (item.transform.position - _resourceGatheringStartingPos).magnitude;
            
            if(dis > resourceScanRange)
                continue;

            NavMeshPath path = new NavMeshPath();
            
            if(!_agent.CalculatePath(item.transform.position, path))
                continue;
            
            if(path.status == NavMeshPathStatus.PathPartial)
                continue;
            
            if(CalculatePathLength(path) > 100.0f)
                continue;
            
            if (dis < nearestDistance)
            {
                nearestDistance = dis;
                nearestResource = item;
            }
        }

        return nearestResource;
    }

    private float CalculatePathLength(NavMeshPath path)
    {
        float length = 0.0f;
       
        if (path.status != NavMeshPathStatus.PathInvalid)
        {
            for ( int i = 1; i < path.corners.Length; ++i )
            {
                length += Vector3.Distance( path.corners[i-1], path.corners[i] );
            }
        }
       
        return length;
    }

    public void StartInteraction(GirlTask interactTask)
    {
        //GiveTask(interactTask);
    }

    public void FinishedInteraction()
    {
        // Buggy! If assign multiple times, will dead lock itself

        if (currentTask == GirlTask.ProcessFuel)
        {
            ExploreController.Instance.GetComponent<CharacterStatus>().fuelRef.ModifyValue(-15.0f);
        }
        
        //TODO add a bool here to prevent assign interact task when already interacting
        GiveTask(_prevTask);
        if (_prevTask == GirlTask.MovingToPlace)
        {
            _destination = _savedDestination;
        }
        isBeingInteracted = false;
        _agent.isStopped = false;
    }

    public void CollectHitEventHandler()
    {
        hasPlayedCollectAnimation = false;
        _collectingTarget?.ReduceCollectHP(_collectingDamage, transform);
        _collectTimer = collectInterval;
    }

    private bool CheckIfReachedDestination()
    {
        if (!_agent.pathPending)
        {
            if (_agent.remainingDistance <= _agent.stoppingDistance)
            {
                if (!_agent.hasPath || _agent.velocity.sqrMagnitude == 0f)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private IEnumerator RandomWalkingAtSpawn()
    {
        while (true)
        {
            Vector3 randomOffset = new Vector3(Random.Range(-5.0f, 5.0f), 0.0f, Random.Range(-5.0f, 5.0f));
            _agent.SetDestination(spawnPoint.transform.position + randomOffset);
            yield return new WaitForSeconds(Random.Range(5.0f, 10.0f));
        }
    }

    public bool IsSanityLow()
    {
        return _status.sanityRef.GetValue() < _status.sanityRef.GetMaxValue() * 0.75f;
    }

    private void DetectEnemy()
    {
        var mobList = SceneObjectManager.Instance.mobList;
        bool enemyWithinRange = false;

        foreach (var mob in mobList)
        {
            float dis = (transform.position - mob.transform.position).sqrMagnitude;
            if (dis <= detectingRangeForEnemy)
            {
                enemyWithinRange = true;
                break;
            }
        }
        
        if(enemyWithinRange && currentTask != GirlTask.BeingAttacked && currentTask != GirlTask.MovingToPlace && currentTask != GirlTask.MovingToShelter)
        {
            isBeingAttacked = true;
            GiveTask(GirlTask.BeingAttacked);
            if (HouseShelter.Instance && !HouseShelter.Instance.isInDestroyedState)
            {
                TryGiveTask(GirlTask.MovingToShelter);
                SetDestination(HouseShelter.Instance.entranceTransform.transform.position);
            }
        }
        else if(!enemyWithinRange && currentTask == GirlTask.BeingAttacked)
        {
            isBeingAttacked = false;
            if(_prevTask != GirlTask.Feed)
                GiveTask(_prevTask);
            else
            {
                GiveTask(GirlTask.Idle);
            }
            _agent.isStopped = false;
        }
    }
    

    /*public void SetResourceType(ResourceType type)
    {
        resourceType = type;
        _resourceGatheringStartingPos = transform.position;
    }*/
    
    public void SetDestination(Vector3 pos)
    {
        _destination = pos;
    }

    /*public int GetGirlAffinityLevelAsInt()
    {
        return _girlAffinity.GetCurrentAffinityLevelAsInt();
    }*/

    private void HoldLeftEventHandler(InputAction.CallbackContext context)
    {
        if (context.performed && canCheckHoldInput)
        {
            holdingLeftInput = true;
        }
        else if (context.canceled)
        {
            holdingLeftInput = false;
        }
    }
    
    private void HoldRightEventHandler(InputAction.CallbackContext context)
    {
        if (context.performed && canCheckHoldInput)
        {
            holdingRightInput = true;
            
            // check to play right click dialogue
            // if (GameStateManager.Instance && !GameStateManager.Instance.isPlayerInGroundCombat &&
            //     GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.PlayerGetsClose] &&
            //     !GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.PlayerFirstRightClicks])
            // {
            //     SceneObjectManager.Instance.mainCanvas.DisableAllGroundTutorialPanels();
            //     GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.PlayerFirstRightClicks] =
            //         true;
            //     
            //     DialogueQuestManager.Instance.PlayYarnDialogue("PlayerRightClicksA1");
            // }
        }
        else if (context.canceled)
        {
            holdingRightInput = false;
            if (OnHoldingRMB != null)
                OnHoldingRMB(false, transform.position);
        }
    }

    public Vector2 GetGirlViewportPositionAndSize(out float size)
    {
        Vector2 viewPos = _mainCam.WorldToViewportPoint(transform.position);
        Vector2 spriteViewPos =
            _mainCam.WorldToViewportPoint(GetComponentInChildren<SpriteRenderer>().gameObject.transform.position);

        size = Vector2.Distance(viewPos, spriteViewPos) * 1.2f;

        return spriteViewPos;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, resourceScanRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectingRangeForEnemy);
    }
    
    #region ISavable Implementation

    public List<Tuple<string, dynamic>> SaveElement()
    {
        List<Tuple<string, dynamic>> elements = new List<Tuple<string, dynamic>>();

        elements.Add(new Tuple<string, dynamic>("f_A1Health", _status.healthRef.GetValue()));
        elements.Add(new Tuple<string, dynamic>("f_A1Hunger", _status.hungerRef.GetValue()));
        elements.Add(new Tuple<string, dynamic>("f_A1Sanity", _status.sanityRef.GetValue()));
        
        return elements;
    }

    public void LoadElement(SaveData saveData)
    {
        _status.healthRef.SetValue((float)Convert.ToDouble(saveData.saveDict["f_A1Health"]));
        _status.hungerRef.SetValue((float)Convert.ToDouble(saveData.saveDict["f_A1Hunger"]));
        _status.sanityRef.SetValue((float)Convert.ToDouble(saveData.saveDict["f_A1Sanity"]));
    }

    #endregion

    #region IRaycastable Implementation

    public string hoverPrompt { get; set; }
    public bool canClickWithPrompt => true;
    public bool canClickWithoutPrompt => true;    
    public Vector2 hoverPromptOffset => new Vector2(-20f, 60f);
    public void OnClickAction(UIMainCanvas mainCanvas, UIRaycastInteractor raycastInteractor, int button)
    {
        // LMB
        if (button == 0)
        {
            if (GameStateManager.Instance && !GameStateManager.Instance.isPlayerInGroundCombat &&
                GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.PlayerFirstTab])
            {
                SceneObjectManager.Instance.mainCanvas.DisableAllGroundTutorialPanels();
                
                // re-enable gameplay player control
                ExploreController.Instance.ToggleInputMasterGameplay(true);
            }
            
            if (raycastInteractor.itemOnCursor.itemData != null && raycastInteractor.itemOnCursor.itemData.GetItem() is Consumable )
            {
                var consumable = raycastInteractor.itemOnCursor.itemData.GetItem() as Consumable;
                if (!consumable.canBeUsedByPlayer)
                {
                    feedFood = raycastInteractor.itemOnCursor.itemData;
                    if (InventoryManager.Instance.TestIfCanUseItemOnCharacter(feedFood.GetItem(), _status))
                    {
                        // Can feed food now (task wise)
                        if (TryGiveTask(GirlTask.Feed))
                        {
                            // Girl can eat now (hunger status wise)
                            if (InventoryManager.Instance.UseItemOnCharacter(feedFood.GetItem(), _status))
                            {
                                raycastInteractor.UsedItemOnCursor();
                                //_girlAffinity.FeedFood(feedFood);

                                //vfx
                                StatsRecoveringVFXBasedOnConsumable(consumable);

                                if (raycastInteractor.itemOnCursor.itemData == null)
                                {
                                    GameObject mouseHoverPrompt = SceneObjectManager.Instance.mainCanvas.mouseHoverPrompt;
                                    mouseHoverPrompt.SetActive(false);
                                }
                            }
                            else
                            {
                                DebugManager.LogDebugString("GirlAI: Failed trying to feed girl, already full");
                                _barkSpawner.SpawnBark(16);
                            }
                        }
                        else
                        {
                            DebugManager.LogDebugString("GirlAI: Failed trying to feed girl, blocked by task (in theory should never happens)");
                        }
                    }
                    else
                    {
                        _barkSpawner.SpawnBark(16);
                        raycastInteractor.ReturnItemToPrevSlot();
                    }
                }
                else
                {
                    
                    feedFood = raycastInteractor.itemOnCursor.itemData;

                    // Process fuel flower
                    if(InventoryManager.Instance.TestIfCanUseItemOnCharacter(feedFood.GetItem(), ExploreController.Instance.GetComponent<CharacterStatus>()))
                    {
                        // can use, need process
                        if (TryGiveTask(GirlTask.ProcessFuel))
                        {
                            raycastInteractor.UsedItemOnCursor();
                            if (raycastInteractor.itemOnCursor.itemData == null)
                            {
                                GameObject mouseHoverPrompt = SceneObjectManager.Instance.mainCanvas.mouseHoverPrompt;
                                mouseHoverPrompt.SetActive(false);
                            }
                            
                            // if fuel flower is processed but dialogue pt1 has not triggered yet, mark fuel flower tutorials as completed
                            if (GameStateManager.Instance && !GameStateManager.Instance.A1SpecialDialoguesUnlockState[
                                    A1SpecialDialogues.InitialFuelFlowerProcessingPt1])
                            {
                                GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.InitialFuelFlowerProcessingPt1] = true;
                                GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.InitialFuelFlowerProcessingPt2] = true;
                            }
                            
                            // fuel flower processing dialogue pt2
                            if (GameStateManager.Instance && !GameStateManager.Instance.isPlayerInGroundCombat &&
                                GameStateManager.Instance.A1SpecialDialoguesUnlockState[
                                    A1SpecialDialogues.InitialFuelFlowerProcessingPt1] &&
                                !GameStateManager.Instance.A1SpecialDialoguesUnlockState[
                                    A1SpecialDialogues.InitialFuelFlowerProcessingPt2])
                            {
                                GameStateManager.Instance.A1SpecialDialoguesUnlockState[
                                    A1SpecialDialogues.InitialFuelFlowerProcessingPt2] = true;
                                DialogueQuestManager.Instance.PlayYarnDialogue("InitialFuelFlowerPt2");
                            }

                            //vfx
                            NPCBoostPlayerFuelVFX.SendEvent("OnBoost");
                        }
                        else
                        {
                            
                        }
                    }
                    else
                    {
                        //bark
                        _barkSpawner.SpawnBark(18);
                        raycastInteractor.ReturnItemToPrevSlot();
                    }
                }
            }
            else
            {
                UIInteractionRing.Instance.ShowRing();
            }
        }
        else if (button == 1)
        {
            if (!UIInteractionRing.isMoveOrderActive)
            {
                UIInteractionRing.isMoveOrderActive = true;
                UIInteractionRing.Instance.HideRing();
                UIInteractionRing.Instance.SpawnMovePointer();
            }
        }
    }

    public string GetHoverPrompt()
    {
        return hoverPrompt;
    }

    public bool ShouldShowPrompt(UIMainCanvas mainCanvas, UIRaycastInteractor raycastInteractor)
    {
        if (raycastInteractor.itemOnCursor.itemData != null &&
            raycastInteractor.itemOnCursor.itemData.GetItem() is Consumable)
        {
            var consumable = raycastInteractor.itemOnCursor.itemData.GetItem() as Consumable;
            if (!consumable.canBeUsedByPlayer)
                hoverPrompt = LocalizationUtility.GetLocalizedString("FEED");
            else
                hoverPrompt = LocalizationUtility.GetLocalizedString("PROCESS");
            
            return true;
        }

        // TODO: Handle hide interaction ring logic here;
        
        return false;
    }
    
    #endregion

}
