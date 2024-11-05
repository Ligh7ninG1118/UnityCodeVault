using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UtilityEnums;

public class GameStateManager : MonoBehaviour, ISavable
{
    public static GameStateManager Instance { get; private set; }
    public static Action<PlayerSetType> OnChangePlayerSetEvent;
    public static Action<CliffElevatorName> OnCliffElevatorUnlocked;
    public static Action OnCliffTutorialCompleted;

    public float gameOverDelayTime = 3.0f;
    public float climbFailReduceMaxHPPercentage = 0.8f;
    public List<GameObject> exploreScenePlayerSet;
    public List<GameObject> climbingScenePlayerSet;
    public List<GameObject> sceneSwitchTriggerBoxes;
    
    public PlayerSetType currentActivePlayerSet;

    private Stack<PauseType> _pauseStack;
    private float persistentHitpoint = 0.0f;
    [HideInInspector] public bool isClimbingFailed;
    
    // the unlock state of the cliff elevators
    public Dictionary<CliffElevatorName, bool> CliffElevatorUnlockState;
    
    // the bark unlock state of cliff climbing holds
    public Dictionary<CliffClimbingHoldBarkType, bool> CliffClimbingHoldBarkUnlockState;
    
    // the unlock state of A1 special dialogues
    public Dictionary<A1SpecialDialogues, bool> A1SpecialDialoguesUnlockState;
    
    // have we taught pot?
    public bool hasTaughtPot;
    
    // are we teaching the rune right now?
    public bool isTeachingRune;
    
    // is ground A2 unlocked?
    public bool isGroundA2Unlocked;
    
    // has the cliff tutorial level been completed?
    public bool hasCliffTutorialLevelCompleted;
    
    // has the climb tutorial run already?
    public bool hasClimbTutorialPlayed;
    
    // has the fails to grab tutorial run already?
    public bool hasGrabFailTutorialPlayed;
    
    // has the overclock tutorial played?
    public bool hasOverclockTutorialPlayed;
    
    // which cliff elevator is closest to the spider bot
    public CliffElevatorName closestCliffElevator;

    public float totalGameTime;
    public bool hasPlayerBeenToCliff;
    public bool hasPlayerBeenToRelicFor1Min;
    
    // the cliff elevator that next cliff scene load should start on
    public CliffElevatorName cliffStartElevator;
    
    // when this is greater than zero, cliff input is disabled
    public int disableCliffInputRequestCount;
    
    public bool isPlayerInGroundCombat;

    // if is currently in cliff tutorial level
    public bool isInCliffTutorial = false;

    private bool _isUnloadingCliffScene = false;

    [Header("Teleport")]
    public Transform playerHomeTransform;
    public UpgradableAttribute teleportHomeCooldown;
    public bool hasUnlockedTeleport = false;
    private float _teleportHomeCooldownTimer;

    private bool _isGameOverPendingProcess;

    [HideInInspector] public Transform groundElevatorA2Transform;
    [HideInInspector] public Transform groundElevatorA1Transform;
    
    // track the cliff elevators
    public List<CliffElevator> cliffElevators;
    
    private void Awake()
    {
        if (Instance != this && Instance != null)
        {
            Destroy(this);
            return;
        }
        
        Instance = this;
        
        // initialize sets of game objects needed to play the explore scene and climbing scene, respectively
        exploreScenePlayerSet = new List<GameObject>();
        climbingScenePlayerSet = new List<GameObject>();
        cliffElevators = new List<CliffElevator>();

        _pauseStack = new Stack<PauseType>();

        // lock the game's fps (not for now)
        // QualitySettings.vSyncCount = 0;
        // Application.targetFrameRate = 60;
        
        // initialize total game time to zero before
        // TODO: load this data from save file
        totalGameTime = 0.0f;

        CliffElevatorUnlockState = new Dictionary<CliffElevatorName, bool>
        {
            { CliffElevatorName.E1 , true},
            { CliffElevatorName.E2 , false},
            { CliffElevatorName.E3 , false},
            { CliffElevatorName.E4 , false}
        };

        CliffClimbingHoldBarkUnlockState = new Dictionary<CliffClimbingHoldBarkType, bool>
        {
            { CliffClimbingHoldBarkType.Sturdy, false},
            { CliffClimbingHoldBarkType.Normal, false},
            { CliffClimbingHoldBarkType.Fragile, false}
        };

        A1SpecialDialoguesUnlockState = new Dictionary<A1SpecialDialogues, bool>
        {
            { A1SpecialDialogues.PlayerGetsClose , false},
            { A1SpecialDialogues.PlayerFirstRightClicks, false},
            { A1SpecialDialogues.PlayerFirstTab, false},
            { A1SpecialDialogues.A1RefusesOrder, false},
            { A1SpecialDialogues.InitialFuelFlowerProcessingPt1 , false},
            { A1SpecialDialogues.InitialFuelFlowerProcessingPt2 , false},
            { A1SpecialDialogues.A1Rune, false},
            { A1SpecialDialogues.A1Pot, false},
            { A1SpecialDialogues.A1SanityTutorial, false},
            { A1SpecialDialogues.A1GoToCliff, false},
            { A1SpecialDialogues.A1FamilyRelic, false}
        };
        
        ((ISavable)this).Subscribe();
    }

    private void OnDestroy()
    {
        ((ISavable)this).Unsubscribe();
    }

    public void StartProcessGameOver()
    {
        if (_isGameOverPendingProcess)
        {
            return;
        }
        _isGameOverPendingProcess = true;
        
        StartCoroutine(ProcessGameOver());
    }
    
    private IEnumerator ProcessGameOver()
    {
        // SFX
        AudioManager.Instance.PlaySFXOneShot2D("PlayerDie");

        if (ExploreController.Instance && ExploreController.Instance.gameObject.activeSelf)
        {
            ExploreController.Instance._animator.SetAnimatorTrigger("Player Die");
        }
        
        AudioManager.Instance.StopAllSFXLoop();
        AudioManager.Instance.StopMusic();
        
        yield return new WaitForSeconds(gameOverDelayTime);
        
        // pause the game
        Time.timeScale = 0f;

        // pull up game over menu
        if (SceneObjectManager.Instance)
        {
            SceneObjectManager.Instance.mainCanvas.SetGameOverMenuActive(true, false);
        }

        _isGameOverPendingProcess = false;
    }
    
    public void TeleportPlayerToGroundA1()
    {
        if (currentActivePlayerSet == PlayerSetType.Explore)
        {
            StartCoroutine(TeleportPlayerToGroundA1Coroutine());
        }
    }

    private IEnumerator TeleportPlayerToGroundA1Coroutine()
    {
        TryAddTimePause(PauseType.Transition);
        GameObject camGround = GameObject.FindGameObjectWithTag("TransitionCameraGround");
        camGround.GetComponent<TransitionVFXManager>().PlayTransitionVFXAnimation();
        yield return new WaitForSecondsRealtime(2.4f);
        TryRestoreTimePause(PauseType.Transition);
        
        SceneObjectManager.Instance.mainCanvas.EnableFadeOverlay();
        
        ExploreController.Instance.gameObject.transform.position = groundElevatorA1Transform.position;
        yield return new WaitForSeconds(1.5f);
        
        SceneObjectManager.Instance.mainCanvas.FadeOutScreen();
    }

    public void TeleportPlayerToGroundA2()
    {
        if (currentActivePlayerSet == PlayerSetType.Explore)
        {
            StartCoroutine(TeleportPlayerToGroundA2Coroutine());
        }
    }

    private IEnumerator TeleportPlayerToGroundA2Coroutine()
    {
        TryAddTimePause(PauseType.Transition);
        GameObject camGround = GameObject.FindGameObjectWithTag("TransitionCameraGround");
        camGround.GetComponent<TransitionVFXManager>().PlayTransitionVFXAnimation();
        yield return new WaitForSecondsRealtime(2.4f);
        TryRestoreTimePause(PauseType.Transition);
        
        SceneObjectManager.Instance.mainCanvas.EnableFadeOverlay();
        
        ExploreController.Instance.gameObject.transform.position = groundElevatorA2Transform.position;
        yield return new WaitForSeconds(1.5f);
        
        SceneObjectManager.Instance.mainCanvas.FadeOutScreen();
    }

    public void TeleportPlayerHome()
    {
        // cannot use if hasn't unlock
        if (!hasUnlockedTeleport)
        {
            return;
        }
        
        // cannot use this when in climb tutorial
        if (!hasCliffTutorialLevelCompleted)
        {
            return;
        }
        
        // cannot use when cooldown is not finished
        if (_teleportHomeCooldownTimer > 0.0f)
        {
            return;
        }
        
        // cannot use in cutscenes
        if (CutsceneManager.Instance && CutsceneManager.Instance.IsPlayingCutscene())
        {
            return;
        }
        
        // cannot use during cooking
        if (CookingPot.Instance && CookingPot.Instance.IsProcessing())
        {
            return;
        }
        
        // cannot use during pause
        if (_pauseStack.Count > 0)
        {
            return;
        }
        
        if (currentActivePlayerSet == PlayerSetType.Explore)
        {
            StartCoroutine(TeleportHomeFromGroundCoroutine());
        }
        else
        {
            StartCoroutine(TeleportHomeFromCliffCoroutine());
        }

        _teleportHomeCooldownTimer = teleportHomeCooldown.currentVal;
    }

    private IEnumerator TeleportHomeFromGroundCoroutine()
    {
        TryAddTimePause(PauseType.Transition);
        TransitionVFXManager.instance.PlayTeleportVFXAnimation();
        yield return new WaitForSecondsRealtime(5f);
        TryRestoreTimePause(PauseType.Transition);
        
        SceneObjectManager.Instance.mainCanvas.EnableFadeOverlay();
        ExploreController.Instance.gameObject.transform.position =
            playerHomeTransform ? playerHomeTransform.position : Vector3.zero;
        SceneObjectManager.Instance.mainCanvas.FadeOutScreen();
    }

    private IEnumerator TeleportHomeFromCliffCoroutine()
    {
        yield return UnloadCliffSceneCoroutine(true);
        
        ExploreController.Instance.gameObject.transform.position =
            playerHomeTransform ? playerHomeTransform.position : Vector3.zero;
    }

    public void SetAllCliffTutorialComplete()
    {
        hasClimbTutorialPlayed = true;
        hasGrabFailTutorialPlayed = true;
        hasOverclockTutorialPlayed = true;
        TutorialManager.Instance.tutorialSection = TutorialSection.IntroA1;
    }

    public void SetAllGroundTutorialComplete()
    {
        A1SpecialDialoguesUnlockState[A1SpecialDialogues.PlayerGetsClose] = true;
        A1SpecialDialoguesUnlockState[A1SpecialDialogues.PlayerFirstRightClicks] = true;
        A1SpecialDialoguesUnlockState[A1SpecialDialogues.PlayerFirstTab] = true;
        A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1RefusesOrder] = true;
    }

    public void UnlockElevator(CliffElevatorName eName)
    {
        CliffElevatorUnlockState[eName] = true;
    }

    public void UnlockAllElevators()
    {
        CliffElevatorUnlockState[CliffElevatorName.E1] = true;
        CliffElevatorUnlockState[CliffElevatorName.E2] = true;
        CliffElevatorUnlockState[CliffElevatorName.E3] = true;
        CliffElevatorUnlockState[CliffElevatorName.E4] = true;
        isGroundA2Unlocked = true;
        
        TriggerOnElevatorUnlock(CliffElevatorName.E1);
        TriggerOnElevatorUnlock(CliffElevatorName.E2);
        TriggerOnElevatorUnlock(CliffElevatorName.E3);
        TriggerOnElevatorUnlock(CliffElevatorName.E4);
    }

    public bool IsElevatorUnlocked(CliffElevatorName eName)
    {
        return CliffElevatorUnlockState[eName];
    }

    public void AddToExplorePlayerSet(GameObject obj)
    {
        exploreScenePlayerSet.Add(obj);
    }

    public void AddToClimbingPlayerSet(GameObject obj)
    {
        climbingScenePlayerSet.Add(obj);
    }

    public void RemoveFromClimbingPlayerSet(GameObject obj)
    {
        climbingScenePlayerSet.Remove(obj);
    }

    public void AddToSceneSwitchTriggerBoxes(GameObject obj)
    {
        sceneSwitchTriggerBoxes.Add(obj);
    }
    
    [ContextMenu("Change player set to Climbing")]
    public void DebugChangePlayerSetToClimbing()
    {
        ChangePlayerSet(PlayerSetType.Climbing);
    }
    
    [ContextMenu("Change player set to Explore")]
    public void DebugChangePlayerSetToExplore()
    {
        ChangePlayerSet(PlayerSetType.Explore);
    }

    public bool IsSpiderPredictedToDie()
    {
        var fuelStatus = ClimbController.Instance.GetComponent<CharacterStatus>().fuelRef;
        return fuelStatus.GetValue() - fuelStatus.GetMaxValue() * climbFailReduceMaxHPPercentage <= 0.001f;
    }
    
    public void ChangePlayerSet(PlayerSetType type)
    {
        // emit event saying that we're changing player set
        OnChangePlayerSetEvent?.Invoke(type);
        
        if (type == PlayerSetType.Explore)
        {
            if (isClimbingFailed)
            {
                SceneObjectManager.Instance.mainCanvas.gameObject.GetComponent<UIRaycastInteractor>().LoseHalfSlots();

                
                var fuelStatus = ClimbController.Instance.GetComponent<CharacterStatus>().fuelRef;
                ClimbController.Instance.GetComponent<CharacterStatus>().fuelRef.ModifyValue(fuelStatus.GetMaxValue() *
                    climbFailReduceMaxHPPercentage);

                if (ClimbController.Instance.GetComponent<CharacterStatus>().fuelRef.GetValue() <= 0.001f && hasCliffTutorialLevelCompleted)
                {
                    // pause the game
                    Time.timeScale = 0f;

                    // pull up game over menu
                    if (SceneObjectManager.Instance)
                    {
                        SceneObjectManager.Instance.mainCanvas.SetGameOverMenuActive(true, false);
                    }
                }
                else if(hasCliffTutorialLevelCompleted)
                {
                    // if it is not game over, IB barks about the failure
                    ExploreController.Instance.SpawnBark(6);
                }
            }
            
            
            float tempVal = ClimbController.Instance.GetFuel();
            ExploreController.Instance.SetMaxFuel(ClimbController.Instance.GetMaxFuel());
            ExploreController.Instance.clawSpeedMultiplier =
                ClimbController.Instance.legMovingSpeedAttribute.currentVal /
                ClimbController.Instance.legMovingSpeedAttribute.baseVal;

            ExploreController.Instance.legExtentAdd = ClimbController.Instance.legExtentAdd;
            
            ExploreController.Instance.overclockCooldownDelta =
                ClimbController.Instance.overclockCooldownAttribute.currentVal - ClimbController.Instance.overclockCooldownAttribute.baseVal;
            
            ExploreController.Instance.fuelConsumptionRateMultiplier =
                ClimbController.Instance.fuelConsumptionAttribute.currentVal /
                ClimbController.Instance.fuelConsumptionAttribute.baseVal;
            
            ExploreController.Instance.SetFuel(tempVal);
            
            currentActivePlayerSet = PlayerSetType.Explore;
            
            foreach (var obj in climbingScenePlayerSet)
            {
                obj.SetActive(false);
            }
            
            foreach (var obj in exploreScenePlayerSet)
            {
                obj.SetActive(true);
            }

            isClimbingFailed = false;
        }
        else
        {
            float tempVal = ExploreController.Instance.GetFuel();
            ClimbController.Instance.SetMaxFuel(ExploreController.Instance.GetMaxFuel());
            ClimbController.Instance.legMovingSpeedAttribute.currentVal =
                ClimbController.Instance.legMovingSpeedAttribute.baseVal *
                ExploreController.Instance.clawSpeedMultiplier;
            
            ClimbController.Instance.overclockCooldownAttribute.currentVal =
                ClimbController.Instance.overclockCooldownAttribute.baseVal +
                ExploreController.Instance.overclockCooldownDelta;
            
            ClimbController.Instance.IncreaseLegExtent(ExploreController.Instance.legExtentAdd);

            ClimbController.Instance.fuelConsumptionAttribute.currentVal =
                ClimbController.Instance.fuelConsumptionAttribute.baseVal *
                ExploreController.Instance.fuelConsumptionRateMultiplier;
            
            ClimbController.Instance.SetFuel(tempVal);
            currentActivePlayerSet = PlayerSetType.Climbing;
            
            // Debug.Log("explore set count: " + exploreScenePlayerSet.Count);
            // Debug.Log("climbing set count: " + climbingScenePlayerSet.Count);
            
            foreach (var obj in exploreScenePlayerSet)
            {
                obj.SetActive(false);
            }
            
            foreach (var obj in climbingScenePlayerSet)
            {
                obj.SetActive(true);
            }
        }
        
        // deactivate trigger boxes that are not part of the target player set
        foreach (var obj in sceneSwitchTriggerBoxes)
        {
            // check for async scene unload
            if (obj)
            {
                obj.SetActive(obj.GetComponent<SceneSwitchTriggerBox>().myPlayerSetType == type);
            }
        }
    }
    
    public void StartLoadCliffTutorial()
    {
        StartCoroutine(LoadCliffTutorialCoroutine());
    }

    public void RestartCliffTutorial(bool noFuel = false)
    {
        SceneObjectManager.Instance.mainCanvas.cliffTutorialFailPanel.SetActive(false);
        StartCoroutine(RestartCliffTutorialCoroutine(noFuel));
    }

    public void StartUnloadCliffTutorial(float transitionTime = 0f)
    {
        StartCoroutine(UnloadCliffTutorialCoroutine(transitionTime));
    }

    public void StartLoadCliff(CliffElevatorName elevatorName, bool isStartup = false)
    {
        if (!hasPlayerBeenToCliff)
        {
            hasPlayerBeenToCliff = true;
        }
        
        StartCoroutine(LoadCliffSceneCoroutine(elevatorName, isStartup));
    }
    
    public void StartUnloadCliff(bool diedInCliff = false)
    {
        StartCoroutine(UnloadCliffSceneCoroutine(false, diedInCliff));
    }
    
    private IEnumerator LoadCliffTutorialCoroutine()
    {
        cliffStartElevator = CliffElevatorName.E1;
        
        SceneObjectManager.Instance.mainCanvas.EnableFadeOverlay();
        
        AsyncOperation aoUI = SceneManager.LoadSceneAsync("Cliff_UI", LoadSceneMode.Additive);
        yield return aoUI;
        
        AsyncOperation aoLevel = SceneManager.LoadSceneAsync("Cliff_TutorialLevel", LoadSceneMode.Additive);
        yield return aoLevel;
        
        ChangePlayerSet(PlayerSetType.Climbing);
        
        SceneObjectManager.Instance.mainCanvas.FadeOutScreen();

        isInCliffTutorial = true;
    }

    private IEnumerator UnloadCliffTutorialCoroutine(float transitionTime = 0f)
    {
        ChangePlayerSet(PlayerSetType.Explore);
        
        SceneObjectManager.Instance.mainCanvas.EnableFadeOverlay();

        yield return new WaitForSeconds(transitionTime);

        climbingScenePlayerSet.Clear();
        
        AsyncOperation aoUI = SceneManager.UnloadSceneAsync("Cliff_UI");
        yield return aoUI;
        
        AsyncOperation aoLevel = SceneManager.UnloadSceneAsync("Cliff_TutorialLevel");
        yield return aoLevel;
        
        SceneObjectManager.Instance.mainCanvas.FadeOutScreen();

        isInCliffTutorial = false;
        
        UIMonitorSystem.Instance.EnableGirlStats();
    }

    private IEnumerator RestartCliffTutorialCoroutine(bool noFuel)
    {
        // set max fuel if restarting cliff tutorial from death
        if (noFuel)
        {
            ClimbController.Instance.SetFuel(ClimbController.Instance.GetMaxFuel());
        }
        
        ChangePlayerSet(PlayerSetType.Explore);
        
        SceneObjectManager.Instance.mainCanvas.EnableFadeOverlay();

        climbingScenePlayerSet.Clear();
        
        AsyncOperation aoUI = SceneManager.UnloadSceneAsync("Cliff_UI");
        yield return aoUI;
        
        AsyncOperation aoLevel = SceneManager.UnloadSceneAsync("Cliff_TutorialLevel");
        yield return aoLevel;
        
        cliffStartElevator = CliffElevatorName.E1;
        
        AsyncOperation aoUI1 = SceneManager.LoadSceneAsync("Cliff_UI", LoadSceneMode.Additive);
        yield return aoUI1;
        
        SceneObjectManager.Instance.mainCanvas.EnableFadeOverlay();
        AsyncOperation aoLevel1 = SceneManager.LoadSceneAsync("Cliff_TutorialLevel", LoadSceneMode.Additive);
        yield return aoLevel1;
        
        ChangePlayerSet(PlayerSetType.Climbing);
        
        SceneObjectManager.Instance.mainCanvas.FadeOutScreen();

        _isGameOverPendingProcess = false;
    }
    
    private IEnumerator LoadCliffSceneCoroutine(CliffElevatorName elevatorName, bool isStartup = false)
    {
        // reposition the spider bot to correct cliff level
        cliffStartElevator = elevatorName;
        
        // wait for main canvas to be ready
        yield return new WaitUntil(() => SceneObjectManager.Instance && SceneObjectManager.Instance.mainCanvas);

        if (!isStartup)
        {
            SceneObjectManager.Instance.mainCanvas.DisableAllGroundTutorialPanels();

            TryAddTimePause(PauseType.Transition);
            GameObject camGround = GameObject.FindGameObjectWithTag("TransitionCameraGround");
            camGround.GetComponent<TransitionVFXManager>().PlayTransitionVFXAnimation();
            yield return new WaitForSecondsRealtime(2.4f);
            TryRestoreTimePause(PauseType.Transition);
            SceneObjectManager.Instance.mainCanvas.EnableFadeOverlay();
        }
        else
        {
            SceneObjectManager.Instance.mainCanvas.EnableFadeOverlay();
        }
        
        
        AsyncOperation aoLevel = SceneManager.LoadSceneAsync("Cliff_Level", LoadSceneMode.Additive);
        yield return aoLevel;
        
        AsyncOperation aoLighting = SceneManager.LoadSceneAsync("Cliff_Lighting", LoadSceneMode.Additive);
        yield return aoLighting;
        
        AsyncOperation aoUI = SceneManager.LoadSceneAsync("Cliff_UI", LoadSceneMode.Additive);
        yield return aoUI;

        AsyncOperation aoBase = SceneManager.LoadSceneAsync("Cliff_BaseGame", LoadSceneMode.Additive);
        yield return aoBase;
        
        AsyncOperation aoGL = SceneManager.UnloadSceneAsync("Ground_Lighting");
        yield return aoGL;
        
        // wait for ground player set to finish filling
        yield return new WaitUntil(() => exploreScenePlayerSet.Count != 0);
        
        ChangePlayerSet(PlayerSetType.Climbing);

        yield return new WaitForSeconds(1.5f);
        
        SceneObjectManager.Instance.mainCanvas.FadeOutScreen();
    }
    
    private IEnumerator UnloadCliffSceneCoroutine(bool useSpecialVFX = false, bool diedInCliff = false)
    {
        if(_isUnloadingCliffScene)
            yield break;
        
        _isUnloadingCliffScene = true;
        
        if (!diedInCliff)
        {
            if (!useSpecialVFX)
            {
                TryAddTimePause(PauseType.Transition);
                GameObject camCliff = GameObject.FindGameObjectWithTag("TransitionCameraCliff");
                camCliff.GetComponent<TransitionVFXManager>().PlayTransitionVFXAnimation();
                yield return new WaitForSecondsRealtime(2.4f);
                TryRestoreTimePause(PauseType.Transition);
            }
            else
            {
                TryAddTimePause(PauseType.Transition);
                GameObject camCliff = GameObject.FindGameObjectWithTag("TransitionCameraCliff");
                camCliff.GetComponent<TransitionVFXManager>().PlayTeleportVFXAnimation();
                yield return new WaitForSecondsRealtime(5f);
                TryRestoreTimePause(PauseType.Transition);
            }
        }
        else
        {
            TryAddTimePause(PauseType.Transition);
            GameObject camCliff = GameObject.FindGameObjectWithTag("TransitionCameraCliff");
            camCliff.GetComponent<TransitionVFXManager>().PlayGlitchVFXAnimation();
            yield return new WaitForSecondsRealtime(2.4f);
            TryRestoreTimePause(PauseType.Transition);
        }
        
        ChangePlayerSet(PlayerSetType.Explore);
        
        SceneObjectManager.Instance.mainCanvas.EnableFadeOverlay();

        climbingScenePlayerSet.Clear();
        
        AsyncOperation aoLevel = SceneManager.UnloadSceneAsync("Cliff_Level");
        yield return aoLevel;
        
        AsyncOperation aoLighting = SceneManager.UnloadSceneAsync("Cliff_Lighting");
        yield return aoLighting;
        
        AsyncOperation aoUI = SceneManager.UnloadSceneAsync("Cliff_UI");
        yield return aoUI;
        
        AsyncOperation aoBase = SceneManager.UnloadSceneAsync("Cliff_BaseGame");
        yield return aoBase;
        
        AsyncOperation aoGL = SceneManager.LoadSceneAsync("Ground_Lighting", LoadSceneMode.Additive);
        yield return aoGL;

        
        SceneObjectManager.Instance.mainCanvas.FadeOutScreen();

        _isUnloadingCliffScene = false;
    }

    public bool TryAddTimePause(PauseType pauseType)
    {
        if (_pauseStack.Count == 0)
        {
            _pauseStack.Push(pauseType);
            AddTimePause();
            if (AudioManager.Instance)
            {
                AudioManager.Instance.PauseSFXLoop("MonsterAlert");
            }
            return true;
        }
        
        PauseType currentPauseType = _pauseStack.Peek();
        // New pause has lower priority than current pause, can't override current pause
        if (pauseType > currentPauseType)
        {
            return false;
        }
        else if(pauseType == currentPauseType)
        {
            Debug.LogError("Illegal Function call");
            return false;
        }
        else
        {
            _pauseStack.Push(pauseType);
            AddTimePause();
            return true;
        }
    }

    public bool TryRestoreTimePause(PauseType pauseType)
    {
        if (_pauseStack.Count == 0)
        {
            return true;
        }
        
        if (pauseType == _pauseStack.Peek())
        {
            if (AudioManager.Instance)
            {
                AudioManager.Instance.ResumeSFXLoop("MonsterAlert");
            }
            
            RestoreTimePause();
            return true;
        }
        else
        {
            return false;
        }
    }

    public bool IsLastPausedByTutorial()
    {
        return _pauseStack.Count > 0 && _pauseStack.Peek() == PauseType.Tutorial;
    }

    public bool IsPausedByAnything()
    {
        return _pauseStack.Count > 0;
    }
    
    private void AddTimePause()
    {
        Time.timeScale = 0.0f;
    }

    private void RestoreTimePause()
    {
        _pauseStack.Pop();
        if (_pauseStack.Count == 0)
        {
            Time.timeScale = 1.0f;
        }
    }

    public void ClearTimePause()
    {
        while (_pauseStack.Count > 0)
        {
            _pauseStack.Pop();
        }
        Time.timeScale = 1.0f;
    }

    public void TryPlayGrabFailTutorial()
    {
        if (!hasGrabFailTutorialPlayed)
        {
            disableCliffInputRequestCount++;
            hasGrabFailTutorialPlayed = true;

            if (DialogueQuestManager.Instance)
            {
                DialogueQuestManager.Instance.PlayYarnDialogue("IBCliffGrabFail");
            }
        }
    }

    public void TryPlayOverclockTutorial()
    {
        if (!hasOverclockTutorialPlayed)
        {
            disableCliffInputRequestCount++;
            hasOverclockTutorialPlayed = true;

            if (DialogueQuestManager.Instance)
            {
                DialogueQuestManager.Instance.PlayYarnDialogue("IBOverclockTutorial");
            }
        }
    }

    public void TriggerOnElevatorUnlock(CliffElevatorName elevatorName)
    {
        OnCliffElevatorUnlocked?.Invoke(elevatorName);
    }
    
    [ContextMenu("Set total game time 8 min")]
    public void SetTotalGameTime8Min()
    {
        totalGameTime = 480f;
    }
    
    // record game time
    private void FixedUpdate()
    {
        totalGameTime += Time.fixedDeltaTime;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12))
        {
            Application.Quit();
        }

        if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha9))
        {
            UnlockAllElevators();
        }

        if (_teleportHomeCooldownTimer > 0.0f)
        {
            _teleportHomeCooldownTimer -= Time.deltaTime;
        }
        
        SceneObjectManager.Instance.mainCanvas.SetTeleportHomeFillRate(1f - _teleportHomeCooldownTimer / teleportHomeCooldown.currentVal);
        
        // record the closest cliff elevator
        float closestDist = float.MaxValue;
        closestCliffElevator = CliffElevatorName.E1;
        foreach (var elevator in cliffElevators)
        {
            Vector3 elevatorPos = elevator.transform.position;
            if (ClimbController.Instance)
            {
                Vector3 spiderPos = ClimbController.Instance.transform.position;
                float distSq = (spiderPos - elevatorPos).sqrMagnitude;

                if (distSq < closestDist)
                {
                    closestCliffElevator = elevator.elevatorName;
                    closestDist = distSq;
                }
            }
        }
    }
    
    #region ISavable Implementation

    public List<Tuple<string, dynamic>> SaveElement()
    {
        List<Tuple<string, dynamic>> elements = new List<Tuple<string, dynamic>>();

        elements.Add(new Tuple<string, dynamic>("b_CliffTutorialCompleted", hasCliffTutorialLevelCompleted));
        elements.Add(new Tuple<string, dynamic>("i_TotalGameTime", totalGameTime));
        
        // cliff elevator unlock states
        elements.Add(new Tuple<string, dynamic>("b_E1Unlocked", CliffElevatorUnlockState[CliffElevatorName.E1]));
        elements.Add(new Tuple<string, dynamic>("b_E2Unlocked", CliffElevatorUnlockState[CliffElevatorName.E2]));
        elements.Add(new Tuple<string, dynamic>("b_E3Unlocked", CliffElevatorUnlockState[CliffElevatorName.E3]));
        elements.Add(new Tuple<string, dynamic>("b_E4Unlocked", CliffElevatorUnlockState[CliffElevatorName.E4]));
        
        // cliff climbing hold bark unlock states
        elements.Add(new Tuple<string, dynamic>("b_HoldBarkSturdyUnlocked", CliffClimbingHoldBarkUnlockState[CliffClimbingHoldBarkType.Sturdy]));
        elements.Add(new Tuple<string, dynamic>("b_HoldBarkNormalUnlocked", CliffClimbingHoldBarkUnlockState[CliffClimbingHoldBarkType.Normal]));
        elements.Add(new Tuple<string, dynamic>("b_HoldBarkFragileUnlocked", CliffClimbingHoldBarkUnlockState[CliffClimbingHoldBarkType.Fragile]));
        
        // A1 special dialogues unlock states
        elements.Add(new Tuple<string, dynamic>("b_PlayerGetsClose", A1SpecialDialoguesUnlockState[A1SpecialDialogues.PlayerGetsClose]));
        elements.Add(new Tuple<string, dynamic>("b_InitialFuelFlowerProcessingPt1", A1SpecialDialoguesUnlockState[A1SpecialDialogues.InitialFuelFlowerProcessingPt1]));
        elements.Add(new Tuple<string, dynamic>("b_InitialFuelFlowerProcessingPt2", A1SpecialDialoguesUnlockState[A1SpecialDialogues.InitialFuelFlowerProcessingPt2]));
        elements.Add(new Tuple<string, dynamic>("b_A1Rune", A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1Rune]));
        elements.Add(new Tuple<string, dynamic>("b_A1Pot", A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1Pot]));
        elements.Add(new Tuple<string, dynamic>("b_A1SanityTutorial", A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1SanityTutorial]));
        elements.Add(new Tuple<string, dynamic>("b_A1GoToCliff", A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1GoToCliff]));
        elements.Add(new Tuple<string, dynamic>("b_A1FamilyRelic", A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1FamilyRelic]));
        
        // misc tutorial related booleans
        elements.Add(new Tuple<string, dynamic>("b_hasTaughtPot", hasTaughtPot));
        elements.Add(new Tuple<string, dynamic>("b_isTeachingRune", isTeachingRune));
        elements.Add(new Tuple<string, dynamic>("b_isGroundA2Unlocked", isGroundA2Unlocked));
        elements.Add(new Tuple<string, dynamic>("b_hasCliffTutorialLevelCompleted", hasCliffTutorialLevelCompleted));
        elements.Add(new Tuple<string, dynamic>("b_hasClimbTutorialPlayed", hasClimbTutorialPlayed));
        elements.Add(new Tuple<string, dynamic>("b_hasGrabFailTutorialPlayed", hasGrabFailTutorialPlayed));
        elements.Add(new Tuple<string, dynamic>("b_hasOverclockTutorialPlayed", hasOverclockTutorialPlayed));
        elements.Add(new Tuple<string, dynamic>("b_hasPlayerBeenToCliff", hasPlayerBeenToCliff));
        elements.Add(new Tuple<string, dynamic>("b_hasPlayerBeenToRelicFor1Min", hasPlayerBeenToRelicFor1Min));
        
        // record whether the player is in cliff, and the closest cliff elevator
        elements.Add(new Tuple<string, dynamic>("b_isPlayerInCliff", currentActivePlayerSet == PlayerSetType.Climbing && hasCliffTutorialLevelCompleted));
        elements.Add(new Tuple<string, dynamic>("i_ClosestCliffElevator", (int)closestCliffElevator));
        
        return elements;
    }

    public void LoadElement(SaveData saveData)
    {
        hasCliffTutorialLevelCompleted = (bool)saveData.saveDict["b_CliffTutorialCompleted"];
        totalGameTime = (float)saveData.saveDict["i_TotalGameTime"];
        
        // cliff elevator unlock states
        CliffElevatorUnlockState[CliffElevatorName.E1] = (bool)saveData.saveDict["b_E1Unlocked"];
        CliffElevatorUnlockState[CliffElevatorName.E2] = (bool)saveData.saveDict["b_E2Unlocked"];
        CliffElevatorUnlockState[CliffElevatorName.E3] = (bool)saveData.saveDict["b_E3Unlocked"];
        CliffElevatorUnlockState[CliffElevatorName.E4] = (bool)saveData.saveDict["b_E4Unlocked"];
        
        // cliff climbing hold bark unlock states
        CliffClimbingHoldBarkUnlockState[CliffClimbingHoldBarkType.Sturdy] = (bool)saveData.saveDict["b_HoldBarkSturdyUnlocked"];
        CliffClimbingHoldBarkUnlockState[CliffClimbingHoldBarkType.Normal] = (bool)saveData.saveDict["b_HoldBarkNormalUnlocked"];
        CliffClimbingHoldBarkUnlockState[CliffClimbingHoldBarkType.Fragile] = (bool)saveData.saveDict["b_HoldBarkFragileUnlocked"];
        
        // A1 special dialogues unlock states
        A1SpecialDialoguesUnlockState[A1SpecialDialogues.PlayerGetsClose] = (bool)saveData.saveDict["b_PlayerGetsClose"];
        A1SpecialDialoguesUnlockState[A1SpecialDialogues.InitialFuelFlowerProcessingPt1] = (bool)saveData.saveDict["b_InitialFuelFlowerProcessingPt1"];
        A1SpecialDialoguesUnlockState[A1SpecialDialogues.InitialFuelFlowerProcessingPt2] = (bool)saveData.saveDict["b_InitialFuelFlowerProcessingPt2"];
        A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1Rune] = (bool)saveData.saveDict["b_A1Rune"];
        A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1Pot] = (bool)saveData.saveDict["b_A1Pot"];
        A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1SanityTutorial] = (bool)saveData.saveDict["b_A1SanityTutorial"];
        A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1GoToCliff] = (bool)saveData.saveDict["b_A1GoToCliff"];
        A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1FamilyRelic] = (bool)saveData.saveDict["b_A1FamilyRelic"];
        
        // misc tutorial related booleans
        hasTaughtPot = (bool)saveData.saveDict["b_hasTaughtPot"];
        isTeachingRune = (bool)saveData.saveDict["b_isTeachingRune"];
        isGroundA2Unlocked = (bool)saveData.saveDict["b_isGroundA2Unlocked"];
        hasCliffTutorialLevelCompleted = (bool)saveData.saveDict["b_hasCliffTutorialLevelCompleted"];
        hasClimbTutorialPlayed = (bool)saveData.saveDict["b_hasClimbTutorialPlayed"];
        hasGrabFailTutorialPlayed = (bool)saveData.saveDict["b_hasGrabFailTutorialPlayed"];
        hasOverclockTutorialPlayed = (bool)saveData.saveDict["b_hasOverclockTutorialPlayed"];
        hasPlayerBeenToCliff = (bool)saveData.saveDict["b_hasPlayerBeenToCliff"];
        hasPlayerBeenToRelicFor1Min = (bool)saveData.saveDict["b_hasPlayerBeenToRelicFor1Min"];
        
        // get the closest cliff elevator
        closestCliffElevator = (CliffElevatorName)Convert.ToInt32(saveData.saveDict["i_ClosestCliffElevator"]);
        
        // decide whether the player saved in cliff and we need to put the player in cliff
        bool isPlayerInCliff = (bool)saveData.saveDict["b_isPlayerInCliff"];
        if (isPlayerInCliff)
        {
            StartLoadCliff((CliffElevatorName)saveData.saveDict["i_ClosestCliffElevator"], true);
        }
    }

    #endregion
}
