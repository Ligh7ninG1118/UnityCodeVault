using System.Collections;
using System.Collections.Generic;
using DG.DemiLib;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class UIMainCanvas : MonoBehaviour
{
    [Header("NPC Purple UI Refs")]
    [SerializeField] private GameObject purpleCard;
    [SerializeField] private GameObject purpleCardDead;
    [SerializeField] private GameObject purpleCardMIA;
    [SerializeField] private Image purpleAvatar;
    [SerializeField] private GameObject purpleUIBloodVFX;
    
    [Header("NPC Blue UI Refs")]
    [SerializeField] private GameObject blueCard;
    [SerializeField] private GameObject blueCardDead;
    [SerializeField] private Image blueAvatar;
    [SerializeField] private GameObject blueUIBloodVFX;
    
    [Header("NPC Green UI Refs")]
    [SerializeField] private GameObject greenCard;
    [SerializeField] private GameObject greenCardDead;
    [SerializeField] private Image greenAvatar;
    [SerializeField] private GameObject greenUIBloodVFX;
    
    [Header("Other Refs")]
    [SerializeField] private GameObject gameOverMenu;

    [SerializeField] private GameObject characterPanel;
    
    [SerializeField] private GameObject finishLevelOverlay;

    [SerializeField] private Material NPCLowHealthUIMaterial;
    
    [Header("Fade Overlay Refs")]
    [SerializeField] private Image fadeInOutOverlay;
    [SerializeField] private float fadeDuration;

    [Header("Enemy Alert Refs")]
    public UINewEnemyAlert newEnemyAlert;

    [Header("Elevator UI Refs")]
    public GameObject groundElevatorMenu;
    public GameObject groundElevatorA2Menu;
    
    [Header("Item Related Refs")]
    public GameObject mouseHoverPrompt;

    public GameObject winMenu;

    public GameObject inventoryItemDragging;

    public bool shouldBlockUI;

    [Header("Ground Tutorial Refs")]
    public GameObject[] groundTutorialPrompts;

    [Header("Teleport Home Refs")]
    public GameObject teleportIcon;
    public Image teleportHomeFill;

    [Header("Cliff Tutorial Refs")]
    public GameObject cliffTutorialFailPanel;

    [Header("New Tutorial Stuff")]
    public UITutorialHighlighter tutorialHighlighter;
    public UIUpgradeSystem upgradeSystem;
    
    // Start is called before the first frame update
    void Start()
    {
        if (SceneObjectManager.Instance)
        {
            SceneObjectManager.Instance.mainCanvas = this;
        }
    }

    public void SetGameOverMenuActive(bool active, bool causedByA1)
    {
        gameOverMenu.SetActive(active);
        gameOverMenu.GetComponent<UIGameOver>().SetDeathCause(causedByA1);
    }

    public void SetCharacterPanelActive(bool active)
    {
        if (characterPanel)
        {
            characterPanel.SetActive(active);
        }
    }

    public void ToggleTeleportHomeUI(bool active)
    {
        teleportIcon.SetActive(active);
        teleportHomeFill.gameObject.SetActive(active);
    }

    public void SetFinishLevelOverlayActive(bool active)
    {
        finishLevelOverlay.SetActive(active);
    }

    public void SetTeleportHomeFillRate(float fillRate)
    {
        teleportHomeFill.fillAmount = fillRate;
    }

    public void EnableGroundTutorialPanelAtIndex(int idx)
    {
        DisableAllGroundTutorialPanels();
        groundTutorialPrompts[idx].SetActive(true);
    }
    
    public void DisableGroundTutorialPanelAtIndex(int idx)
    {
        groundTutorialPrompts[idx].SetActive(false);
    }

    public void DisableAllGroundTutorialPanels()
    {
        foreach (GameObject go in groundTutorialPrompts)
        {
            go.SetActive(false);
        }
    }

    public void FadeOutScreen()
    {
        Color originalColor = fadeInOutOverlay.color;
        fadeInOutOverlay.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1.0f);
        fadeInOutOverlay.gameObject.SetActive(true);
        
        fadeInOutOverlay.DOColor(new Color(originalColor.r, originalColor.g, originalColor.b, 0.0f), fadeDuration).OnComplete(DisableFadeOverlay);
    }

    public void EnableFadeOverlay()
    {
        Color originalColor = fadeInOutOverlay.color;
        fadeInOutOverlay.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1.0f);
        
        fadeInOutOverlay.gameObject.SetActive(true);
    }

    private void DisableFadeOverlay()
    {
        Color originalColor = fadeInOutOverlay.color;
        fadeInOutOverlay.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.0f);
        
        fadeInOutOverlay.gameObject.SetActive(false);
    }

    public void SetNPCLowHealthUIActive(UtilityEnums.CharacterType characterType, bool active)
    {
        Material mat = active? NPCLowHealthUIMaterial : null;
        switch (characterType)
        {
            case UtilityEnums.CharacterType.NPCPurple:
                purpleAvatar.material = mat;
                purpleUIBloodVFX.SetActive(active);
                break;
            case UtilityEnums.CharacterType.NPCBlue:
                blueAvatar.material = mat;
                blueUIBloodVFX.SetActive(active);
                break;
            case UtilityEnums.CharacterType.NPCGreen:
                greenAvatar.material = mat;
                greenUIBloodVFX.SetActive(active);
                break;
        }
    }

    public void SetNPCDead(UtilityEnums.CharacterType characterType)
    {
        // gray out the corresponding npc
        switch (characterType)
        {
            case UtilityEnums.CharacterType.NPCPurple:
                purpleCard.SetActive(false);
                purpleCardDead.SetActive(true);
                purpleCardMIA.SetActive(false);
                if (SceneObjectManager.Instance.npcDict[characterType].isMIA)
                {
                    purpleCard.SetActive(true);
                    purpleCardMIA.SetActive(true);
                    purpleCardDead.SetActive(false);
                }
                break;
            case UtilityEnums.CharacterType.NPCBlue:
                blueCard.SetActive(false);
                blueCardDead.SetActive(true);
                break;
            case UtilityEnums.CharacterType.NPCGreen:
                greenCard.SetActive(false);
                greenCardDead.SetActive(true);
                break;
        }
    }
}
