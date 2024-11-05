using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UtilityEnums;
using UtilityFunc;
using Math = System.Math;

public class UIPauseMenu : MonoBehaviour
{
    
    [SerializeField] private GameObject uiPanel;
    [SerializeField] private bool shouldDisableKeyControl;
    [SerializeField] private SceneReference mainMenuScene;

    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    
    [SerializeField] private GameObject musicOn;
    [SerializeField] private GameObject musicOff;
    [SerializeField] private GameObject sfxOn;
    [SerializeField] private GameObject sfxOff;

    [SerializeField] private TextMeshProUGUI musicNum;
    [SerializeField] private TextMeshProUGUI sfxNum;
    
    [SerializeField] private GameObject confirmPrompt;
    [SerializeField] private TextMeshProUGUI confirmPromptText;

    [SerializeField] private Button saveButton;
    [SerializeField] private GameObject savePanel;

    [SerializeField] private Toggle chineseToggle;
    [SerializeField] private Toggle englishToggle;

    [SerializeField] private List<GameObject> hoverIndicators;
    

    private bool preventInputHack = true;

    private float _cacheMusicVolume = 1.0f;
    private float _cacheSFXVolume = 1.0f;

    private Action _pendingAction;
    
    IEnumerator Start()
    {
        InitSliders();
        yield return new WaitForSeconds(1.5f);
        preventInputHack = false;
        InitLanguageToggles();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !shouldDisableKeyControl && !UIUpgradeSystem.isEnabled && !preventInputHack)
        {
            TogglePanel();
        }

        bool shouldDisableSaveButton = false;
        if (SceneObjectManager.Instance != null && DialogueQuestManager.Instance != null)
            if (SceneObjectManager.Instance.mobList.Count != 0 || DialogueQuestManager.Instance.IsDialogueRunning())
                shouldDisableSaveButton = true;
        
        saveButton.interactable = !shouldDisableSaveButton;
    }

    public void ConfirmQuitGame()
    {
        SetConfirmingAction(QuitGame);
        confirmPrompt.SetActive(true);
        confirmPromptText.text = LocalizationUtility.GetLocalizedString("menu-pause-quit-confirm");
    }

    public void ConfirmBackToMainMenu()
    {
        SetConfirmingAction(BackToMainMenu);
        confirmPrompt.SetActive(true);
        confirmPromptText.text = LocalizationUtility.GetLocalizedString("menu-pause-main-confirm");
    }
    
    private void SetConfirmingAction(Action pendingAction)
    {
        _pendingAction = pendingAction;
    }

    public void ExecuteConfirmingAction()
    {
        _pendingAction();
        ClearConfirmingAction();
    }

    public void ClearConfirmingAction()
    {
        _pendingAction = null;
    }

    public void BackToMainMenu()
    {
        GameStateManager.Instance?.ClearTimePause();
        SceneManager.LoadScene(mainMenuScene.ScenePath);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void InitSliders()
    {
        //TODO: Bad! This will run forever in the main menu scene
        StartCoroutine((Util.ConditionalCallbackTimer(
            () => AudioManager.Instance != null,
            () => { musicOn.SetActive(true);
                musicOff.SetActive(false);
                musicSlider.value = AudioManager.Instance.GetMusicVolume();
                sfxOn.SetActive(true);
                sfxOff.SetActive(false);
                sfxSlider.value = AudioManager.Instance.GetSFXVolume(); }
        )));
    }

    private void InitLanguageToggles()
    {
        if (LocalizationManager.Instance.GetCurrentLocaleIndex() == 1)
        {
            englishToggle.SetIsOnWithoutNotify(true);
            chineseToggle.SetIsOnWithoutNotify(false);
        }
        else
        {
            englishToggle.SetIsOnWithoutNotify(false);
            chineseToggle.SetIsOnWithoutNotify(true);
        }
    }

    private void SwitchLanguage(int idx)
    {
        LocalizationManager.Instance.SetLocale(idx);
    }

    public void ToggleChineseOption()
    {
        // Block player turning it off by clicking on an already On toggle button
        englishToggle.SetIsOnWithoutNotify(false);
        chineseToggle.SetIsOnWithoutNotify(true);
        SwitchLanguage(0);
    }
    
    public void ToggleEnglishOption()
    {
        // two toggles are mutually exclusive
        englishToggle.SetIsOnWithoutNotify(true);
        chineseToggle.SetIsOnWithoutNotify(false);
        SwitchLanguage(1);
    }
    
    public void AdjustMusicVolume(float val)
    {
        if(AudioManager.Instance)
            AudioManager.Instance.AdjustMusicVolume(val);
        
        if(ProfileManager.Instance)
            ProfileManager.Instance.SavePreferenceData("MusicVolume", val);

        musicOn.SetActive(true);
        musicOff.SetActive(false);

        int num = (int)Math.Round(val * 100.0f);
        musicNum.text = num.ToString();
    }

    public void AdjustSFXVolume(float val)
    {
        if(AudioManager.Instance)
            AudioManager.Instance.AdjustSFXVolume(val);
        
        if(ProfileManager.Instance)
            ProfileManager.Instance.SavePreferenceData("SFXVolume", val);
        
        sfxOn.SetActive(true);
        sfxOff.SetActive(false);
        
        int num = (int)Math.Round(val * 100.0f);
        sfxNum.text = num.ToString();
    }
    
    
    public void ToggleMusic(bool val)
    {
        if (val)
        {
            musicSlider.value = _cacheMusicVolume;
            
            int num = (int)Math.Round(_cacheMusicVolume * 100.0f);
            musicNum.text = num.ToString();
            
            if(AudioManager.Instance)
                AudioManager.Instance.AdjustMusicVolume(musicSlider.value);
            
            if(ProfileManager.Instance)
                ProfileManager.Instance.SavePreferenceData("MusicVolume", musicSlider.value);
        }
        else
        {
            _cacheMusicVolume = musicSlider.value;
            musicSlider.value = 0.0001f;
            musicNum.text = "0"; 
            
            if(AudioManager.Instance)
                AudioManager.Instance.AdjustMusicVolume(musicSlider.value);
            
            if(ProfileManager.Instance)
                ProfileManager.Instance.SavePreferenceData("MusicVolume", 0.0001f);
        }
    }

    public void ToggleSFX(bool val)
    {
        if (val)
        {
            sfxSlider.value = _cacheSFXVolume;
            int num = (int)Math.Round(_cacheSFXVolume * 100.0f);
            sfxNum.text = num.ToString();
            
            if(AudioManager.Instance)
                AudioManager.Instance.AdjustSFXVolume(sfxSlider.value);
            
            if(ProfileManager.Instance)
                ProfileManager.Instance.SavePreferenceData("SFXVolume", sfxSlider.value);
            
        }
        else
        {
            _cacheSFXVolume = sfxSlider.value;
            sfxSlider.value = 0.0001f;
            sfxNum.text = "0";
            
            if(AudioManager.Instance)
                AudioManager.Instance.AdjustSFXVolume(sfxSlider.value);
            
            if(ProfileManager.Instance)
                ProfileManager.Instance.SavePreferenceData("SFXVolume", 0.0001f);
        }
    }
    
    public void TogglePanel()
    {
        bool val = !uiPanel.activeInHierarchy;
        
        if (GameStateManager.Instance)
        {
            if (val)
            {
                if (!GameStateManager.Instance.TryAddTimePause(PauseType.PauseMenu))
                    return;
            }
            else
            {
                if (!GameStateManager.Instance.TryRestoreTimePause(PauseType.PauseMenu))
                    return;
            }
        }
        
        uiPanel.SetActive(val);
        if(!val)
        {
            if(savePanel != null)
                savePanel.SetActive(false);

            if (hoverIndicators != null && hoverIndicators.Count > 0)
            {
                foreach (var hoverIndicator in hoverIndicators)
                {
                    hoverIndicator.SetActive(false);
                }
            }
           
            if(SceneObjectManager.Instance)
                SceneObjectManager.Instance.mainCanvas.shouldBlockUI = false;
        }
    }

}
