using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UtilityEnums;
using Steamworks;

public class CutsceneManager : MonoBehaviour
{
    public static CutsceneManager Instance;

    [SerializeField] private List<CutsceneData> cutsceneData;
    [SerializeField] private GameObject uiElements;
    [SerializeField] private Image imageMask;
    [SerializeField] private Image fadeInOutMask;
    [SerializeField] private Image slideImage;
    [SerializeField] private GameObject subtitlePanel;
    [SerializeField] private TextMeshProUGUI subtitleTextField;

    [SerializeField] private SceneReference mainMenuScene;

    
    private InputMaster _inputMaster;
    public bool _isPlayingCutscene = false;
    
    private int _slideIndex = -1;
    private CutsceneData _currentCutsceneData;

    private string _musicStringCache;
    
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(this);
        }
        Instance = this;
    }
    
    private void OnEnable()
    {
        _inputMaster = new InputMaster();
        _inputMaster.Enable();

        _inputMaster.UI.NextSlide.performed += NextSlideInputEventHandler;
        _inputMaster.UI.SkipAll.performed += SkipAllSlidesInputEventHandler;
    }

    private void OnDisable()
    {
        _inputMaster.UI.NextSlide.performed -= NextSlideInputEventHandler;
        _inputMaster.UI.SkipAll.performed -= SkipAllSlidesInputEventHandler;
        
        _inputMaster.Disable();
    }

    [ContextMenu("Show Family Rune Cutscene")]
    public void TutorialCutsceneTest()
    {
        KickstartCutscene(CutsceneID.FamilyRune);
    }
    
    [ContextMenu("Show EndingA Cutscene")]
    public void PlaceholderCutsceneTest()
    {
        KickstartCutscene(CutsceneID.EndingA);
    }
    
    public void KickstartCutscene(CutsceneID id)
    {
        if (_isPlayingCutscene)
        {
            Debug.LogError("Already playing cutscene. Illegal function call");
            return;
        }
        
        _isPlayingCutscene = true;
        _currentCutsceneData = FindCutsceneDataByType(id);
        if(_currentCutsceneData == null)
        {
            Debug.LogError("Error playing cutscene");
            return;
        }
        
        _slideIndex = 0;

        if (AudioManager.Instance)
        {
            _musicStringCache = AudioManager.Instance.currentPlayingMusic;
            AudioManager.Instance.StopMusic();
        }
        
        GameStateManager.Instance?.TryAddTimePause(PauseType.Cutscene);

        imageMask.color = _currentCutsceneData.imageMaskColor;
        
        StartCoroutine(ShowSlide());
    }

    public void NextSlideInputEventHandler(InputAction.CallbackContext context)
    {
        if(!_isPlayingCutscene)
            return;
        StartNextSlide();
    }

    public void StartNextSlide()
    {
        Debug.Log("start next slide");
        StartCoroutine(NextSlide());
    }

    private IEnumerator NextSlide()
    {
        yield return StartCoroutine(HideSlide());
        _slideIndex++;
        if (_slideIndex < _currentCutsceneData.cutsceneSlides.Count)
        {
            yield return StartCoroutine(ShowSlide());
        }
        else
        {
            ExecuteCutsceneEndBehavior();
        }
    }

    public void SkipAllSlidesInputEventHandler(InputAction.CallbackContext context)
    {
        if(!_isPlayingCutscene)
            return;

        ExecuteCutsceneEndBehavior();
    }

    private CutsceneData FindCutsceneDataByType(CutsceneID id)
    {
        foreach (var cutscene in cutsceneData)
        {
            if (cutscene.cutsceneID == id)
            {
                return cutscene;
            }
        }
        Debug.LogError("Cant find cutscene from CutsceneData");
        return null;
    }

    private IEnumerator ShowSlide()
    {
        Tween tween;
        
        CutsceneSlide slide = _currentCutsceneData.cutsceneSlides[_slideIndex];
        if(LocalizationManager.Instance != null)
            slideImage.sprite = LocalizationManager.Instance.GetCurrentLocaleIndex() == 1 ? slide.imageSprite : slide.imageSpriteCN;
        
        if (_slideIndex == 0)
        {
            if (_currentCutsceneData.shouldFadeIn)
            {
                tween = fadeInOutMask.DOFade(1.0f, 3.0f);
                tween.SetUpdate(true);
                yield return new DOTweenCYInstruction.WaitForCompletion(tween);

                uiElements.SetActive(true);
                tween = fadeInOutMask.DOFade(0.0f, 3.0f);
                tween.SetUpdate(true);
                yield return new DOTweenCYInstruction.WaitForCompletion(tween);
            }
            else
            {
                Color tgt = Color.black;
                fadeInOutMask.color = tgt;
                uiElements.SetActive(true);
                tween = fadeInOutMask.DOFade(0.0f, 5.0f);
                tween.SetUpdate(true);
                yield return new DOTweenCYInstruction.WaitForCompletion(tween);
            }
        }
        
        tween = slideImage.DOFade(1.0f, slide.fadeInTime);
        tween.SetUpdate(true);
        yield return new DOTweenCYInstruction.WaitForCompletion(tween);

        StartCoroutine(ShowSubtitle(slide));
        
        if(!string.IsNullOrEmpty(slide.SFXToPlay))
        {
            StartCoroutine(PlaySFX(slide));
        }

        if (!string.IsNullOrEmpty(slide.musicToPlay))
        {
            if(slide.shouldUseOverrideForCrossFadeTime)
                AudioManager.Instance.CrossFadeMusic(slide.musicToPlay, true, slide.musicCrossFadeTimeOverride);
            else
                AudioManager.Instance.CrossFadeMusic(slide.musicToPlay);
        }
    }

    private IEnumerator ShowSubtitle(CutsceneSlide slide)
    {
        subtitleTextField.text = slide.subtitleText;
        yield return new WaitForSecondsRealtime(slide.subtitlePlayDelay);
        subtitlePanel.SetActive(true);
    }

    private IEnumerator PlaySFX(CutsceneSlide slide)
    {
        yield return new WaitForSecondsRealtime(slide.SFXPlayDelay);
        
        AudioManager.Instance?.PlaySFXOneShot2D(slide.SFXToPlay);
    }

    private IEnumerator HideSlide()
    {
        CutsceneSlide slide = _currentCutsceneData.cutsceneSlides[_slideIndex];
        Tween tween;

        subtitlePanel.SetActive(false);
        
        tween = slideImage.DOFade(0.0f, slide.fadeOutTime);
        tween.SetUpdate(true);
        yield return new DOTweenCYInstruction.WaitForCompletion(tween);
    }

    public bool IsPlayingCutscene()
    {
        return _isPlayingCutscene;
    }

    private void ExecuteCutsceneEndBehavior()
    {
        GameStateManager.Instance?.TryRestoreTimePause(PauseType.Cutscene);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopMusic();
        }
        
        if (!string.IsNullOrEmpty(_musicStringCache))
        {
            AudioManager.Instance?.CrossFadeMusic(_musicStringCache);
        }
        uiElements.SetActive(false);

        switch (_currentCutsceneData.cutsceneID)
        {
            case CutsceneID.ReservedDefault:
                break;
            case CutsceneID.Opening:
                OpeningCutsceneBootstrapper.Instance.LoadNextScene();
                break;
            case CutsceneID.Tutorial1:
                break;
            case CutsceneID.Tutorial2:
                break;
            case CutsceneID.FamilyRune:
                GameStateManager.Instance.isGroundA2Unlocked = true;
                InventoryManager.Instance.RemoveRune();
                SceneObjectManager.Instance.mainCanvas.GetComponent<UIRaycastInteractor>().RemoveRune();
                break;
            case CutsceneID.ReachedTheBottom:
                GameStateManager.Instance.StartUnloadCliff();
                DialogueQuestManager.Instance.PlayYarnDialogue("EndingChoice");
                break;
            case CutsceneID.EndingA:
                if(SteamManager.Initialized)
                {
                    SteamUserStats.SetAchievement("ACH_ENDING_A");
                    SteamUserStats.StoreStats();
                }
                StartCoroutine(DoFadeOutAndExit());
                break;
            case CutsceneID.EndingB:
                if(SteamManager.Initialized)
                {
                    SteamUserStats.SetAchievement("ACH_ENDING_B");
                    SteamUserStats.StoreStats();
                }
                StartCoroutine(DoFadeOutAndExit());
                break;
            case CutsceneID.TestPlaceholder1:
                break;
            default:
                break;
        }

        _slideIndex = -1;
        _isPlayingCutscene = false;
        
        //TODO: Change to cache music playing before cutscene started, and resume to play that music
        AudioManager.Instance?.PlayMusic(AudioManager.Instance.musicOnStartUpName, AudioManager.Instance.loopMusicOnStartUp);
    }

    private IEnumerator DoFadeOutAndExit()
    {
        uiElements.SetActive(true);

        Tween tween = fadeInOutMask.DOFade(1.0f, 3.0f);
        tween.SetUpdate(true);
        yield return new DOTweenCYInstruction.WaitForCompletion(tween);
        SceneManager.LoadScene(mainMenuScene.ScenePath);
    }
}
