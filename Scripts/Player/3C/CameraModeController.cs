using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using UnityEngine.UI;


public struct NPCState
{
    public NPC npc;
    public int depth;
    public int bbType;
}

public class CameraModeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] CinemachineVirtualCamera firstPersonCam;
    [SerializeField] RepeatedTask poopPictureTask;


    [Header("Camera Settings")]
    [Tooltip("Maxmium Zoom FOV for Camera Mode")]
    [SerializeField][Range(10f, 120f)] public float maxZoomHFOV1stP = 60f;
    [Tooltip("Minimum Zoom FOV for Camera Mode")]
    [SerializeField][Range(10f, 120f)] public float minZoomHFOV1stP = 20f;
    [Tooltip("Use smooth zooming or a more rigid zooming")]
    [SerializeField] bool smoothZooming = true;
    [Tooltip("Smooth speed for smooth zooming. Higher value equals more rigid zooming")]
    [SerializeField][Range(0f, 100f)] float smoothZoomingSpeed = 10f;
    [Tooltip("Maximum up/down angle for vertical panning")]
    [SerializeField][Range(0f, 180f)] float verticalPanningMaxmiumAngle = 85f;
    [Tooltip("Maximum left/right angle for horizontal panning")]
    [SerializeField][Range(0f, 180f)] float horizontalPanningMaxmiumAngle = 60f;
    //[SerializeField][Range(0f, 1f)] 
    public float panningSpeed = 0.3f;
    [Tooltip("Have Camera FOV affects panning speed. The lower the FOV is, the slower the camera pans")]
    [SerializeField] bool FOVAffectsPanningSpeed = true;
    [HideInInspector] public bool isCameraModeEnabled = false;
    private float targetFOV1stP;
    private Vector2 startCamAngle;


    [Header("Identification Settings")]

    [Tooltip("Maximum distance between the bird and the NPC for them to be identifiable")]
    [SerializeField][Range(0f, 100f)] float maxIdentifiableDistance = 20.0f;
    [Tooltip("Maximum area of the screen (in percentage) the NPC has to be inside for them to be identifiable")]
    [SerializeField][Range(0f, 1f)] float maxIdentifiableScreenPct = 0.5f;
    [Tooltip("Maximum degrees for NPC to face away from the player and be identifiable. 0 means NPC can't be identified from any angles; 180 means NPC is always identifiable from any angles")]
    [SerializeField][Range(0f, 180f)] float maxIdentifiableFacingAwayDegrees = 90.0f;
    [SerializeField] LayerMask cameraMask;
    private float startingHFOV;
    public event Action OnTakingPhoto;
    public event Action<bool, string> OnIdentifying;
    public event Action<bool> OnCameraModeToggled;
    [SerializeField] private bool debugMode = false;
    private bool taskMarkedComplete = false;
    private List<NPC> identifiedNPCs = new List<NPC>();
    private bool isSwitchingCameraMode;
    private Camera mainCam;
    private PlayerController playerController;
    private CinemachineFreeLook thirdPersonCam;
    [HideInInspector] public CinemachinePOV cinemachinePOV;
    private List<NPC> npcList;

    private bool haveDoneFirstPass = false;
    private int bestCandidateIndex;

    [HideInInspector] public NPCState[] npcStateList;

    Subscription<ReloadEvent> reloadEventSub;

    private void Start()
    {
        playerController = PlayerController.Instance;
        mainCam = playerController.mainCam;
        thirdPersonCam = playerController.thirdPersonCam;

        isSwitchingCameraMode = false;

        startingHFOV = targetFOV1stP = firstPersonCam.m_Lens.FieldOfView = thirdPersonCam.m_Lens.FieldOfView;
        cinemachinePOV = firstPersonCam.GetCinemachineComponent<CinemachinePOV>();
        cinemachinePOV.m_VerticalAxis.m_MaxValue = verticalPanningMaxmiumAngle;
        cinemachinePOV.m_VerticalAxis.m_MinValue = -verticalPanningMaxmiumAngle;
        if (ControlManager.Instance.currControlScheme == ControlDeviceType.KeyboardAndMouse)
            cinemachinePOV.m_HorizontalAxis.m_MaxSpeed = cinemachinePOV.m_VerticalAxis.m_MaxSpeed = ControlManager.Instance.mouseSensCameraMode;
        else
            cinemachinePOV.m_HorizontalAxis.m_MaxSpeed = cinemachinePOV.m_VerticalAxis.m_MaxSpeed = ControlManager.Instance.joystickSensCameraMode;
        PlayerController.Instance.cameraMode.cinemachinePOV.m_HorizontalAxis.m_InvertInput = ControlManager.Instance.InvertXAxis;
        PlayerController.Instance.cameraMode.cinemachinePOV.m_VerticalAxis.m_InvertInput = !ControlManager.Instance.InvertYAxis;

        startCamAngle = Vector2.zero;
        StartCoroutine(Util.VoidCallbackTimer(1.0f,
                    () =>
                    {
                        GetNPCReferences();
                    }));

        reloadEventSub = GameEventInfra.Subscribe<ReloadEvent>(OnSceneReload);
    }

    public void UpdateFOV(float inputVal)
    {
        targetFOV1stP += inputVal;
        targetFOV1stP = Mathf.Clamp(targetFOV1stP, HFOVtoVFOV(minZoomHFOV1stP), HFOVtoVFOV(maxZoomHFOV1stP));
    }

    private void Update()
    {
        if ((isCameraModeEnabled) && haveDoneFirstPass)
        {
            IdentifyNPCPerUpdate();
        }
    }

    public void UpdatePanningSpeed(float sensVal, ControlDeviceType controlScheme)
    {
        if (controlScheme == ControlDeviceType.KeyboardAndMouse)
        {
            cinemachinePOV.m_HorizontalAxis.m_SpeedMode = AxisState.SpeedMode.InputValueGain;
            cinemachinePOV.m_HorizontalAxis.m_AccelTime = 0.0f;
            cinemachinePOV.m_VerticalAxis.m_SpeedMode = AxisState.SpeedMode.InputValueGain;
            cinemachinePOV.m_VerticalAxis.m_AccelTime = 0.0f;
            cinemachinePOV.m_VerticalAxis.m_MaxSpeed = cinemachinePOV.m_HorizontalAxis.m_MaxSpeed = panningSpeed = sensVal;
        }
        else
        {
            sensVal *= 100.0f;
            cinemachinePOV.m_HorizontalAxis.m_SpeedMode = AxisState.SpeedMode.MaxSpeed;
            cinemachinePOV.m_HorizontalAxis.m_AccelTime = 0.75f;
            cinemachinePOV.m_VerticalAxis.m_SpeedMode = AxisState.SpeedMode.MaxSpeed;
            cinemachinePOV.m_VerticalAxis.m_AccelTime = 0.75f;
            cinemachinePOV.m_VerticalAxis.m_MaxSpeed = cinemachinePOV.m_HorizontalAxis.m_MaxSpeed = panningSpeed = sensVal;
        }

    }

    public void ZoomCamera()
    {
        if (!smoothZooming)
            firstPersonCam.m_Lens.FieldOfView = targetFOV1stP;
        else
            firstPersonCam.m_Lens.FieldOfView = Mathf.MoveTowards(firstPersonCam.m_Lens.FieldOfView, targetFOV1stP, Time.unscaledDeltaTime * smoothZoomingSpeed);

        GameEventInfra.Publish<UpdateSliderUIEvent>(new UpdateSliderUIEvent(firstPersonCam.m_Lens.FieldOfView, mainCam.aspect));
        if (FOVAffectsPanningSpeed)
        {
            float affectedPanningSpeed = panningSpeed * (targetFOV1stP / startingHFOV);
            cinemachinePOV.m_HorizontalAxis.m_MaxSpeed = cinemachinePOV.m_VerticalAxis.m_MaxSpeed = affectedPanningSpeed;
        }
    }

    public IEnumerator ProcessCameraMode()
    {
        if (isSwitchingCameraMode || playerController.blockingCameraMode)
            yield break;

        if (!isCameraModeEnabled && !(playerController.dialogueActive || playerController.blockingMovement)
            && UIStateManager.Instance.IsSafeOpeningUI(UIType.CAMERA))
        {
            UIStateManager.Instance.StateSetter(UIType.CAMERA, true);
            isCameraModeEnabled = !isCameraModeEnabled;

            isSwitchingCameraMode = true;
            UISounds.Instance.PlayCameraEnterSound();
            cinemachinePOV.m_HorizontalAxis.m_MaxValue = 0.1f + mainCam.transform.rotation.eulerAngles.y;
            cinemachinePOV.m_HorizontalAxis.m_MinValue = -0.1f + mainCam.transform.rotation.eulerAngles.y;

            firstPersonCam.Priority = 1;
            thirdPersonCam.Priority = 0;
            startCamAngle.x = mainCam.transform.rotation.eulerAngles.y;


            //Wait until camera blending finishes
            yield return new WaitForSecondsRealtime(mainCam.GetComponent<CinemachineBrain>().m_DefaultBlend.BlendTime);
            ControlManager.Instance.SwtichActionMap("Camera Mode");

            mainCam.cullingMask &= ~(1 << LayerMask.NameToLayer("Player"));

            //Camera hor angle depends on current 3rd person camera, have to calculate everytime enters camera mode
            cinemachinePOV.m_HorizontalAxis.Value = startCamAngle.x;
            cinemachinePOV.m_HorizontalAxis.m_MaxValue = horizontalPanningMaxmiumAngle + startCamAngle.x;
            cinemachinePOV.m_HorizontalAxis.m_MinValue = -horizontalPanningMaxmiumAngle + startCamAngle.x;
            startCamAngle.y = cinemachinePOV.m_VerticalAxis.Value;
            Time.timeScale = 0.0f;
            isSwitchingCameraMode = false;
            IdentifyNPCFirstPass();
            OnCameraModeToggled?.Invoke(true);
        }
        else if (isCameraModeEnabled)
        {
            UIStateManager.Instance.StateSetter(UIType.CAMERA, false);
            isCameraModeEnabled = !isCameraModeEnabled;

            isSwitchingCameraMode = true;
            haveDoneFirstPass = false;
            bestCandidateIndex = -1;

            mainCam.cullingMask |= (1 << LayerMask.NameToLayer("Player"));

            firstPersonCam.Priority = 0;
            thirdPersonCam.Priority = 1;
            ControlManager.Instance.SwtichActionMap("Gameplay");

            Time.timeScale = 1.0f;
            isSwitchingCameraMode = false;
            OnCameraModeToggled?.Invoke(false);
        }
    }

    public IEnumerator ProcessScreenshot()
    {
        PlayerSounds.Instance.PlayCameraShutterSound();

        OnTakingPhoto();

        mainCam.clearFlags = CameraClearFlags.Skybox;
        yield return new WaitForEndOfFrame();

        int width = Screen.width;
        int height = Screen.height;
        RenderTexture renderTexture = new RenderTexture(width, height, 32, RenderTextureFormat.ARGB32);
        Texture2D screenshotTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);
        Rect rect = new Rect(0, 0, width, height);
        mainCam.targetTexture = renderTexture;
        mainCam.Render();

        RenderTexture currentRenderTexture = RenderTexture.active;
        RenderTexture.active = renderTexture;
        screenshotTexture.ReadPixels(rect, 0, 0);
        screenshotTexture.Apply();
        mainCam.targetTexture = null;
        RenderTexture.active = currentRenderTexture;
        GameObject.Destroy(renderTexture);

        PhotoManager.Instance?.AddPhoto(screenshotTexture);
        mainCam.clearFlags = CameraClearFlags.Nothing;

        IdentifyNPCOutput();
    }


    // ---------------------------------------------------------------------
    // First pass of identification, will execute everytime camera mode starts.
    // Since we are freezing player & game world while in camera mode, this function
    // checks for all NPC and assign them a depth value based on how many conditions
    // they passed. The results will be consistent throughout a single camera mode 
    // session, so we only need to do it once.
    // ---------------------------------------------------------------------
    // Depth    |      Notes
    // -----------------------------
    //   0      |   Outside Maximum Identifiable Distance
    //   1      |   Outside Maximum Reachable FOV
    //   2      |   Blocked by Obstacles
    //   3      |   NPC is Facing Away from the Player
    //   4      |   Has No Poop Tracker
    //   5      |   Potential Candidate
    // ---------------------------------------------------------------------
    private void IdentifyNPCFirstPass()
    {
        if (debugMode)
            Debug.Log("IdentifyNPCFirstPass(): Iterating through npcList, Count: " + npcList.Count);

        for (int i = 0; i < npcList.Count; i++)
        {
            var npc = npcList[i];
            int currentDepth = 0;
            npcStateList[i].depth = currentDepth;
            npcStateList[i].bbType = 0;

            Transform npcHead = npc.GetComponent<NPC>().headTransform.transform;
            if (debugMode)
                Debug.Log("IdentifyNPCFirstPass(): Camera now checking " + npc.name);
            Vector3 birdToNPC = npcHead.transform.position - mainCam.transform.position;

            Color c;
            if (debugMode)
            {
                c = Color.cyan;
                Debug.DrawLine(mainCam.transform.position, mainCam.transform.position + mainCam.transform.forward, c, 10.0f);
                c = Color.yellow;
                Debug.DrawLine(mainCam.transform.position, mainCam.transform.position + birdToNPC.normalized, c, 10.0f);
            }


            // ---------------------------------------------------------------------
            // Check if within distance (defined by maxIdentifiableDistance)
            // ---------------------------------------------------------------------
            if (birdToNPC.sqrMagnitude > maxIdentifiableDistance * maxIdentifiableDistance)
            {
                if (debugMode)
                {
                    Debug.Log(npc.name + " " + currentDepth);
                    Debug.Log("IdentifyNPCFirstPass(): distance: " + birdToNPC.magnitude);
                }
                continue;
            }
            currentDepth++;     // Depth = 1
            npcStateList[i].depth = currentDepth;

            // ---------------------------------------------------------------------
            // Check if within Player's maximum reachable FOV (calculated by maxIdentifiableScreenPct + horizontalPanningMaxmiumAngle)
            // ---------------------------------------------------------------------
            float dotProduct = Vector3.Dot(birdToNPC.normalized, mainCam.transform.forward);
            if (dotProduct <= Mathf.Cos((maxIdentifiableScreenPct * targetFOV1stP + horizontalPanningMaxmiumAngle * 2) * Mathf.Deg2Rad))
            {
                if (debugMode)
                {
                    Debug.Log(npc.name + " " + currentDepth);
                    Debug.Log("IdentifyNPCFirstPass(): Dot Product: " + Mathf.Acos(dotProduct) * Mathf.Rad2Deg);
                    Debug.Log("IdentifyNPCFirstPass(): Required Dot Product: " + Mathf.Cos((maxIdentifiableScreenPct * targetFOV1stP + horizontalPanningMaxmiumAngle * 2) * Mathf.Deg2Rad));
                }
                continue;
            }
            currentDepth++;     // Depth = 2
            npcStateList[i].depth = currentDepth;

            // ---------------------------------------------------------------------
            // Check if blocked by obstacles
            // ---------------------------------------------------------------------
            RaycastHit hit;
            Vector3 hitPos;
            hitPos = npcHead.transform.position + 0.5f * (mainCam.transform.position - npcHead.transform.position).normalized;
            bool hitResult = Physics.Linecast(mainCam.transform.position, hitPos, out hit, cameraMask);
            if (debugMode)
            {
                if (hitResult)
                    c = Color.red;
                else
                    c = Color.green;
                Debug.DrawLine(mainCam.transform.position, hitPos, c, 10.0f);
            }
            if (hitResult)
            {
                if (debugMode)
                {
                    Debug.Log(npc.name + " " + currentDepth);
                    Debug.Log("IdentifyNPCFirstPass(): Blocked by: " + hit.transform.name);
                }
                continue;
            }
            currentDepth++;     // Depth = 3
            npcStateList[i].depth = currentDepth;

            // ---------------------------------------------------------------------
            // Check if npc is facing towards the bird (defined by maxIdentifiableFacingAwayDegrees)
            // ---------------------------------------------------------------------
            if (Vector3.Dot(birdToNPC.normalized, npc.GetComponent<NPC>().headTransform.transform.forward) > Mathf.Cos((180.0f - maxIdentifiableFacingAwayDegrees) * Mathf.Deg2Rad))
            {
                if (debugMode)
                {
                    Debug.Log(npc.name + " " + currentDepth);
                    Debug.Log("IdentifyNPCFirstPass(): Facing Away Degrees: " + Mathf.Acos(Vector3.Dot(birdToNPC.normalized, npc.transform.forward)) * Mathf.Rad2Deg);
                }
                continue;
            }
            currentDepth++;     // Depth = 4
            npcStateList[i].depth = currentDepth;

            // ---------------------------------------------------------------------
            // Check if npc has poop tracker
            // ---------------------------------------------------------------------
            if (!npc.IsPoopedOn())
            {
                if (debugMode)
                {
                    Debug.Log(npc.name + " " + currentDepth);
                    Debug.Log("IdentifyNPCFirstPass(): Has Poop Tracker: " + npc.IsPoopedOn());
                }
                continue;
            }
            currentDepth++;     // Depth = 5
            npcStateList[i].depth = currentDepth;
        }
        haveDoneFirstPass = true;
    }

    // ---------------------------------------------------------------------
    // Second part of the identification logic, execute on every frame. This
    // function checks whether the NPCs are within player's actual screen FOV 
    // (a percentage of it), and assign them a bbType value, for drawing their
    // bounding boxes in CameraModeInterfaceController. It's depending on player's
    // current forward vector, so we need to check it every frame.
    // ---------------------------------------------------------------------
    // bbType   |       Depth & Condition      |     Notes
    // ------------------------------------------------------------------
    //   0      |       0, 1, 2                |   Do Not Draw (Disable)
    //   1      |       3 AND within view      |   Red Bounding Box
    //   2      |       4 AND within view      |   Yellow Bounding Box
    //   3      |       5 AND within view      |   Green Bounding Box
    // ---------------------------------------------------------------------
    private void IdentifyNPCPerUpdate()
    {
        float maximumDotProduct = -2.0f;
        int maximumIndex = -1;

        if (debugMode)
            Debug.Log("IdentifyNPCPerUpdate(): Iterating through npcList, Count: " + npcList.Count);


        for (int i = 0; i < npcList.Count; i++)
        {
            var npc = npcList[i];

            // For NPCs that has less than 3 depth, skip
            // bbType already initialized as 0 in FirstPass
            // if (npcStateList[i].depth < 3)
            // {
            //     if (debugMode)
            //         Debug.Log("IdentifyNPCPerUpdate(): NPC Index " + i + " skipped. Depth less than 3.");
            //     continue;
            // }

            Transform npcHead = npc.GetComponent<NPC>().headTransform.transform;
            Vector3 birdToNPC = npcHead.transform.position - mainCam.transform.position;
            float dotProduct = Vector3.Dot(birdToNPC.normalized, mainCam.transform.forward);

            // Check and log the NPC with the maximum dot product (closet to the screen center)
            // which we presume is the NPC that the player is trying to take picture of
            if (dotProduct >= maximumDotProduct)
            {
                maximumDotProduct = dotProduct;
                maximumIndex = i;
            }

            // For NPCs that are outside player's view, disable their bounding box
            if (dotProduct <= Mathf.Cos(maxIdentifiableScreenPct * targetFOV1stP * Mathf.Deg2Rad))
            {
                if (debugMode)
                    Debug.Log("IdentifyNPCPerUpdate(): NPC Index " + i + " outside view.");
                npcStateList[i].bbType = 0;
                continue;
            }

            switch (npcStateList[i].depth)
            {
                case 0:
                case 1:
                case 2:
                    npcStateList[i].bbType = 0;
                    break;
                case 3:
                    npcStateList[i].bbType = 1;
                    break;
                case 4:
                case 5:
                    npcStateList[i].bbType = 2;
                    break;
            }

        }

        if (debugMode)
            Debug.Log("IdentifyNPCPerUpdate(): maximumIndex is " + maximumIndex);

        bestCandidateIndex = maximumIndex;

        // If the NPC closest to the screen center also has a depth of 5 (passed all conditions)
        // Assign it the green bounding box
        if (npcStateList[maximumIndex].depth == 5)
        {
            npcStateList[maximumIndex].bbType = 3;
        }

    }

    // ---------------------------------------------------------------------
    // Last part of the identification logic, will execute every time the player
    // takes a picture. 
    // ---------------------------------------------------------------------
    private void IdentifyNPCOutput()
    {
        NPC npcCandidate = npcList[bestCandidateIndex];
        if (debugMode)
        {
            Debug.Log("IdentifyNPCOutput(): bestCandidateIndex is " + bestCandidateIndex);
            Debug.Log("IdentifyNPCOutput(): bestCandidateIndex depth is " + bestCandidateIndex);
        }

        switch (npcStateList[bestCandidateIndex].depth)
        {
            case 0:
                OnIdentifying(false, "distance");
                StartCoroutine(ControlManager.Instance.RumblePulse(0.7f, 0.7f, 0.1f, 0.2f, 2));
                break;
            case 1:
                OnIdentifying(false, "center");
                StartCoroutine(ControlManager.Instance.RumblePulse(0.7f, 0.7f, 0.1f, 0.2f, 2));
                break;
            case 2:
                OnIdentifying(false, "blocked");
                StartCoroutine(ControlManager.Instance.RumblePulse(0.7f, 0.7f, 0.1f, 0.2f, 2));
                break;
            case 3:
                OnIdentifying(false, "facing");
                StartCoroutine(ControlManager.Instance.RumblePulse(0.7f, 0.7f, 0.1f, 0.2f, 2));
                break;
            case 4:
                OnIdentifying(false, "poop");
                StartCoroutine(ControlManager.Instance.RumblePulse(0.7f, 0.7f, 0.1f, 0.2f, 2));
                break;
            case 5:
                OnIdentifying(true, npcCandidate.npcProfile.npcName);
                StartCoroutine(ControlManager.Instance.RumblePulse(0.7f, 0.7f, 0.1f));
                if (!identifiedNPCs.Contains(npcCandidate))
                {
                    if (!taskMarkedComplete && poopPictureTask != null && npcCandidate.shouldCountForBBQuest)
                    {
                        //taskMarkedComplete = true;
                        poopPictureTask.SetIsComplete();
                        identifiedNPCs.Add(npcCandidate);
                        if (poopPictureTask.GetCurrRepetitions() == poopPictureTask.GetTotalRepetitions())
                            taskMarkedComplete = true;
                    }

                    // unlock npc profile if any
                    if (npcCandidate.npcProfile != null && !npcCandidate.profileUnlocked)
                    {
                        npcCandidate.profileUnlocked = true;
                        PopupManager.Instance.UnlockNpcProfile(npcCandidate);
                    }
                }
                break;
        }
    }


    private void OnSceneReload(ReloadEvent _input)
    {
        StartCoroutine(Util.VoidCallbackTimer(1.0f,
                    () =>
                    {
                        GetNPCReferences();
                    }));
    }

    public void GetNPCReferences()
    {
        npcList = NPCManager.Instance.GetNPCs();
        npcStateList = new NPCState[npcList.Count];
        for (int i = 0; i < npcList.Count; i++)
            npcStateList[i].npc = npcList[i];
    }

    public void ResetCamRotation()
    {
        var c = firstPersonCam.GetCinemachineComponent<CinemachinePOV>();
        c.m_HorizontalAxis.Value = startCamAngle.x;
        c.m_VerticalAxis.Value = startCamAngle.y;
    }

    private float HFOVtoVFOV(float HFOV)
    {
        return 2.0f * Mathf.Atan((Mathf.Tan(HFOV * 0.5f * Mathf.Deg2Rad)) * (1.0f / mainCam.aspect)) * Mathf.Rad2Deg;
    }

    private void OnDestroy()
    {
        GameEventInfra.Unsubscribe<ReloadEvent>(reloadEventSub);
    }
}
