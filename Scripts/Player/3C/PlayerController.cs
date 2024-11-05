using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using UnityEngine.SceneManagement;
using DG.Tweening;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour, ICanInteract
{
    public static PlayerController Instance;

    public enum MovementState
    {
        Walking,
        Flying,
        Gliding,
        Dropping,
        Falling,
        Crashing
    }

    [Header("Player Movement")]
    [Tooltip("Character horizontal speed while in Walking state (On the ground)")]
    [SerializeField][Range(0f, 20f)] float walkingSpeed = 4f;
    [Tooltip("Number of seconds to fall before transitioning to flying")]
    [SerializeField][Range(0f, 1f)] float fallTimer = 0.2f;
    [Tooltip("Character horizontal speed while in Flying state. (In the air and has vertical input e.g. fly / descend)")]
    [SerializeField][Range(0f, 20f)] float flyingSpeed = 9f;
    [Tooltip("Character vertical speed for ascending.")]
    [SerializeField][Range(0f, 20f)] float flyingAscendSpeed = 3f;
    [Tooltip("Character vertical speed for descending.")]
    [SerializeField][Range(0f, 20f)] float flyingDescendSpeed = 3f;
    [Tooltip("Character horizontal speed while in Gliding state. (In the air and has no vertical input)")]
    [SerializeField][Range(0f, 20f)] float glidingSpeed = 6f;
    [Tooltip("Character vertical speed while in Gliding state. (Auto-descending)")]
    [SerializeField][Range(0f, 20f)] float glidingDescendSpeed = 1.5f;
    [Tooltip("Character turning senstivity while running. (in deg/s)")]
    [HideInInspector][Range(0f, 360f)] float runningTurnSenstivity = 60f;
    [Tooltip("Character vertical speed while crashing from no energy.")]
    [SerializeField][Range(0f, 20f)] float immediateCrashSpeed = 20f;
    [Tooltip("Speed while dropping before flying")]
    [SerializeField][Range(0f, 10f)] float fallSpeed = 2.6f;
    [Tooltip("Layer Mask for Ground Check. Set to Ground.")]
    [SerializeField] LayerMask groundMask;
    [Tooltip("Extra distance for ground checking")]
    [SerializeField][Range(0f, 1f)] float groundCheckExtraDistance = 0.05f;

    [Tooltip("Bird's speed recovery curve when stunned")]
    [SerializeField] AnimationCurve stunnedRecoverCurve;
    [Tooltip("How long the bird will be stunned when repelled from a non-landable area")]
    [SerializeField][Range(0f, 5f)] float stunnedTimeDuration = 1.0f;
    private bool isStunned;
    private float stunnedTimer;
    [Header("Speed Boost")]
    [Tooltip("Character boost speed while in Walking state")]
    [SerializeField][Range(0, 20f)] float boostWalkingSpeed = 10f;
    [Tooltip("Character boost speed while in Flying state")]
    [SerializeField][Range(0, 20f)] float boostFlyingSpeed = 10f;
    [Tooltip("Number of seconds to boost speed")]
    [SerializeField][Range(0, 10f)] float boostDuration = 1.5f;
    [Tooltip("Boost cooldown time")]
    [SerializeField][Range(0, 10f)] float boostCooldown = 1f;
    //[SerializeField] private Material speedLineVFX;
    [HideInInspector] public bool isBoosted = false;
    private float cooldownTime;
    private float timeBoosted = 0f;
    private bool boostIsHeld = false;

    [Header("Hitpoint")]
    [SerializeField][Range(1, 10f)] int totalHitpoint = 2;
    [SerializeField][Range(0.0f, 20.0f)] float hitpointRecoveryTime = 5.0f;
    [SerializeField][Range(0.0f, 5.0f)] float deathAnimOverrideTime = 3.0f;
    [HideInInspector] public int currentHitpoint;
    [HideInInspector] public bool isInvincible = false;
    [HideInInspector] public float hitpointRecTimer = 0.0f;
    private float crashingTimer = 0.0f;

    [HideInInspector] public CameraModeController cameraMode;

    [Header("Third Person Camera")]
    public CinemachineFreeLook thirdPersonCam;
    [SerializeField][Range(10f, 120f)] float HFOV3rdP = 80f;
    [SerializeField] Vector3 cameraOrbitRadius;
    [SerializeField] Vector3 cameraOrbitHeight;
    [SerializeField][Range(0f, 3f)] float maxZoomOffsetMultiplier = 1.5f;
    [SerializeField][Range(0f, 3f)] float minZoomOffsetMultiplier = 0.5f;
    [SerializeField][Range(0f, 20f)] float zoomSensitivity = 1.0f;
    [SerializeField][Range(0.5f, 1.0f)] float panDownViewY = 0.6f;
    [SerializeField][Range(0.0f, 1.0f)] float panDownSpeed = 0.0035f;
    private float targetZoomOffset3rdP;
    private bool panDownFlag;

    [Header("Animation")]
    public Animator animator;
    [Tooltip("Character turning around time (in seconds)")]
    [SerializeField][Range(0f, 1f)] float turnSmoothTime = 0.1f;
    [SerializeField][Range(0.0f, 2.0f)] float birdlyActionMoveFreezeTime;
    [SerializeField] private ParticleSystem sparkParticles;

    public Transform hatTransformRef;
    [HideInInspector] public Hat currentHat;

    [HideInInspector] public MovementState movementState;
    private ICanInteract.HoldingState holdingState;
    [HideInInspector]
    public event Action OnStartFlying, OnStartWalking, OnStartGliding;
    [HideInInspector] public Camera mainCam;
    private Rigidbody rb;
    [HideInInspector] public InputWrapper inputWrapper;
    private Collider boxCollider;
    private float rotCache;
    private Vector3 rawInputDir;
    private float turnSmoothVelocity;
    private float currentSpeed;
    private bool wasHoveringBeforeDeath;

    [Header("Audio")]
    private bool isCamPanSFXPlaying = false;

    [Header("Interaction")]
    [SerializeField] Interactable objectInBeak = null;
    [SerializeField] Transform beakTransform;
    private float normalWalkingSpeed;
    private float normalFlyingSpeed;
    private float normalAscendSpeed;

    public event Action<Interactable> OnInteractingWithObject;
    [HideInInspector] public int isUnderShelter = 0;
    [HideInInspector] public int isItemUnderShelter = 0;
    private bool deathAnimCalled = false;
    private bool isUsing;
    private bool holdingInteract = false;
    [HideInInspector] public bool blockingMovement = false;
    [HideInInspector] public bool blockingInteraction = false;
    [HideInInspector] public bool blockingFlight = false;
    [HideInInspector] public bool blockingCameraMode = false;
    private float orbitTopRadius, orbitMidRadius, orbitBotRadius;
    private float orbitTopHeight, orbitMidHeight, orbitBotHeight;


    [HideInInspector] public bool dialogueActive = false;
    private double interactStartTime;
    private double interactHoldTime;

    [HideInInspector] public bool isDoingBirdlyAction = false;
    [HideInInspector] public bool canDoBirdlyAction = true;
    int prevBirdlyAction = -1;

    [HideInInspector] public bool isDescending = false;

    [Header("Helicopter Tilt")]
    [Tooltip("Max degrees bird tilts forward while flying")]
    [SerializeField] float maxTiltDegrees = 10f;
    [Tooltip("Max offset on local Z axis while flying")]
    [SerializeField] float maxTiltOffset = -0.1f;
    [Tooltip("Max angles per frame that bird tilts while flying")]
    [SerializeField] float maxTiltDegreesDelta = 2f;
    [Tooltip("Max offset units per frame that bird moves while flying")]
    [SerializeField] float maxTiltOffsetDelta = 0.01f;
    [Tooltip("Length of wobble after bird stops while flying")]
    [SerializeField] float wobbleTime = 3.0f;
    private bool isHelicopterWobbling = false;
    private float helicopterWobbleTimer = 0f;
    DG.Tweening.Core.TweenerCore<Quaternion, Vector3, DG.Tweening.Plugins.Options.QuaternionOptions> tiltTween;
    DG.Tweening.Core.TweenerCore<Vector3, Vector3, DG.Tweening.Plugins.Options.VectorOptions> posTween;

    [Header("Auto Height-Adjustment")]
    [Tooltip("Max ledge that the bird can automatically be raised over. For smoothing out uneven ground traversal.")]
    [SerializeField] float maxHeightAdjustment = 0.2f;
    float currMaxGroundHeight;

    [Header("Screen Crack")]
    [SerializeField] float screenCrackTime = 0.5f;
    [SerializeField] float screenRecoverTime = 0.5f;
    private Image screenCrack1;
    private Image screenCrack2;
    private bool screenCracking = false;
    private float screenCrackTimer = 0.0f;
    private Image lastScreenCrack;
    private Image currentScreenCrack;
    private bool screenRecovering = false;
    private float screenRecoverTimer;

    // General MonoBehavior
    #region 
    private void Awake()
    {
        if (Instance != null)
        {
            //Debug.LogError("Two PlayerControllers in the scene!");
        }
        Instance = this;

        // Components cached
        mainCam = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        rb = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();
        inputWrapper = GetComponent<InputWrapper>();
        cameraMode = GetComponent<CameraModeController>();

        // Event subscribed
        OnStartFlying += HandleStartFlying;
        OnStartWalking += HandleStartWalking;
        OnStartGliding += HandleStartGliding;

        normalWalkingSpeed = walkingSpeed;
        normalFlyingSpeed = flyingSpeed;
        normalAscendSpeed = flyingAscendSpeed;

        thirdPersonCam.m_Lens.FieldOfView = HFOVtoVFOV(HFOV3rdP);

        targetZoomOffset3rdP = 1.0f;

        if (CheckIfOnTheGround())
            movementState = MovementState.Walking;
        else
            movementState = MovementState.Flying;

        // Sprinting intialized
        cooldownTime = boostCooldown;

        orbitTopRadius = cameraOrbitRadius.x;
        orbitMidRadius = cameraOrbitRadius.y;
        orbitBotRadius = cameraOrbitRadius.z;

        orbitTopHeight = cameraOrbitHeight.x;
        orbitMidHeight = cameraOrbitHeight.y;
        orbitBotHeight = cameraOrbitHeight.z;

        //Control Settings Init
        StartCoroutine((Util.ConditionalCallbackTimer(
                    () => ControlManager.Instance != null,
                    () =>
                    {
                        if (ControlManager.Instance.currControlScheme == ControlDeviceType.KeyboardAndMouse)
                        {
                            thirdPersonCam.m_YAxis.m_SpeedMode = AxisState.SpeedMode.InputValueGain;
                            thirdPersonCam.m_YAxis.m_AccelTime = 0.0f;
                            thirdPersonCam.m_XAxis.m_SpeedMode = AxisState.SpeedMode.InputValueGain;
                            thirdPersonCam.m_XAxis.m_AccelTime = 0.0f;

                            thirdPersonCam.m_YAxis.m_MaxSpeed = ControlManager.Instance.mouseSensGameplay / 100.0f;
                            thirdPersonCam.m_XAxis.m_MaxSpeed = ControlManager.Instance.mouseSensGameplay;

                        }
                        else
                        {
                            thirdPersonCam.m_YAxis.m_SpeedMode = AxisState.SpeedMode.MaxSpeed;
                            thirdPersonCam.m_YAxis.m_AccelTime = ControlManager.Instance.joystickSensAccelTime;
                            thirdPersonCam.m_XAxis.m_SpeedMode = AxisState.SpeedMode.MaxSpeed;
                            thirdPersonCam.m_XAxis.m_AccelTime = ControlManager.Instance.joystickSensAccelTime;
                            PlayerController.Instance.thirdPersonCam.m_YAxis.m_MaxSpeed = ControlManager.Instance.joystickSensGameplay;
                            PlayerController.Instance.thirdPersonCam.m_XAxis.m_MaxSpeed = ControlManager.Instance.joystickSensGameplay * 100.0f;
                        }
                        PlayerController.Instance.thirdPersonCam.m_XAxis.m_InvertInput = ControlManager.Instance.InvertXAxis;
                        PlayerController.Instance.thirdPersonCam.m_YAxis.m_InvertInput = !ControlManager.Instance.InvertYAxis;
                    }
                )));


        // Death Animation intialized
        // Do it here since object is not DontDestroyOnLoad
        animator.ResetTrigger("endDie");
        animator.SetTrigger("endDie");

        crashingTimer = 0.0f;

        //event subscriptions
        GameEventInfra.Subscribe<ReloadEvent>(OnSceneReload);

        currentHitpoint = totalHitpoint;

        isBoosted = false;
        boostIsHeld = false;
        deathAnimCalled = false;
        blockingMovement = false;
        blockingFlight = false;
        blockingInteraction = false;
        blockingCameraMode = false;
        dialogueActive = false;
        isDoingBirdlyAction = false;
        canDoBirdlyAction = true;
        isDescending = false;
        holdingInteract = false;
        isHelicopterWobbling = false;

        helicopterWobbleTimer = 0f;

        prevBirdlyAction = -1;
        animator.SetInteger("BirdlyAction", -1);

        screenCrack1 = this.gameObject.transform.GetChild(9).GetChild(0).gameObject.GetComponent<Image>();
        screenCrack1.gameObject.SetActive(true);
        screenCrack1.color = new Color(screenCrack1.color.r, screenCrack1.color.g, screenCrack1.color.b, 0.0f);
        screenCrack2 = this.gameObject.transform.GetChild(9).GetChild(1).gameObject.GetComponent<Image>();
        screenCrack2.gameObject.SetActive(true);
        screenCrack2.color = new Color(screenCrack2.color.r, screenCrack2.color.g, screenCrack2.color.b, 0.0f);
        lastScreenCrack = screenCrack2;
        screenRecoverTimer = screenRecoverTime;
    }

    private void OnEnable()
    {
        StartCoroutine((Util.ConditionalCallbackTimer(
            () => ControlManager.Instance != null,
            () => ControlManager.Instance.OnControlSchemeChanges += ControlSchemeChangesEventHandler
        )));

        StartCoroutine((Util.ConditionalCallbackTimer(
            () => inputWrapper.inputMaster != null,
            () =>
            {
                inputWrapper.inputMaster.Gameplay.Interact.started += OnInteractInput;
                inputWrapper.inputMaster.Gameplay.Interact.performed += OnInteractInput;
                inputWrapper.inputMaster.Gameplay.Interact.canceled += OnInteractInput;

                inputWrapper.inputMaster.Gameplay.BirdlyActions.performed += OnBirdlyActionInput;

                // inputWrapper.inputMaster.Gameplay.BoostTap.started += OnBoostInputTap;
                // inputWrapper.inputMaster.Gameplay.BoostTap.performed += OnBoostInputTap;
                // inputWrapper.inputMaster.Gameplay.BoostTap.canceled += OnBoostInputTap;

                // inputWrapper.inputMaster.Gameplay.BoostHold.started += OnBoostInputHold;
                // inputWrapper.inputMaster.Gameplay.BoostHold.performed += OnBoostInputHold;
                // inputWrapper.inputMaster.Gameplay.BoostHold.canceled += OnBoostInputHold;

                inputWrapper.inputMaster.Gameplay.CameraModeEnableDisable.performed += OnCameraModeInput;
                inputWrapper.inputMaster.CameraMode.CameraModeEnableDisable.performed += OnCameraModeEnableDisablePerformed;
                inputWrapper.inputMaster.CameraMode.CameraModeScreenshot.performed += OnCameraModeScreenshotPerformed;
                inputWrapper.inputMaster.CameraMode.CameraModeReset.performed += OnCameraModeResetPerformed;
            })));
    }

    private void OnDisable()
    {
        StartCoroutine((Util.ConditionalCallbackTimer(
           () => ControlManager.Instance != null,
           () => ControlManager.Instance.OnControlSchemeChanges -= ControlSchemeChangesEventHandler
       )));

        inputWrapper.inputMaster.Gameplay.Interact.started -= OnInteractInput;
        inputWrapper.inputMaster.Gameplay.Interact.performed -= OnInteractInput;
        inputWrapper.inputMaster.Gameplay.Interact.canceled -= OnInteractInput;

        inputWrapper.inputMaster.Gameplay.BirdlyActions.performed -= OnBirdlyActionInput;

        // inputWrapper.inputMaster.Gameplay.BoostTap.started -= OnBoostInputTap;
        // inputWrapper.inputMaster.Gameplay.BoostTap.performed -= OnBoostInputTap;
        // inputWrapper.inputMaster.Gameplay.BoostTap.canceled -= OnBoostInputTap;

        // inputWrapper.inputMaster.Gameplay.BoostHold.started -= OnBoostInputHold;
        // inputWrapper.inputMaster.Gameplay.BoostHold.performed -= OnBoostInputHold;
        // inputWrapper.inputMaster.Gameplay.BoostHold.canceled -= OnBoostInputHold;

        inputWrapper.inputMaster.Gameplay.CameraModeEnableDisable.performed -= OnCameraModeInput;
        inputWrapper.inputMaster.CameraMode.CameraModeEnableDisable.performed -= OnCameraModeInput;
        inputWrapper.inputMaster.CameraMode.CameraModeScreenshot.performed -= OnCameraModeScreenshotInput;
        inputWrapper.inputMaster.CameraMode.CameraModeReset.performed -= OnCameraModeResetInput;
        inputWrapper.inputMaster.CameraMode.CameraModeEnableDisable.performed -= OnCameraModeEnableDisablePerformed;
        inputWrapper.inputMaster.CameraMode.CameraModeScreenshot.performed -= OnCameraModeScreenshotPerformed;
        inputWrapper.inputMaster.CameraMode.CameraModeReset.performed -= OnCameraModeResetPerformed;
    }

    private void Update()
    {
        // Check if vec(bird->cam) collides with hiding spots (bushes, trees)
        {
            Vector3 v = GetComponent<Collider>().bounds.center - mainCam.transform.position;
            // raycast from bird -> camera so only need to do collision check on bird, not camera
            // Check OnTriggerStay for the other part of calling doFade
            RaycastHit[] hits = Physics.RaycastAll(GetComponent<Collider>().bounds.center, -v, v.magnitude, LayerMask.GetMask("NavMeshIgnore"));
            if (hits.Length != 0)
            {
                foreach (var h in hits)
                {
                    if (h.transform.TryGetComponent<SafeZone>(out SafeZone s))
                    {
                        s.doFade = true;
                    }
                }
            }
        }

        if (isStunned)
        {
            stunnedTimer += Time.deltaTime;
            if (stunnedTimer >= stunnedTimeDuration)
            {
                stunnedTimer = 0.0f;
                isStunned = false;
            }
        }

        if (screenCracking)
        {
            screenCrackTimer += Time.deltaTime;
            if (screenCrackTimer > screenCrackTime)
            {
                screenCrackTimer = screenCrackTime;
            }
            currentScreenCrack.color = new Color(currentScreenCrack.color.r, currentScreenCrack.color.g, currentScreenCrack.color.b, screenCrackTimer / screenCrackTime);

            if (screenCrackTimer == screenCrackTime)
            {
                screenCracking = false;
                lastScreenCrack = currentScreenCrack;
                currentScreenCrack = null;
            }
        }

        if (screenRecovering)
        {
            screenRecoverTimer -= Time.deltaTime;
            if (screenRecoverTimer < 0.0f)
            {
                screenRecoverTimer = 0.0f;
            }
            lastScreenCrack.color = new Color(lastScreenCrack.color.r, lastScreenCrack.color.g, lastScreenCrack.color.b, screenRecoverTimer / screenRecoverTime);

            if (screenRecoverTimer == 0.0f)
            {
                screenRecovering = false;
            }
        }

        //rawInputDir = Vector3.zero;
        //Block movements when in camera mode
        if (!cameraMode.isCameraModeEnabled)
        {
            PlayerSounds.Instance.StopCameraPanSound();
            isCamPanSFXPlaying = false;
            rawInputDir = inputWrapper.rawMoveDir;
            // Camera Zoom Input
            float val = inputWrapper.rawZoomVal;
            val = Mathf.Clamp(val, -1.0f, 1.0f);
            targetZoomOffset3rdP += val * Time.deltaTime;
            targetZoomOffset3rdP = Mathf.Clamp(targetZoomOffset3rdP, minZoomOffsetMultiplier, maxZoomOffsetMultiplier);

            if (movementState == MovementState.Flying && inputWrapper.rawGameplayLookDir.magnitude != 0)
                panDownFlag = false;

            if (movementState == MovementState.Flying && panDownFlag)
                PanDownCamera();
        }
        else
        {
            float val = inputWrapper.rawZoomVal;
            val = Mathf.Clamp(val, -1.0f, 1.0f);
            cameraMode.UpdateFOV(val);
            float camLookLen = inputWrapper.rawCameraModeLookDir.magnitude;
            //camLookDir.magnitude
            if (!dialogueActive && !isCamPanSFXPlaying && camLookLen != 0.0f)
            {
                PlayerSounds.Instance.PlayCameraPanSound();
                isCamPanSFXPlaying = true;
            }
            else if (isCamPanSFXPlaying && camLookLen == 0.0f)
            {
                PlayerSounds.Instance.StopCameraPanSound();
                isCamPanSFXPlaying = false;
            }
        }

        ProcessHitpointRecovery();

        //Update animation blend tree
        if (movementState == MovementState.Walking)
            animator.SetFloat("speed", Mathf.Sqrt(rb.velocity.magnitude));
        else
            animator.SetFloat("speed", Mathf.Pow(rb.velocity.magnitude, 0.333f));
        ZoomCamera();

        if (holdingInteract)
        {
            interactHoldTime = Time.time - interactStartTime;
            if (interactHoldTime > 0.4)
            {
                // Key-Held Behavior
                holdingInteract = false;
                isUsing = true;
                Interact();
            }
        }

        // for debugging Respawn() related functionality
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.P)) Respawn();
        if (Input.GetKeyDown(KeyCode.O)) Hit();
        if (Input.GetKeyDown(KeyCode.T))
        {
            animator.SetTrigger("hit");
        }
#endif

        //debug mode capabilities
        if (DebugModeManager.Instance && DebugModeManager.Instance.isDebugMode)
        {
            if (Input.GetKeyDown(KeyCode.I)) //toggle on/ off mesh
            {
                animator.gameObject.SetActive(!animator.gameObject.activeSelf);
                gameObject.transform.GetChild(5).gameObject.SetActive(!gameObject.transform.GetChild(5).gameObject.activeSelf);
            }
        }
    }

    private void FixedUpdate()
    {
        currMaxGroundHeight = 0f;

        //If no movement key pressed, stop
        rb.velocity = Vector3.zero;
        if (movementState == MovementState.Walking)
        {
            rawInputDir.y = Mathf.Max(rawInputDir.y, 0f);
        }

        //Also check to prevent small drifting on key release
        if (rawInputDir.magnitude > Mathf.Epsilon && !blockingMovement && !dialogueActive)
            ProcessMovement();

        // If no vertical input and in gliding state OR
        // not having interactable selected, enforce auto descend
        if (Mathf.Approximately(rawInputDir.y, 0f) && movementState == MovementState.Gliding && !(InteractableManager.Instance.GetSelectedObject()) && !blockingMovement && !dialogueActive)
            rb.velocity += Vector3.down * glidingDescendSpeed;

        if (movementState == MovementState.Crashing)
        {
            rb.velocity = Vector3.down * immediateCrashSpeed;
            crashingTimer += Time.fixedDeltaTime;
        }
        if (crashingTimer >= deathAnimOverrideTime && !deathAnimCalled)
        {
            StartCoroutine(DeathAnimation());
        }
        if (movementState == MovementState.Falling)
            rb.velocity += Vector3.down * fallSpeed;

        if (CheckIfOnTheGround())
        {
            if (movementState == MovementState.Crashing && !deathAnimCalled)
            {
                if (wasHoveringBeforeDeath)
                {
                    PlayerSounds.Instance.PlayExplosionHoverSound();
                    wasHoveringBeforeDeath = false;
                }
                else PlayerSounds.Instance.PlayExplosionSound();
                StartCoroutine(DeathAnimation());
            }
            else if (movementState != MovementState.Walking)
                OnStartWalking();
            else if (movementState == MovementState.Walking)
            {
                //determine if bird should be auto-raised above a ledge
                if (currMaxGroundHeight > 0.001f && currMaxGroundHeight < maxHeightAdjustment)
                {
                    rb.MovePosition(new Vector3(rb.position.x, rb.position.y + currMaxGroundHeight, rb.position.z));
                }
            }
        }
        else
        {
            if (movementState == MovementState.Walking)
            {
                if (Mathf.Abs(rawInputDir.y) >= float.Epsilon)
                {
                    OnStartFlying();
                }
                else StartCoroutine(AllowFallBeforeFlying());
            }
            if (movementState == MovementState.Flying && Mathf.Abs(rawInputDir.y) <= float.Epsilon)
                OnStartGliding();
            if (movementState == MovementState.Gliding && Mathf.Abs(rawInputDir.y) >= float.Epsilon)
                OnStartFlying();
            if (movementState == MovementState.Dropping && Mathf.Abs(rawInputDir.y) >= float.Epsilon)
                OnStartFlying();
            // if (movementState == MovementState.Hovering && Mathf.Abs(rawInputDir.y) >= float.Epsilon)
            //     OnStartFlying();

        }
        BoostSpeed();
        ProcessHelicopterTilt();
    }

    #endregion

    //Physics Event Handlers
    #region 
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag != "Ground" && collision.gameObject.tag != "BoundaryWall")
        {
            PlayerSounds.Instance.PlayThumpSound();
        }
    }

    private void OnTriggerEnter(Collider collider)
    {
        if (collider.gameObject.tag == "Water")
        {
            if (sparkParticles != null)
            {
                sparkParticles.Play();
                var main = sparkParticles.main;
                main.loop = true;
            }
        }
    }

    //Continuous calling to reset the value
    private void OnTriggerStay(Collider other)
    {
        if (other.transform.TryGetComponent<SafeZone>(out SafeZone s))
        {
            s.doFade = true;
        }
    }

    private void OnTriggerExit(Collider collider)
    {
        if (collider.gameObject.tag == "Water")
        {
            if (sparkParticles != null)
            {
                sparkParticles.Stop();
                var main = sparkParticles.main;
                main.loop = false;
            }
        }
    }
    #endregion

    //Input Event Handlers
    #region 

    public void OnInteractInput(InputAction.CallbackContext context)
    {
        if (dialogueActive)
            return;

        if (context.started)
        {
            holdingInteract = true;
            isUsing = false;
            interactStartTime = Time.time;

        }

        else if (context.canceled && !isUsing)
        {
            // Key-Tapped Behavior
            holdingInteract = false;
            isUsing = false;
            Interact();

        }

        else if (context.canceled && isUsing)
        {
            holdingInteract = false;
            isUsing = false;
        }
    }

    public void OnBirdlyActionInput(InputAction.CallbackContext context)
    {
        if (dialogueActive)
            return;

        if (context.performed)
        {
            if (canDoBirdlyAction && movementState == MovementState.Walking)
            {
                StartCoroutine(Peck());
            }
        }
    }

    public void OnBoostInputTap(InputAction.CallbackContext context)
    {
        if (dialogueActive)
            return;

        if (context.performed && !isBoosted)
        {
            if (!isBoosted && boostCooldown <= 0)
            {
                isBoosted = true;
            }
        }
    }

    public void OnBoostInputHold(InputAction.CallbackContext context)
    {
        if (dialogueActive)
            return;

        if (context.performed && !isBoosted)
        {
            isBoosted = true;
            boostIsHeld = true;
        }
        else
        {
            isBoosted = false;
            boostIsHeld = false;
        }
    }


    public void ControlSchemeChangesEventHandler(ControlDeviceType controlDeviceType)
    {
        if (controlDeviceType == ControlDeviceType.KeyboardAndMouse)
        {
            thirdPersonCam.m_YAxis.m_SpeedMode = AxisState.SpeedMode.InputValueGain;
            thirdPersonCam.m_YAxis.m_AccelTime = 0.0f;
            thirdPersonCam.m_YAxis.m_MaxSpeed = ControlManager.Instance.mouseSensGameplay / 100.0f;

            thirdPersonCam.m_XAxis.m_SpeedMode = AxisState.SpeedMode.InputValueGain;
            thirdPersonCam.m_XAxis.m_AccelTime = 0.0f;
            thirdPersonCam.m_XAxis.m_MaxSpeed = ControlManager.Instance.mouseSensGameplay;
            cameraMode.UpdatePanningSpeed(ControlManager.Instance.mouseSensCameraMode, ControlDeviceType.KeyboardAndMouse);
        }
        else
        {
            thirdPersonCam.m_YAxis.m_SpeedMode = AxisState.SpeedMode.MaxSpeed;
            thirdPersonCam.m_YAxis.m_AccelTime = ControlManager.Instance.joystickSensAccelTime;
            thirdPersonCam.m_YAxis.m_MaxSpeed = ControlManager.Instance.joystickSensGameplay;

            thirdPersonCam.m_XAxis.m_SpeedMode = AxisState.SpeedMode.MaxSpeed;
            thirdPersonCam.m_XAxis.m_AccelTime = ControlManager.Instance.joystickSensAccelTime;
            thirdPersonCam.m_XAxis.m_MaxSpeed = ControlManager.Instance.joystickSensGameplay * 100.0f;
            cameraMode.UpdatePanningSpeed(ControlManager.Instance.joystickSensCameraMode, ControlDeviceType.Xbox);
        }
    }

    public void OnCameraModeInput(InputAction.CallbackContext context)
    {
        /*
            When entering/exiting camera mode, there is a blend time betweeen the two cameras
            Which might get stuck itself, if there is a dialogue playing in that time
            Moved this dialogueActive check to ProcessCameraMode(), and only do the check for entering camera mode
            So the player can still exit the camera mode if the dialogue is active
        */

        if (context.performed)
            StartCoroutine(cameraMode.ProcessCameraMode());
    }

    public void OnCameraModeScreenshotInput(InputAction.CallbackContext context)
    {
        if (dialogueActive)
            return;

        if (context.performed)
            StartCoroutine(cameraMode.ProcessScreenshot());
    }

    public void OnCameraModeResetInput(InputAction.CallbackContext context)
    {
        if (dialogueActive)
            return;

        if (context.performed)
            cameraMode.ResetCamRotation();
    }

    // previously lambda functions - turned them into actual functions so they can be unsubscribed
    void OnCameraModeEnableDisablePerformed(InputAction.CallbackContext context) { if (ControlManager.Instance.GetCurrentActionMap().Contains("Camera Mode")) OnCameraModeInput(context); }
    void OnCameraModeScreenshotPerformed(InputAction.CallbackContext context) { if (ControlManager.Instance.GetCurrentActionMap().Contains("Camera Mode")) OnCameraModeScreenshotInput(context); }
    void OnCameraModeResetPerformed(InputAction.CallbackContext context) { if (ControlManager.Instance.GetCurrentActionMap().Contains("Camera Mode")) OnCameraModeResetInput(context); }

    #endregion

    // Movement Logics
    #region 
    private void ProcessMovement()
    {
        if (isStunned)
            currentSpeed *= stunnedRecoverCurve.Evaluate(stunnedTimer / stunnedTimeDuration);

        float targetAngle;
        float joystickMultiplier = new Vector2(rawInputDir.x, rawInputDir.z).magnitude;
        if (Mathf.Approximately(rawInputDir.x, 0f) && Mathf.Approximately(rawInputDir.z, 0f))
            targetAngle = rotCache;
        // else if (isBoosted)
        // {
        //     //TODO bird tend to turn left when resolving this angle thing
        //     targetAngle = Mathf.Atan2(rawInputDir.x, rawInputDir.z) * Mathf.Rad2Deg + mainCam.transform.eulerAngles.y;
        //     float diff = targetAngle - rotCache;
        //     diff = (diff + 180.0f) % 360.0f - 180.0f;
        //     targetAngle = rotCache + Mathf.Min(diff, diff > 0 ? runningTurnSenstivity : -runningTurnSenstivity) * Time.fixedDeltaTime;
        //     rotCache = targetAngle % 360.0f;
        // }
        else
            targetAngle = rotCache = Mathf.Atan2(rawInputDir.x, rawInputDir.z) * Mathf.Rad2Deg + mainCam.transform.eulerAngles.y;

        //Smooth rotation
        float dampAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
        transform.rotation = Quaternion.Euler(0f, dampAngle, 0f);

        //Calculate move direction
        Vector3 horMoveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward * new Vector3(rawInputDir.x, 0f, rawInputDir.z).magnitude;
        horMoveDir = horMoveDir.normalized;
        Vector3 verMoveDir = Vector3.up * rawInputDir.y;
        isDescending = verMoveDir.y >= 0 ? false : true;
        if (blockingFlight) verMoveDir = Vector3.zero;

        float currentVertSpeed = verMoveDir.y > 0 ? verMoveDir.y * flyingAscendSpeed : verMoveDir.y * flyingDescendSpeed;
        if (isStunned)
            currentVertSpeed *= stunnedRecoverCurve.Evaluate(stunnedTimer / stunnedTimeDuration);

        rb.velocity = new Vector3(horMoveDir.x * currentSpeed * joystickMultiplier, currentVertSpeed, horMoveDir.z * currentSpeed * joystickMultiplier);
    }

    private void ProcessHitpointRecovery()
    {
        if (currentHitpoint < totalHitpoint)
        {
            hitpointRecTimer += Time.deltaTime;
            if (hitpointRecTimer >= hitpointRecoveryTime)
            {
                currentHitpoint++;
                hitpointRecTimer = 0.0f;

                // screen recovery
                screenRecovering = true;
            }
        }
    }

    void ProcessHelicopterTilt()
    {
        if (movementState != MovementState.Walking)
        {
            if (helicopterWobbleTimer > 0f) helicopterWobbleTimer -= Time.deltaTime;

            Vector2 horSpeed = new Vector2(rb.velocity.x, rb.velocity.z);

            // WOBBLE
            if (isHelicopterWobbling)
            {
                if (horSpeed.magnitude < 0.1f) return;

                // if wobble tween is over OR the player has started moving again, reset the helicopter wobble state
                if (horSpeed.magnitude >= 0.1f || helicopterWobbleTimer <= 0f)
                {
                    StopHelicopterWobble();
                }
            }

            // TILT
            float prevRotation = animator.gameObject.transform.rotation.eulerAngles.x;
            float lerp = horSpeed.magnitude / currentSpeed;
            Quaternion targetTiltQuat = Quaternion.Euler(maxTiltDegrees * lerp, 0f, 0f);

            //slerp to a forward tilt based on speed
            Quaternion finalRotation = Quaternion.RotateTowards(animator.gameObject.transform.localRotation, targetTiltQuat, maxTiltDegreesDelta);
            animator.gameObject.transform.localRotation = finalRotation;

            //translate to a nicer looking offset position
            float transZ = Mathf.Clamp(maxTiltOffset - animator.gameObject.transform.localPosition.z, -maxTiltOffsetDelta, maxTiltOffsetDelta);
            animator.gameObject.transform.Translate(0f, 0f, transZ, Space.Self);
            Vector3 localPos = animator.gameObject.transform.localPosition;
            localPos.x = 0f;
            localPos.y = 0f;
            animator.gameObject.transform.localPosition = localPos;

            //if stop is abrupt enough, tween a wobble
            if (prevRotation >= (maxTiltDegrees - 3f) && targetTiltQuat.eulerAngles.x <= 0.1f && !isHelicopterWobbling)
            {
                tiltTween = animator.gameObject.transform.DOLocalRotate(Vector3.zero, wobbleTime).SetEase(Ease.OutElastic);
                posTween = animator.gameObject.transform.DOLocalMoveZ(0f, wobbleTime).SetEase(Ease.OutElastic);

                helicopterWobbleTimer = wobbleTime;
                isHelicopterWobbling = true;
            }
        }
        // if bird is walking, always return to zero rotation and position offset
        else if (animator.gameObject.transform.rotation.eulerAngles.x > 0f)
        {
            StopHelicopterWobble();

            //slerp to no tilt
            Quaternion finalRotation = Quaternion.RotateTowards(animator.gameObject.transform.localRotation, Quaternion.identity, maxTiltDegreesDelta);
            animator.gameObject.transform.localRotation = finalRotation;

            //translate to no offset
            float transZ = Mathf.Clamp(-animator.gameObject.transform.localPosition.z, -maxTiltOffsetDelta, maxTiltOffsetDelta);
            animator.gameObject.transform.Translate(0f, 0f, transZ, Space.Self);
            Vector3 localPos = animator.gameObject.transform.localPosition;
            localPos.x = 0f;
            localPos.y = 0f;
            animator.gameObject.transform.localPosition = localPos;
        }
    }

    public void StopHelicopterWobble()
    {
        if (tiltTween != null) tiltTween.Kill();
        if (posTween != null) posTween.Kill();

        isHelicopterWobbling = false;
        helicopterWobbleTimer = 0f;
    }

    private void HandleStartFlying()
    {
        if (blockingFlight) return;

        PlayerSounds.Instance.StopLastSound();
        PlayerSounds.Instance.PlayPropellerLoopSound();
        movementState = MovementState.Flying;
        currentSpeed = flyingSpeed;
        if (isBoosted)
        {
            //currentSpeed += boostFlyingSpeed;
            //animator.speed = 2.0f;
        }
        else
        {
            animator.speed = 1.0f;
        }
        animator.ResetTrigger("walk");
        animator.ResetTrigger("fly");
        animator.SetTrigger("fly");
    }
    private void HandleStartWalking()
    {
        PlayerSounds.Instance.StopLastSound();
        movementState = MovementState.Walking;
        currentSpeed = walkingSpeed;
        panDownFlag = true;
        if (isBoosted)
        {
            currentSpeed += boostWalkingSpeed;
            //animator.speed = 2.0f;
        }
        else
        {
            animator.speed = 1.0f;
        }

        animator.ResetTrigger("walk");
        animator.ResetTrigger("fly");
        animator.SetTrigger("walk");
    }

    private void HandleStartGliding()
    {
        PlayerSounds.Instance.StopLastSound();
        PlayerSounds.Instance.PlayPropellerLoopSound();

        movementState = MovementState.Gliding;
        currentSpeed = glidingSpeed;
        if (isBoosted)
        {
            //currentSpeed += boostFlyingSpeed;
            //animator.speed = 2.0f;
        }
        else
        {
            animator.speed = 1.0f;
        }
    }

    private void HandleStartDropping()
    {
        PlayerSounds.Instance.StopLastSound();
        PlayerSounds.Instance.PlayPropellerLoopSound();

        movementState = MovementState.Dropping;
        isBoosted = false;
        timeBoosted = 0f;
        //animator.ResetTrigger("fall");
        //animator.SetTrigger("fall");
    }
    private void PanDownCamera()
    {
        if (thirdPersonCam.m_YAxis.Value != panDownViewY)
        {
            thirdPersonCam.m_YAxis.Value = Mathf.MoveTowards(thirdPersonCam.m_YAxis.Value, panDownViewY, panDownSpeed);
        }
    }
    private void ZoomCamera()
    {
        if (cameraMode.isCameraModeEnabled)
        {
            cameraMode.ZoomCamera();
        }
        else
        {
            thirdPersonCam.m_Orbits[0].m_Radius = Mathf.MoveTowards(thirdPersonCam.m_Orbits[0].m_Radius, targetZoomOffset3rdP * orbitTopRadius, Time.deltaTime);
            thirdPersonCam.m_Orbits[1].m_Radius = Mathf.MoveTowards(thirdPersonCam.m_Orbits[1].m_Radius, targetZoomOffset3rdP * orbitMidRadius, Time.deltaTime);
            thirdPersonCam.m_Orbits[2].m_Radius = Mathf.MoveTowards(thirdPersonCam.m_Orbits[2].m_Radius, targetZoomOffset3rdP * orbitBotRadius, Time.deltaTime);

            thirdPersonCam.m_Orbits[0].m_Height = Mathf.MoveTowards(thirdPersonCam.m_Orbits[0].m_Height, targetZoomOffset3rdP * orbitTopHeight, Time.deltaTime);
            thirdPersonCam.m_Orbits[1].m_Height = Mathf.MoveTowards(thirdPersonCam.m_Orbits[1].m_Height, targetZoomOffset3rdP * orbitMidHeight, Time.deltaTime);
            thirdPersonCam.m_Orbits[2].m_Height = Mathf.MoveTowards(thirdPersonCam.m_Orbits[2].m_Height, targetZoomOffset3rdP * orbitBotHeight, Time.deltaTime);
        }


    }
    private void BoostSpeed()
    {
        if (isBoosted)
        {
            // Color nC = speedLineVFX.GetColor("_Colour");
            // nC.a = 1.0f;
            // speedLineVFX.SetColor("_Colour", nC);
            if (movementState == MovementState.Walking && currentSpeed != walkingSpeed + boostWalkingSpeed) currentSpeed = walkingSpeed + boostWalkingSpeed;
            if (movementState == MovementState.Flying && currentSpeed != flyingSpeed + boostFlyingSpeed) currentSpeed = flyingSpeed + boostFlyingSpeed;
            if (movementState == MovementState.Gliding && currentSpeed != glidingSpeed + boostFlyingSpeed) currentSpeed = glidingSpeed + boostFlyingSpeed;

            if (boostCooldown <= 0f && !boostIsHeld)
            {
                timeBoosted += Time.deltaTime;
                if (timeBoosted >= boostDuration)
                {
                    boostCooldown = 1f;
                    timeBoosted = 0f;
                    isBoosted = false;
                }
            }
        }
        else
        {
            // Color nC = speedLineVFX.GetColor("_Colour");
            // nC.a = 0.0f;
            // speedLineVFX.SetColor("_Colour", nC);

            if (boostCooldown < 0f)
            {
                boostCooldown = 0f;
            }
            else if (boostCooldown > 0)
            {
                boostCooldown -= Time.deltaTime;
            }
            if (movementState == MovementState.Walking && currentSpeed != walkingSpeed) currentSpeed = walkingSpeed;
            if (movementState == MovementState.Flying && currentSpeed != flyingSpeed) currentSpeed = flyingSpeed;
            if (movementState == MovementState.Gliding && currentSpeed != glidingSpeed) currentSpeed = glidingSpeed;
        }

    }

    public void Hit()
    {
        if (isInvincible)
            return;

        currentHitpoint--;

        if (currentHitpoint <= 0)
        {
            Crash();
        }
        else
        {
            animator.SetTrigger("hit");
        }
        hitpointRecTimer = 0.0f;

        StartCoroutine(ControlManager.Instance.RumblePulse(0.2f, 0.2f, 0.1f));

        // screen cracks
        screenCracking = true;
        if (lastScreenCrack == screenCrack1)
        {
            currentScreenCrack = screenCrack2;
        }
        else if (lastScreenCrack == screenCrack2)
        {
            currentScreenCrack = screenCrack1;
        }
    }

    public void Repel(Vector3 forceDir, float forcePower)
    {
        //Hit and emit sparks
        Hit();
        sparkParticles?.Play();

        // if crashed from Hit(), dont add more force
        if (movementState == MovementState.Crashing)
            return;

        // TODO: ease out the velocity change?
        rb.AddForce(forceDir.normalized * forcePower, ForceMode.Impulse);
        isStunned = true;
    }

    public void Crash()
    {
        movementState = MovementState.Crashing;
        blockingMovement = true;
    }

    public void Respawn(bool shouldReloadNPCScene = true)
    {
        // AkSoundEngine.StopAll();
        PlayerSounds.Instance.StopLastSound();

        GameObject spawnPoint = GameManager.Instance.GetClosestSpawnPoint(transform.position);
        gameObject.transform.position = spawnPoint.transform.position;
        gameObject.transform.rotation = spawnPoint.transform.rotation;
        GetComponent<PoopController>().ResetPoop();
        if (!CheckIfOnTheGround())
            OnStartFlying();
        else
            OnStartWalking();

        // Resetting bird
        GameEventInfra.Publish<ReloadEvent>(new ReloadEvent());

        Awake();

        // reload scene
        if (shouldReloadNPCScene)
            GameManager.Instance.ReloadNPCScene();

        // end death animation
        // moved to awake()
    }

    //called right before interactable scene reloads
    private void OnSceneReload(ReloadEvent _input)
    {
        // drop object
        if (objectInBeak != null)
        {
            var tempObject = objectInBeak;
            objectInBeak.Interact(GetComponent<ICanInteract>());

            // move to original pos
            var interactableScene = GameManager.Instance.GetInteractableScene();
            if (interactableScene.isLoaded) SceneManager.MoveGameObjectToScene(tempObject.gameObject, interactableScene);
            tempObject.transform.position = tempObject.GetInitialPosition();
            tempObject.transform.rotation = tempObject.GetInitialRotation();

            tempObject.ResetPlayerTriggerState();
        }
    }

    private IEnumerator AllowFallBeforeFlying()
    {
        float timer = fallTimer;
        movementState = MovementState.Falling;
        while (timer > 0)
        {
            timer -= Time.deltaTime;
            if (CheckIfOnTheGround())
            {
                movementState = MovementState.Walking;
                yield break;
            }
            yield return null;
        }
        if (!CheckIfOnTheGround()) OnStartFlying();
    }

    private IEnumerator DeathAnimation()
    {
        deathAnimCalled = true;
        animator.SetTrigger("die");
        Vector2 viewportPos = Camera.main.WorldToViewportPoint(transform.position);
        GameEventInfra.Publish<LooneyTunesTransitionEvent>(new LooneyTunesTransitionEvent(2f, 1f, 1f, viewportPos));
        yield return new WaitForSeconds(3.5f);
        animator.ResetTrigger("die");
        Respawn();
    }
    #endregion

    // Helper
    #region 
    private bool CheckIfOnTheGround()
    {

        Vector3[] points = new Vector3[4] { boxCollider.bounds.center, boxCollider.bounds.center, boxCollider.bounds.center, boxCollider.bounds.center };
        points[0].x += boxCollider.bounds.extents.x;
        points[0].z += boxCollider.bounds.extents.z;

        points[1].x += boxCollider.bounds.extents.x;
        points[1].z -= boxCollider.bounds.extents.z;

        points[2].x -= boxCollider.bounds.extents.x;
        points[2].z += boxCollider.bounds.extents.z;

        points[3].x -= boxCollider.bounds.extents.x;
        points[3].z -= boxCollider.bounds.extents.z;

        bool result = false;
        //Color debugColor;
        foreach (var point in points)
        {
            bool tmp;
            RaycastHit hitInfo;
            tmp = Physics.Raycast(point, Vector3.down, out hitInfo, boxCollider.bounds.extents.y + groundCheckExtraDistance, groundMask, QueryTriggerInteraction.Ignore);
            if (tmp)
            {
                result = true;

                currMaxGroundHeight = Mathf.Max(currMaxGroundHeight, hitInfo.point.y - rb.position.y); //store height of contact point for auto-raising the bird
            }
            // debugColor = tmp ? Color.red : Color.green;
            // Debug.DrawRay(point, Vector3.down * (boxCollider.bounds.extents.y + groundCheckExtraDistance), debugColor);
        }
        return result;
    }

    private float HFOVtoVFOV(float HFOV)
    {
        return 2.0f * Mathf.Atan((Mathf.Tan(HFOV * 0.5f * Mathf.Deg2Rad)) * (1.0f / mainCam.aspect)) * Mathf.Rad2Deg;
    }

    public void SetFlyingSpeed(float speed)
    {
        flyingSpeed = speed;
        if (movementState == MovementState.Flying) currentSpeed = speed;
        //if (isBoosted) currentSpeed += boostFlyingSpeed;
    }

    public void SetWalkingSpeed(float speed)
    {
        walkingSpeed = speed;
        if (movementState == MovementState.Walking) currentSpeed = speed;
        if (isBoosted) currentSpeed += boostWalkingSpeed;
    }

    public float GetFlyingSpeed()
    {
        return flyingSpeed;
    }

    public float GetWalkingSpeed()
    {
        return walkingSpeed;
    }

    #endregion

    //ICanInteract implementation
    #region
    public ICanInteract.HoldingState holdState => holdingState;
    public Interactable holdingObject { get => objectInBeak; set => objectInBeak = value; }
    public Transform holdingObjectTransform { get => beakTransform; }
    public bool affectedByPhysics => true;
    public bool showOutline => true;
    public float walkSpeed { get => walkingSpeed; set => walkingSpeed = value; }
    public float flySpeed { get => flyingAscendSpeed; set => flyingAscendSpeed = value; }

    public void ChangeHoldingState(ICanInteract.HoldingState newHoldingState) => holdingState = newHoldingState;
    public void SetSpeedToDefault()
    {
        walkingSpeed = normalWalkingSpeed;
        flyingAscendSpeed = normalAscendSpeed;
        if (movementState == MovementState.Walking) currentSpeed = normalWalkingSpeed;
        else if (movementState == MovementState.Flying || movementState == MovementState.Gliding) flyingAscendSpeed = normalAscendSpeed;
    }
    public void Interact()
    {
        if (!dialogueActive && !blockingInteraction)
        {
            if (InteractableManager.Instance == null)
            {
                return;
            }
            var selectedObject = InteractableManager.Instance.GetSelectedObject();

            if (isUsing && selectedObject)
            {
                selectedObject.Use(GetComponent<ICanInteract>());
                return;
            }

            //Already holding item
            if (objectInBeak != null)
            {
                if (selectedObject == null || !selectedObject.IsInteractableWhileHoldingObject())
                {
                    if (isUsing)
                        objectInBeak.Use(GetComponent<ICanInteract>());
                    else
                        objectInBeak.Interact(GetComponent<ICanInteract>());
                    if (OnInteractingWithObject != null) OnInteractingWithObject(null);
                    return;
                }
                else if (selectedObject.IsInteractableWhileHoldingObject())
                {
                    selectedObject.Interact(GetComponent<ICanInteract>());
                    return;
                }
            }

            //Not holding anything AND no obj selected
            if (selectedObject == null)
            {
                return;
            }

            //Not holding anything AND has a obj selected 
            if (selectedObject.PlayerShouldPeck())
            {
                //object is picked up during second half of animation
                animator.SetTrigger("interact");
                StartCoroutine(Util.VoidCallbackTimer(0.25f,
                    () =>
                    {
                        selectedObject.Interact(GetComponent<ICanInteract>());
                        StartCoroutine(ControlManager.Instance.RumblePulse(0.25f, 0.25f, 0.15f));
                    }
                    ));
            }
            else
            {
                selectedObject.Interact(GetComponent<ICanInteract>());
            }

            if (OnInteractingWithObject != null) OnInteractingWithObject(selectedObject);

        }
    }
    #endregion

    public IEnumerator Peck()
    {
        canDoBirdlyAction = false;
        isDoingBirdlyAction = true;
        blockingMovement = true;

        //make sure same action not performed twice in a row
        int nextBirdlyAction = prevBirdlyAction + UnityEngine.Random.Range(1, 4);
        if (nextBirdlyAction > 3) nextBirdlyAction -= 4;
        animator.SetInteger("BirdlyAction", nextBirdlyAction);
        switch (nextBirdlyAction)
        {
            // Peck ground
            case 0:
                PlayerSounds.Instance.PlayPeckSound();
                StartCoroutine(ControlManager.Instance.RumblePulse(0.2f, 0.2f, 0.15f));
                break;
            // Flap wings
            case 1:
                PlayerSounds.Instance.PlayFlapSound();
                StartCoroutine(ControlManager.Instance.RumblePulse(0.25f, 0.25f, 0.2f, 0.15f, 2));
                break;
            // Wobble
            case 2:
                PlayerSounds.Instance.PlayCooSound();
                StartCoroutine(ControlManager.Instance.RumbleAlternate(0.25f, true, 0.2f, 0.15f, 1));
                break;
            // Scratch self
            case 3:
                PlayerSounds.Instance.PlayCooSound();
                StartCoroutine(ControlManager.Instance.RumblePulse(0.0f, 0.25f, 0.2f));
                break;
            default:
                PlayerSounds.Instance.PlayPeckSound();
                break;
        }
        prevBirdlyAction = nextBirdlyAction;

        if (objectInBeak != null)
            objectInBeak.Interact(GetComponent<ICanInteract>());

        yield return new WaitForSeconds(birdlyActionMoveFreezeTime);

        animator.SetInteger("BirdlyAction", -1);

        blockingMovement = false;
        canDoBirdlyAction = true;
        isDoingBirdlyAction = false;
    }

    public void ShowSparks()
    {
        sparkParticles.Play();
    }
}