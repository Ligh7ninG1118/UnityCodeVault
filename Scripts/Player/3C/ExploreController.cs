using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UtilityEnums;
using UtilityFunc;

[RequireComponent(typeof(PlayerInput))]
public class ExploreController : MonoBehaviour, ISavable
{
    public static ExploreController Instance;
    public static Action BulletHitEnemy;
    public static Action GirlEntersLowHealth;
    public static Action AllEnemiesCleared;
    
    [Header("Movement")]
    [SerializeField][Range(1.0f, 10.0f)] private float baseMovingSpeed = 2.5f;
    [SerializeField] [Range(0.1f, 1.0f)] private float pushSpeedFromBeingHit = 1.0f;

    
    [Header("Resource Gathering")]
    [Tooltip("Cooldown time in seconds while collecting ")]
    public float collectInterval = 0.5f;
    
    [Tooltip("Collect radius")]
    public float collectRadius = 1.0f;
    public float collectWalkToRadius = 3.0f;
    
    [Tooltip("Only collect resource within this degree in front of the player")]
    public float collectDeg = 90.0f;
    
    [SerializeField][Range(0.01f, 3.0f)] private float baseMiningDamage = 1.0f;
    [SerializeField][Range(0.01f, 3.0f)] private float baseWoodChoppingDamage = 1.0f;
    [SerializeField][Range(0.01f, 3.0f)] private float baseForagingDamage = 1.0f;

    [SerializeField] [Range(0.01f, 20.0f)] private float magneticPickUpRange = 2.5f;
    
    [Header("VFX")]
    public ParticleSystem dustVFX;
    [SerializeField] NPCBootsUpdateLevelTwo PlayerDashVFX;
    
    // Attributes
    [HideInInspector] public UpgradableAttribute movingSpeedAttribute;
    [HideInInspector] public UpgradableAttribute miningDamageAttribute;
    [HideInInspector] public UpgradableAttribute woodChoppingDamageAttribute;
    [HideInInspector] public UpgradableAttribute foragingDamageAttribute;

    
    // Component References
    private InputMaster _inputMaster;
    private CharacterController _characterController;
    [HideInInspector] public AnimatorController2D _animator;
    private Rigidbody _rb;
    private CharacterStatus _status;
    private Combatant _combatant;
    
    // Input
    [HideInInspector] public Vector2 rawMovingVector;
    [HideInInspector] public bool isInteracting = false;
    [HideInInspector] public bool isAttacking = false;
    [HideInInspector] public bool isMoving = false;


    [HideInInspector] public float collectTimer = 0.0f;
    private bool _hasPlayedAttackAnim = false;
    public ItemWorldObject _resourceTarget;
    public ItemWorldObject _pathfindingTarget;
    public GameObject _simplePathfindingTarget;
    private Vector3 _lastMovingDir = Vector3.back;
    public bool _isQueueingCollectAction = false;
    public bool _isWithinInteractRadius = false;
    public bool _isWorldInteracting = false;
    public bool _isTriggeredByWorldInteract = false;
    [HideInInspector] public Vector2 realMovingVector;
    
    // player is stiff for a short period of time after getting hit
    private bool _isPlayerStiff;

    private IBBarkSpawner _ibBarkSpawner;

    private float _timeAfterAllEnemiesCleared;
    private bool _areAllEnemiesCleared;

    [HideInInspector] public float clawSpeedMultiplier = 1.0f;
    [HideInInspector] public float overclockCooldownDelta = 0.0f;
    [HideInInspector] public float legExtentAdd = 0.0f;
    [HideInInspector] public float fuelConsumptionRateMultiplier = 1.0f;


    [HideInInspector] public List<GameObject> hideGunSpriteRequesters;

    [Header("Narrative")]
    public float A1InitialDialogueRange = 4.0f;

    private Camera _exploreCam;
    private bool _wasGirlInView;

    [Header("Upgrade")]
    public float emergencyFuelReserve;
    public float dashCooldown;
    public float dashDuration;
    public float dashSpeed;
    [HideInInspector] public bool isEmergencyFuelUnlocked;
    [HideInInspector] public bool isDashUnlocked;
    private bool _isEmergencyFuelReady = true;
    private float _dashCooldownTimer;
    [HideInInspector] public bool hasTaughtDash;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("Two ExploreControllers in the scene");
        }
        Instance = this;
        
        _characterController = GetComponent<CharacterController>();
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponentInChildren<AnimatorController2D>();
        _status = GetComponent<CharacterStatus>();
        _combatant = GetComponent<Combatant>();
        _ibBarkSpawner = GetComponent<IBBarkSpawner>();

        movingSpeedAttribute = new(baseMovingSpeed);
        miningDamageAttribute = new(baseMiningDamage);
        foragingDamageAttribute = new(baseForagingDamage);
        woodChoppingDamageAttribute = new(baseWoodChoppingDamage);
        hideGunSpriteRequesters = new List<GameObject>();

        overclockCooldownDelta = 0.0f;

        _dashCooldownTimer = dashCooldown;

        ((ISavable)this).Subscribe();
    }

    private void Start()
    {
        _exploreCam = GameObject.FindGameObjectWithTag("ExploreMainCamera").GetComponent<Camera>();
        _animator.InitializeAnimBool();

        _combatant.OnStartingAttack += StartingAttackEventHandler;
        _combatant.OnBeingHit += BeingHitEventHandler;
        _animator.OnAttackHit += AttackHitEventHandler;
        
        // handle player death
        _status.fuelRef.OnDepleting += PlayerDeathEventHandler;
        _status.fuelRef.OnEnteringLowState += EnterLowFuelStateHandler;
        _status.fuelRef.OnValueIncreased += FuelRecoverHandler;

        InventoryManager.Instance.OnNewExistingItemAdded += NewItemAddedEventHandler;
        InventoryManager.Instance.OnNewUniqueItemAdded += NewItemAddedEventHandler;

        BulletHitEnemy += BulletHitEnemyHandler;
        GirlEntersLowHealth += GirlEntersLowHealthHandler;
        AllEnemiesCleared += AllEnemiesClearedHandler;
        
        SceneObjectManager.Instance.playerRef = gameObject;
        
        GameStateManager.OnCliffTutorialCompleted += OnCliffTutorialCompletedHandler;
        
        // start the cliff tutorial level if it has not been completed yet
        if (!GameStateManager.Instance.hasCliffTutorialLevelCompleted)
        {
            Invoke(nameof(LoadCliffTutorial), 0.01f);
        }
        // start tutorial 1 if cliff tutorial level is completed
        else
        {
            // CutsceneManager.Instance.KickstartCutscene(CutsceneID.Tutorial1);
        }
    }

    private void LoadCliffTutorial()
    {
        GameStateManager.Instance.StartLoadCliffTutorial();
    }

    private void OnDestroy()
    {
        _combatant.OnStartingAttack -= StartingAttackEventHandler;
        _combatant.OnBeingHit -= BeingHitEventHandler;
        _animator.OnAttackHit -= AttackHitEventHandler;
        
        _status.fuelRef.OnDepleting -= PlayerDeathEventHandler;
        _status.fuelRef.OnEnteringLowState -= EnterLowFuelStateHandler;
        _status.fuelRef.OnValueIncreased -= FuelRecoverHandler;
        
        InventoryManager.Instance.OnNewExistingItemAdded -= NewItemAddedEventHandler;
        InventoryManager.Instance.OnNewUniqueItemAdded -= NewItemAddedEventHandler;

        BulletHitEnemy -= BulletHitEnemyHandler;
        GirlEntersLowHealth -= GirlEntersLowHealthHandler;
        AllEnemiesCleared -= AllEnemiesClearedHandler;
        
        GameStateManager.OnCliffTutorialCompleted -= OnCliffTutorialCompletedHandler;
        
        ((ISavable)this).Unsubscribe();

    }

    private void OnEnable()
    {
        _inputMaster = new InputMaster();
        _inputMaster.Enable();
        RegisterInputEvents();
    }

    private void OnDisable()
    {
        UnregisterInputEvents();
        _inputMaster.Disable();
    }

    public bool IsEmergencyFuelReady()
    {
        return _isEmergencyFuelReady;
    }

    public void ToggleInputMasterGameplay(bool enable)
    {
        if (enable)
        {
            _inputMaster.Gameplay.Enable();
        }
        else
        {
            _inputMaster.Gameplay.Disable();
        }
    }

    private void OnCliffTutorialCompletedHandler()
    {
        // Invoke(nameof(DelayStartCutsceneTutorial1), 6.5f);
    }
    
    private void DelayStartCutsceneTutorial1()
    {
        CutsceneManager.Instance.KickstartCutscene(CutsceneID.Tutorial1);
    }

    private void OnTeleportPlayerHomeHandler(InputAction.CallbackContext context)
    {
        if (GameStateManager.Instance)
        {
            GameStateManager.Instance.TeleportPlayerHome();
        }
    }

    public static void TriggerBulletHitEnemyEvent()
    {
        BulletHitEnemy?.Invoke();
    }

    public static void TriggerGirlEnterLowHealthEvent()
    {
        GirlEntersLowHealth?.Invoke();
    }

    public static void TriggerAllEnemiesClearedEvent()
    {
        AllEnemiesCleared?.Invoke();
        Instance._areAllEnemiesCleared = true;
        Instance._timeAfterAllEnemiesCleared = 0.0f;
    }

    private void BulletHitEnemyHandler()
    {
        // player first hits enemy after enemy wave has spawned
        if (DialogueQuestManager.Instance.hasMonsterWaveJustSpawned)
        {
            _ibBarkSpawner.SpawnBark(1);
            DialogueQuestManager.Instance.hasMonsterWaveJustSpawned = false;
        }
    }

    private void EnterLowFuelStateHandler()
    {
        // spawn low health (fuel) bark
        _ibBarkSpawner.SpawnBark(2);
        
        // play sfx
        AudioManager.Instance.PlaySFXOneShot2D("IBLowFuel");
    }

    private void FuelRecoverHandler(float val)
    {
        _isEmergencyFuelReady = true;
        UIMonitorSystem.Instance?.SetEmergencyFuelStats(false);
        AudioManager.Instance.PlaySFXOneShot2D("IBRecoverFuel");
    }

    private void GirlEntersLowHealthHandler()
    {
        // spawn girl low health bark
        _ibBarkSpawner.SpawnBark(3);
    }

    private void AllEnemiesClearedHandler()
    {
        _ibBarkSpawner.SpawnBark(4);
    }

    public void SpawnBark(int scenario)
    {
        _ibBarkSpawner.SpawnBark(scenario);
    }
    
    private void NewItemAddedEventHandler(ItemData itemData, bool isCollectedByPlayer)
    {
        VFXControlHelper vfxCH = GetComponent<VFXControlHelper>();
        if (vfxCH != null && isCollectedByPlayer)
        {
            vfxCH.TriggerVFXEvent("OnPlay");//play collect vfx
        }
    }
    
    // Player's attack is driven by animation
    // So when received this event, play attack animation
    private void StartingAttackEventHandler()
    {
        if(!_hasPlayedAttackAnim)
        {
            //_animator.SetAnimatorTrigger("Attack");
            _hasPlayedAttackAnim = true;
        }
    }

    // Received this event from animator/animation
    private void AttackHitEventHandler()
    {
        if (isInteracting || _isWorldInteracting)
        {
            collectTimer = collectInterval;
        }
        
        if (_resourceTarget != null && _isWithinInteractRadius)
        {
            float dmgVal = 0.0f;
            switch (_resourceTarget.type)
            {
                case ResourceType.Tree:
                    dmgVal = woodChoppingDamageAttribute.currentVal;
                    break;
                case ResourceType.Bush:
                    dmgVal = foragingDamageAttribute.currentVal;
                    break;
                case ResourceType.Mineral:
                    dmgVal = miningDamageAttribute.currentVal;
                    break;
            }
            
            _resourceTarget.ReduceCollectHP(dmgVal, transform);
            TutorialManager.Instance.IncreaseResourceCollectionCount();
            if (_resourceTarget.hasBeenCollected)
            {
                _animator.SetAnimatorBool("Chisel Rock", false);
                _animator.SetAnimatorBool("Chop Grass", false);
                _animator.SetAnimatorBool("Chop Tree", false);
                _hasPlayedAttackAnim = false;
            }
            _resourceTarget = null;
        }
        else if(isAttacking)
        {
            _combatant.ProcessAttack();
        }

        if (_isTriggeredByWorldInteract)
        {
            _animator.SetAnimatorBool("Chisel Rock", false);
            _animator.SetAnimatorBool("Chop Grass", false);
            _animator.SetAnimatorBool("Chop Tree", false);
            _hasPlayedAttackAnim = false;
            _isTriggeredByWorldInteract = false;
        }
    }

    // Attacked by opponent
    // Need to reset attack anim bool since our attack anim might be overrided by hurt animation
    private void BeingHitEventHandler(float damage, Vector3 attckerPos)
    {
        if (!_combatant.isInvincible)
        {
            _status.fuelRef.ModifyValue(damage);
        
            _hasPlayedAttackAnim = false;
            _animator.SetAnimatorTrigger("Player Hurt");
        
            // enable invincible frame
            _combatant.isInvincible = true;
            
            // make the player stiff
            _isPlayerStiff = true;
            
            // calculate push direction on the player
            Vector3 pushDirection = transform.position - attckerPos;
            pushDirection.y = 0f;
            
            // move the player by half of the time of the i-frame
            StartCoroutine(PlayerPushedBack(pushDirection, _combatant.beingHitPushBackDuration));
            
            // set timer to disable invincible frame
            Invoke(nameof(DisableInvincibility), _combatant.invincibleDuration);
        }
    }

    public bool IsFuelBelowVal(float val)
    {
        return _status.fuelRef.GetValue() < val;
    }

    private IEnumerator PlayerPushedBack(Vector3 pushDirection, float pushDuration)
    {
        float timer = 0f;
        while (timer < pushDuration)
        {
            timer += Time.deltaTime;
            Vector3 futurePos = transform.position + pushDirection * (pushSpeedFromBeingHit * Time.deltaTime);
            if (NavMeshUtility.isPositionOnNavMesh(futurePos))
            {
                transform.position = futurePos;
            }

            yield return null;
        }
        
        // restore player control after push back is finished
        RestorePlayerControl();
    }

    private void RestorePlayerControl()
    {
        _isPlayerStiff = false;
    }

    private void DisableInvincibility()
    {
        _combatant.isInvincible = false;
    }
    
    [ContextMenu("Unlock emergency fuel")]
    public void UnlockEmergencyFuel()
    {
        isEmergencyFuelUnlocked = true;
        UIMonitorSystem.Instance?.EnableEmergencyFuelDisplay();
    }
    
    [ContextMenu("Unlock dash")]
    public void UnlockDash()
    {
        isDashUnlocked = true;
    }

    private void PlayerDeathEventHandler()
    {
        if (GameStateManager.Instance.currentActivePlayerSet == PlayerSetType.Climbing)
        {
            return;
        }
        
        Debug.Log("handle game over");
        
        // if emergency fuel can be used and it is unlocked
        if (_isEmergencyFuelReady && isEmergencyFuelUnlocked)
        {
            // refuel
            UseEmergencyFuel();

            return;
        }
        
        // process game over in game state manager
        GameStateManager.Instance.StartProcessGameOver();
    }

    public void UseEmergencyFuel()
    {
        ClimbController.Instance.SetFuel(emergencyFuelReserve);
        _status.fuelRef.ModifyValue(-emergencyFuelReserve);
        UIMonitorSystem.Instance?.SetEmergencyFuelStats(true);
        _isEmergencyFuelReady = false;
    }
    
    // private IEnumerator ProcessGameOver()
    // {
    //     // SFX
    //     AudioManager.Instance.PlaySFXOneShot2D("PlayerDie");
    //     
    //     _animator.SetAnimatorTrigger("Player Die");
    //
    //     
    //     yield return new WaitForSeconds(GameStateManager.Instance.gameOverDelayTime);
    //     
    //     // pause the game
    //     Time.timeScale = 0f;
    //
    //     // pull up game over menu
    //     if (SceneObjectManager.Instance)
    //     {
    //         SceneObjectManager.Instance.mainCanvas.SetGameOverMenuActive(true, false);
    //     }
    // }
    
    void Update()
    {
        
        
        
        // debug function to mark all tutorials complete
        if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha0))
        {
            ToggleInputMasterGameplay(true);
            DialogueQuestManager.Instance.StopRunningDialogue();
            GameStateManager.Instance.SetAllGroundTutorialComplete();
            SceneObjectManager.Instance.mainCanvas.DisableAllGroundTutorialPanels();
        }
        
        // if player control is paused by dialogue, don't update on player control OR player is stiff from hit
        if ((DialogueQuestManager.Instance && DialogueQuestManager.Instance.isInDialogue) || _isPlayerStiff)
        {
            _status.shouldPauseDebuff = true;
            rawMovingVector = realMovingVector = Vector2.zero;
            return;
        }
        
        Vector3 A1Pos = GirlAI.Instance.transform.position;
        float distToA1 = (A1Pos - transform.position).magnitude;
        
        // dialogues triggered when the player gets close to A1
        if (GameStateManager.Instance && distToA1 < A1InitialDialogueRange && !GameStateManager.Instance.isPlayerInGroundCombat)
        {
            // initial interaction with A1
            if ( GameStateManager.Instance.hasCliffTutorialLevelCompleted &&
                !GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.PlayerGetsClose])
            {
                GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.PlayerGetsClose] = true;
                
                DialogueQuestManager.Instance.PlayYarnDialogue("PlayerGetsClose");
            }
            // sanity tutorial dialogue
            else if (GirlAI.Instance && GirlAI.Instance.IsSanityLow() && !GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1SanityTutorial])
            {
                GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1SanityTutorial] = true;
                
                DialogueQuestManager.Instance.PlayYarnDialogue("A1SanityTutorial");
            }
            // should go to cliff dialogue
            else if (GameStateManager.Instance.totalGameTime > 480f && !GameStateManager.Instance.hasPlayerBeenToCliff && !GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1GoToCliff])
            {
                GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1GoToCliff] = true;
                
                DialogueQuestManager.Instance.PlayYarnDialogue("A1GoToCliff");
            }
            else if (GameStateManager.Instance.hasPlayerBeenToRelicFor1Min &&
                     !GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1FamilyRelic])
            {
                GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1FamilyRelic] = true;
                
                DialogueQuestManager.Instance.PlayYarnDialogue("A1FamilyRelic");
            }
        }
        
        // tick the dash cooldown timer
        if (_dashCooldownTimer > 0.0f)
        {
            _dashCooldownTimer -= Time.deltaTime;
        }
        
        // feed enemy clear info to dialogue system
        if (_areAllEnemiesCleared)
        {
            _timeAfterAllEnemiesCleared += Time.deltaTime;
            DialogueQuestManager.Instance.isOneMinAfterEnemiesCleared = _timeAfterAllEnemiesCleared <= 60f;
        }
        
        // bark when girl is not in view anymore
        bool isGirlInView = CameraUtility.IsPositionInCameraView(GirlAI.Instance.transform.position, _exploreCam);
        if (!isGirlInView && _wasGirlInView)
        {
            _ibBarkSpawner.SpawnBark(7);
        }
        _wasGirlInView = isGirlInView;
        
        _status.shouldPauseDebuff = false;
        
        ProcessInput();
        ProcessMovement();
        ProcessResourceGathering();
        ProcessItemPickUp();
        ProcessResourceInteraction();

        if (!isMoving && isAttacking)
            _combatant.shouldDoAttack = true;
        else
            _combatant.shouldDoAttack = false;


        Vector3 adjustedPos = transform.position;
        adjustedPos.y = 0.0f;
        transform.position = adjustedPos;
    }

    #region Input Handling
    
    private void ProcessInput()
    {
        rawMovingVector = _inputMaster.Gameplay.Movement.ReadValue<Vector2>();
    }

    private void RegisterInputEvents()
    {
        _inputMaster.Gameplay.Interact.performed += InteractInputEventHandler;
        _inputMaster.Gameplay.Interact.canceled += InteractInputEventHandler;
        
        _inputMaster.Gameplay.Attack.performed += AttackInputEventHandler;
        _inputMaster.Gameplay.Attack.canceled += AttackInputEventHandler;

        _inputMaster.UI.LeftClick.performed += WorldInteractInputEventHandler;

        _inputMaster.Gameplay.TeleportHome.performed += OnTeleportPlayerHomeHandler;

        _inputMaster.Gameplay.Dash.performed += DashHandler;
    }

    private void UnregisterInputEvents()
    {
        _inputMaster.Gameplay.Interact.performed -= InteractInputEventHandler;
        _inputMaster.Gameplay.Interact.canceled -= InteractInputEventHandler;

        _inputMaster.Gameplay.Attack.performed -= AttackInputEventHandler;
        _inputMaster.Gameplay.Attack.canceled -= AttackInputEventHandler;
        
        _inputMaster.UI.LeftClick.performed -= WorldInteractInputEventHandler;
        
        _inputMaster.Gameplay.TeleportHome.performed -= OnTeleportPlayerHomeHandler;

        _inputMaster.Gameplay.Dash.performed -= DashHandler;
    }
    
    #endregion


    #region Gameplay Logic

    private void DashHandler(InputAction.CallbackContext context)
    {
        // if we can dash
        if (isDashUnlocked && _dashCooldownTimer <= 0.0f)
        {
            SceneObjectManager.Instance.mainCanvas.DisableGroundTutorialPanelAtIndex(4);
            
            _dashCooldownTimer = dashCooldown;
            _isPlayerStiff = true;
            _combatant.isInvincible = true;
            
            AudioManager.Instance.PlaySFXOneShot2D("IBSprint");
            
            StartCoroutine(DashCoroutine());
            
            Invoke(nameof(DisableInvincibility), dashDuration);
        }
    }

    private IEnumerator DashCoroutine()
    {
        PlayerDashVFX.isLevelTwo = true;
        float timer = 0f;
        while (timer < dashDuration)
        {
            timer += Time.deltaTime;
            Vector3 predictMoveVec = _lastMovingDir * (dashSpeed * Time.deltaTime);
            Vector3 futurePos = transform.position + predictMoveVec;
            if (NavMeshUtility.isPositionOnNavMesh(futurePos))
            {
                _characterController.Move(predictMoveVec);
            }

            yield return null;
        }
        
        // restore player control after push back is finished
        RestorePlayerControl();
        PlayerDashVFX.isLevelTwo = false;
    }
    
    private void ProcessMovement()
    {
        Vector3 movingDir = new Vector3(rawMovingVector.x, 0.0f, rawMovingVector.y);
        realMovingVector = Vector2.zero;
        isMoving = (movingDir != Vector3.zero);

        if (isMoving)
        {
            _pathfindingTarget = null;
            _simplePathfindingTarget = null;
            _lastMovingDir = movingDir;
            realMovingVector = new Vector2(movingDir.x, movingDir.z);
            

            Vector3 moveVec = movingSpeedAttribute.currentVal * Time.deltaTime * movingDir.normalized;
            Vector3 predictPos = transform.position + moveVec;
            
            if(NavMeshUtility.isPositionOnNavMesh(predictPos))
                _characterController.Move(moveVec);
        }
        else if (_pathfindingTarget != null)
        {
            Vector3 leftOffsetPos, rightOffsetPos;
            leftOffsetPos = rightOffsetPos = _pathfindingTarget.transform.position;

            Vector2 offset = _pathfindingTarget.GetComponent<ItemWorldObject>().GetCollectOffset(true);
            
            float collectXOffset = offset.x;
            leftOffsetPos.x -= collectXOffset;
            leftOffsetPos.z += offset.y;
            rightOffsetPos.x += collectXOffset;
            rightOffsetPos.z += offset.y;
                
            float dis = Mathf.Min((leftOffsetPos - transform.position).magnitude, (rightOffsetPos - transform.position).magnitude);
            Vector3 targetPos =
                (leftOffsetPos - transform.position).magnitude > (rightOffsetPos - transform.position).magnitude
                    ? rightOffsetPos
                    : leftOffsetPos;
            
            if (dis > collectRadius)
            {
                Vector3 dirToTarget = (targetPos - transform.position).normalized;
                realMovingVector = new Vector2(dirToTarget.x, dirToTarget.z);
                
                Vector3 moveVec = movingSpeedAttribute.currentVal * Time.deltaTime * dirToTarget;
                Vector3 predictPos = transform.position + moveVec;
                if(NavMeshUtility.isPositionOnNavMesh(predictPos))
                    _characterController.Move(moveVec);
                
                //_characterController.Move(movingSpeedAttribute.currentVal * Time.deltaTime * dirToTarget);
            }
        }
        else if (_simplePathfindingTarget != null)
        {
            Vector3 targetPos = _simplePathfindingTarget.transform.position;

            if (_simplePathfindingTarget.GetComponent<CookingPot>())
            {
                Vector2 offset = _simplePathfindingTarget.GetComponent<CookingPot>().interactOffset;
                targetPos += (Vector3)offset;
            }
            float dis = (targetPos - transform.position).magnitude;
            if (dis > 0.55f)
            {
                Vector3 dirToTarget = (targetPos - transform.position).normalized;
                realMovingVector = new Vector2(dirToTarget.x, dirToTarget.z);
                
                Vector3 moveVec = movingSpeedAttribute.currentVal * Time.deltaTime * dirToTarget;
                Vector3 predictPos = transform.position + moveVec;
                if(NavMeshUtility.isPositionOnNavMesh(predictPos))
                    _characterController.Move(moveVec);
                //_characterController.Move(movingSpeedAttribute.currentVal * Time.deltaTime * dirToTarget);
            }
            else
            {
                //TODO: Change this! Could also be reaching cooking pot
                _simplePathfindingTarget = null;
                GroundElevator.Instance.ToggleOperatingPanel(true);
            }
        }
    }

    private void InteractInputEventHandler(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            isInteracting = true;
            hideGunSpriteRequesters.Add(gameObject);
        }
        else if (context.canceled)
        {
            isInteracting = false;
            _animator.SetAnimatorBool("Chisel Rock", false);
            _animator.SetAnimatorBool("Chop Grass", false);
            _animator.SetAnimatorBool("Chop Tree", false);
            _hasPlayedAttackAnim = false;
            
            hideGunSpriteRequesters.Remove(gameObject);
        }

        if (isAttacking)
        {
            isInteracting = false;
        }
    }
    
    private void AttackInputEventHandler(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            isAttacking = true;
        }
        else if (context.canceled)
        {
            isAttacking = false;
        }
    }
    
    private void WorldInteractInputEventHandler(InputAction.CallbackContext context)
    {
        if(UIInteractionRing.isMoveOrderActive)
            return;
        
        var mainCanvas = SceneObjectManager.Instance.mainCanvas;
        var raycaster = SceneObjectManager.Instance.mainCanvas.gameObject.GetComponent<UIRaycastInteractor>();
        
        if(mainCanvas.GetComponent<UIMainCanvas>().shouldBlockUI)
            return;
        
        GameObject clickedObject = raycaster.mouseOverObject;
        
        if (clickedObject)
        {
            ItemWorldObject iwo;
            GroundElevatorOperatingPanel panel;
            CookingPot pot;
            
            if (clickedObject.TryGetComponent<ItemWorldObject>(out iwo) && raycaster.itemOnCursor.itemData == null)
            {
                _simplePathfindingTarget = null;
                
                _pathfindingTarget = iwo;
                _resourceTarget = iwo;
                _isWithinInteractRadius = true;
                _isQueueingCollectAction = true;
                _isWorldInteracting = true;
            }
            else if(clickedObject.TryGetComponent<GroundElevatorOperatingPanel>(out panel) && raycaster.itemOnCursor.itemData == null)
            {
                _pathfindingTarget = null;
                
                _simplePathfindingTarget = panel.IBMoveToTransform.gameObject;
                _isWorldInteracting = true;
            }
            else if (clickedObject.TryGetComponent<CookingPot>(out pot))
            {
                _pathfindingTarget = null;
                
                _simplePathfindingTarget = pot.gameObject;
                _isWorldInteracting = true;
            }
        }
    }

    private void ProcessResourceGathering()
    {
        //Count timer (even when not interacting)
        if (collectTimer > 0.0f)
        {
            collectTimer -= Time.deltaTime;
        }
        else
        {
            if(!isInteracting || isMoving)
                return;
            
            Collider[] colliders = Physics.OverlapSphere(transform.position, collectWalkToRadius);
        
            GameObject nearestObj = null;
            float nearestDis = Mathf.Infinity;
            if (colliders.Length > 0)
            {
                foreach (var collider in colliders)
                {
                    if (!collider.CompareTag("Resource"))
                        continue;
                    
                    if(collider.GetComponentInParent<ItemWorldObject>().hasBeenCollected)
                        continue;

                    Vector3 dir = (collider.transform.position - transform.position).normalized;
                    float dotResult = Vector3.Dot(dir, _lastMovingDir);
                    float dotResultToDeg = Mathf.Acos(dotResult) * Mathf.Rad2Deg;

                    if (dotResultToDeg > collectDeg)
                        continue;
                    
                    float dis = Vector3.Distance(transform.position, collider.transform.position);
                    if (dis < nearestDis)
                    {
                        nearestObj = collider.GetComponentInParent<ItemWorldObject>().gameObject;
                        nearestDis = dis;
                    }
                }
            }

            if (nearestObj != null)
            { 
                _resourceTarget = nearestObj.GetComponent<ItemWorldObject>();

                Vector3 leftOffsetPos, rightOffsetPos;
                leftOffsetPos = rightOffsetPos = _resourceTarget.transform.position;

                Vector2 offset = _resourceTarget.GetComponent<ItemWorldObject>().GetCollectOffset(true);
                
                float collectXOffset = offset.x;
                leftOffsetPos.x -= collectXOffset;
                leftOffsetPos.z += offset.y;
                rightOffsetPos.x += collectXOffset;
                rightOffsetPos.z += offset.y;
                
                float dis = Mathf.Min((leftOffsetPos - transform.position).magnitude, (rightOffsetPos - transform.position).magnitude);
                if (dis > collectRadius)
                {
                    _pathfindingTarget = _resourceTarget;
                    _isWithinInteractRadius = false;
                }
                else
                {
                    _isWithinInteractRadius = true;
                }
                
            }
            
            
            if (!_hasPlayedAttackAnim && _isWithinInteractRadius && _resourceTarget != null)
            {
                //_animator.SetAnimatorTrigger("Attack Collect");
                _animator.CalculateAnimatorFlip(_resourceTarget.transform.position);
                switch (_resourceTarget.type)
                {
                    case ResourceType.Tree:
                        _animator.SetAnimatorBool("Chop Tree", true);
                        break;
                    case ResourceType.Bush:
                        _animator.SetAnimatorBool("Chop Grass", true);
                        break;
                    case ResourceType.Mineral:
                        _animator.SetAnimatorBool("Chisel Rock", true);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                _hasPlayedAttackAnim = true;
            }
        }
        
    }

    private void ProcessItemPickUp()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, magneticPickUpRange);
        foreach (var collider in colliders)
        {
            ItemObject itemObject;
            if (collider.TryGetComponent<ItemObject>(out itemObject))
            {
                if(InventoryManager.Instance.TryAddItem(itemObject.itemData, true, true))
                    itemObject.movingToPlayer = true;
            }
        }
    }

    private void ProcessResourceInteraction()
    {
        if (_pathfindingTarget != null)
        {
            Vector3 leftOffsetPos, rightOffsetPos;
            leftOffsetPos = rightOffsetPos = _pathfindingTarget.transform.position;

            if (_pathfindingTarget.GetComponent<ItemWorldObject>())
            {
                Vector2 offset = _resourceTarget.GetComponent<ItemWorldObject>().GetCollectOffset(true);
                
                float collectXOffset = offset.x;
                leftOffsetPos.x -= collectXOffset;
                leftOffsetPos.z += offset.y;
                rightOffsetPos.x += collectXOffset;
                rightOffsetPos.z += offset.y;
            }
            else if (_pathfindingTarget.GetComponent<CookingPot>())
            {
                Vector2 offset = _pathfindingTarget.GetComponent<CookingPot>().interactOffset;
                
                float collectXOffset = offset.x;
                leftOffsetPos.x -= collectXOffset;
                leftOffsetPos.z += offset.y;
                rightOffsetPos.x += collectXOffset;
                rightOffsetPos.z += offset.y;
            }
            
                
            float dis = Mathf.Min((leftOffsetPos - transform.position).magnitude, (rightOffsetPos - transform.position).magnitude);
            if (dis < collectRadius)
            {
                _pathfindingTarget = null;
                _isWithinInteractRadius = true;
                if (_isQueueingCollectAction && collectTimer < 0.001f && !_hasPlayedAttackAnim)
                {
                    _animator.CalculateAnimatorFlip(_resourceTarget.transform.position);
                    switch (_resourceTarget.type)
                    {
                        case ResourceType.Tree:
                            hideGunSpriteRequesters.Add(gameObject);
                            _animator.SetAnimatorBool("Chop Tree", true);
                            Invoke(nameof(RemoveHideGunRequester), 0.417f);
                            break;
                        case ResourceType.Bush:
                            hideGunSpriteRequesters.Add(gameObject);
                            _animator.SetAnimatorBool("Chop Grass", true);
                            Invoke(nameof(RemoveHideGunRequester), 0.483f);
                            break;
                        case ResourceType.Mineral:
                            hideGunSpriteRequesters.Add(gameObject);
                            _animator.SetAnimatorBool("Chisel Rock", true);
                            Invoke(nameof(RemoveHideGunRequester), 0.417f);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    _hasPlayedAttackAnim = true;
                    _isQueueingCollectAction = false;
                    _isWorldInteracting = false;
                    _isTriggeredByWorldInteract = true;
                }
            }
        }
    }

    private void RemoveHideGunRequester()
    {
        hideGunSpriteRequesters.Remove(gameObject);
    }
    
    #endregion

    public float GetFuel()
    {
        return _status.fuelRef.GetValue();
    }

    public float GetMaxFuel()
    {
        return _status.fuelRef.GetMaxValue();
    }
    
    public void SetFuel(float val)
    {
        _status.fuelRef.SetValue(val);
    }

    public void SetMaxFuel(float val)
    {
        _status.fuelRef.SetMaxValue(val);
    }

    #region ISavable Implementation

    public List<Tuple<string, dynamic>> SaveElement()
    {
        List<Tuple<string, dynamic>> elements = new List<Tuple<string, dynamic>>();

        elements.Add(new Tuple<string, dynamic>("v3_IBPosition", transform.position));
        elements.Add(new Tuple<string, dynamic>("b_hasTaughtDash", hasTaughtDash));
        elements.Add(new Tuple<string, dynamic>("f_IBFuel", _status.fuelRef.GetValue()));
        
        
        return elements;
    }

    public void LoadElement(SaveData saveData)
    {
        transform.position = (Vector3)saveData.saveDict["v3_IBPosition"];
        hasTaughtDash = (bool)saveData.saveDict["b_hasTaughtDash"];
        SetFuel((float)Convert.ToDouble(saveData.saveDict["f_IBFuel"]));
    }

    #endregion
    
}
