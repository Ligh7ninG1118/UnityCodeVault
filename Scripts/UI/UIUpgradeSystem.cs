using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UtilityEnums;
using UtilityFunc;

public class UIUpgradeSystem : MonoBehaviour, ISavable
{
    public static UIUpgradeSystem Instance;
    
    [SerializeField] private GameObject buttonElementGroup;
    [SerializeField] private GameObject buttonReminder;

    
    [Space(10.0f)]
    [SerializeField] private GameObject uiPanel;

    [SerializeField] private GameObject uiHover;

    [SerializeField] private GameObject hoverPanelBG;

    [SerializeField] private TextMeshProUGUI nameText;
    
    [SerializeField] private TextMeshProUGUI descText;

    [SerializeField] private TextMeshProUGUI levelText;
    
    [SerializeField] private TextMeshProUGUI fulfillmentText;
    [SerializeField] private Color unlockedTextColor;
    [SerializeField] private Color fulfilledTextColor;
    [SerializeField] private Color unfulfilledTextColor;

    [SerializeField] private List<Image> materialIcons;
    [SerializeField] private List<TextMeshProUGUI> materialNums;

    [SerializeField] private MaterialToSpriteData iconData;
    
    [SerializeField][Range(0.0f, 1.0f)] private float hoverXOffsetPerc = 0.0f;

    [SerializeField] private GameObject potIndicator;
    public List<UIUpgradeSlot> _upgradeSlots;

    [SerializeField] private RectTransform pot1Rect;
    [SerializeField] private RectTransform mainCanvasRect;

    private UpgradeData mouseHoverUpgradeData;

    private float hoverBGWidth;

    private bool _isMaterialEnough = false;

    private float resolutionRatio = 0.0f;

    private bool _hasTaughtPot;

    private DOTweenAnimation _panelAnim;

    public static bool isEnabled;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
        }
        Instance = this;
        
        hoverBGWidth = hoverPanelBG.GetComponent<RectTransform>().rect.width;
        
        buttonReminder.SetActive(false);

        _panelAnim = uiPanel.GetComponent<DOTweenAnimation>();

        foreach (var slot in _upgradeSlots)
        {
            slot.Init();
        }
        ((ISavable)this).Subscribe();
    }

    private void Start()
    {
        InventoryManager.Instance.OnNewExistingItemAdded += NewItemCollectedEventHandler;
        InventoryManager.Instance.OnNewUniqueItemAdded += NewItemCollectedEventHandler;
        InventoryManager.Instance.OnItemRemoved += ItemRemovedEventHandler;
    }

    private void OnDestroy()
    {
        InventoryManager.Instance.OnNewExistingItemAdded -= NewItemCollectedEventHandler;
        InventoryManager.Instance.OnNewUniqueItemAdded -= NewItemCollectedEventHandler;
        InventoryManager.Instance.OnItemRemoved -= ItemRemovedEventHandler;
        
        ((ISavable)this).Unsubscribe();
    }

    void Update()
    {
        if(ClimbController.Instance == null)
            buttonElementGroup.SetActive(true);
        else
            buttonElementGroup.SetActive(false);
        
        if (Input.GetKeyDown(KeyCode.Tab) && ClimbController.Instance == null)
        {
            // if (GameStateManager.Instance && !GameStateManager.Instance.isPlayerInGroundCombat &&
            //     GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.PlayerGetsClose] &&
            //     !GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.PlayerFirstTab])
            // {
            //     SceneObjectManager.Instance.mainCanvas.DisableAllGroundTutorialPanels();
            //     GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.PlayerFirstTab] = true;
            //     // DialogueQuestManager.Instance.PlayYarnDialogue("PlayerTAB");
            // }
            TogglePanel();
            
            if (GameStateManager.Instance && !GameStateManager.Instance.isPlayerInGroundCombat && GameStateManager.Instance.A1SpecialDialoguesUnlockState[A1SpecialDialogues.A1Pot] &&
                !GameStateManager.Instance.hasTaughtPot)
            {
                GameStateManager.Instance.hasTaughtPot = true;
                TutorialManager.OnTutorialSectionFinished?.Invoke(TutorialSection.OpenUpgradeMenu);
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape) && uiPanel.activeInHierarchy)
        {
            TogglePanel();
        }
        
        
        if(!uiPanel.activeSelf)
        {
            mouseHoverUpgradeData = null;
        }

        if (mouseHoverUpgradeData == null)
        {
            uiHover.SetActive(false);
        }
        else
        {
            resolutionRatio = Screen.width / 1920.0f;
            
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector2 cutoffOffset = Vector2.zero;
            if (mousePos.x + hoverBGWidth * resolutionRatio > Screen.width)
            {
                cutoffOffset.x = - (Screen.width * 0.2f);
            }
            
            Vector2 hoverOffset = Vector2.zero;

            hoverOffset.x = Screen.width * hoverXOffsetPerc;
            
            uiHover.transform.position = Mouse.current.position.ReadValue() + hoverOffset + cutoffOffset;
            
            uiHover.SetActive(true);
        }
    }

    private void LateUpdate()
    {
        isEnabled = uiPanel.activeInHierarchy;

    }

    public void TogglePanel()
    {
        RefreshAllUpgradeIcons();
        bool val = !uiPanel.activeInHierarchy;

        AudioManager.Instance.PlaySFXOnUIOneShot(val ? "UpgradeMenuOn" : "UpgradeMenuOff");

        if (!val && ExploreController.Instance && ExploreController.Instance.isDashUnlocked &&
            !ExploreController.Instance.hasTaughtDash)
        {
            TutorialManager.Instance.ShowDashTutorial();
            ExploreController.Instance.hasTaughtDash = true;
        }

        StartCoroutine(DoTogglePanelAnim(val));
    }

    private IEnumerator DoTogglePanelAnim(bool val)
    {
        if (val)
        {
            if (!GameStateManager.Instance.TryAddTimePause(PauseType.UpgradeMenu))
                yield break;

            _panelAnim.DORestart();
        }
        else
        {
            if (!GameStateManager.Instance.TryRestoreTimePause(PauseType.UpgradeMenu))
                yield break;

            _panelAnim.DOPlayBackwards();
            yield return new WaitForSeconds(_panelAnim.duration);
            buttonReminder.SetActive(false);
            CheckUpgradeMaterialProgress();
        }
        uiPanel.SetActive(val);
    }

    public void SetHoverData(UpgradeData data)
    {
        mouseHoverUpgradeData = data;
        if(data != null)
        {
            nameText.text = LocalizationUtility.GetLocalizedString(mouseHoverUpgradeData.upgrade.upgradeName);
            descText.text = LocalizationUtility.GetLocalizedString(mouseHoverUpgradeData.upgrade.upgradeDescription);
            //levelText.text = "Lv." + mouseHoverUpgradeData.upgrade.upgradeLevel.ToString();

            if (data.upgrade.unlockValue < 0)
            {
                fulfillmentText.text = LocalizationUtility.GetLocalizedString("unlocked");
                fulfillmentText.color = unlockedTextColor;
            }
            else if (mouseHoverUpgradeData.CheckMaterials(true))
            {
                fulfillmentText.text = LocalizationUtility.GetLocalizedString("fulfilled");
                fulfillmentText.color = fulfilledTextColor;
            }
            else
            {
                fulfillmentText.text = LocalizationUtility.GetLocalizedString("unfulfilled");
                fulfillmentText.color = unfulfilledTextColor;
            }

            ResetMaterialUIElements();

            List<Upgrade.Require> requires = new List<Upgrade.Require>();
            requires.AddRange(mouseHoverUpgradeData.upgrade.requireMaterials);

            for (int i = 0; i < requires.Count; i++)
            {
                materialIcons[i].sprite = iconData.MaterialTypeToSprite(requires[i].material);
                materialIcons[i].enabled = true;
                materialNums[i].text = requires[i].quantity.ToString();
            }

        }
    }

    public void RefreshFulfillmentText(UpgradeData data)
    {
        if(data != null)
        {
            if (data.upgrade.unlockValue < 0)
            {
                fulfillmentText.text = LocalizationUtility.GetLocalizedString("unlocked");
                fulfillmentText.color = unlockedTextColor;
            }
            else if (data.CheckMaterials(true))
            {
                fulfillmentText.text = LocalizationUtility.GetLocalizedString("fulfilled");
                fulfillmentText.color = fulfilledTextColor;
            }
            else
            {
                fulfillmentText.text = LocalizationUtility.GetLocalizedString("fulfilled");
                fulfillmentText.color = unfulfilledTextColor;
            }
        }
    }

    private void ResetMaterialUIElements()
    {
        foreach (var icon in materialIcons)
        {
            icon.enabled = false;
        }

        foreach (var num in materialNums)
        {
            num.text = "";
        }
    }

    public void RefreshAllUpgradeIcons()
    {
        foreach (var slot in _upgradeSlots)
        {
            slot.RefreshIcon();
        }
    }

    private void NewItemCollectedEventHandler(ItemData itemData, bool isCollectedByPlayer)
    {
        CheckUpgradeMaterialProgress();
    }

    private void ItemRemovedEventHandler()
    {
        CheckUpgradeMaterialProgress();
        RefreshAllUpgradeIcons();
    }

    private void CheckUpgradeMaterialProgress()
    {
        buttonReminder.SetActive(false);
        foreach (var slot in _upgradeSlots)
        {
            var data = slot.GetUpgradeData();
            
            if(data.upgrade.unlockValue != 0)
                continue;

            if (data.CheckMaterials(true))
            {
                buttonReminder.SetActive(true);
                break;
            }
        }
    }

    public Vector2 GetPot1ViewportPositionAndSize(out float size)
    {
        Vector2 screenPos = new Vector2(pot1Rect.anchorMin.x * Screen.width + pot1Rect.anchoredPosition.x * mainCanvasRect.localScale.x, pot1Rect.anchorMin.y * Screen.height + pot1Rect.anchoredPosition.y * mainCanvasRect.localScale.y);
        Camera cam = GameObject.FindGameObjectWithTag("ExploreMainCamera").GetComponent<Camera>();
        Vector2 viewPortPos = cam.ScreenToViewportPoint(screenPos);

        size = pot1Rect.rect.height / Screen.height;

        return viewPortPos;
    }
    
    
    #region ISavable Implementation

    public List<Tuple<string, dynamic>> SaveElement()
    {
        List<Tuple<string, dynamic>> elements = new List<Tuple<string, dynamic>>();
        foreach (var upgradeSlot in _upgradeSlots)
        {
            Upgrade upgrade = upgradeSlot.GetUpgradeData().upgrade;
            elements.Add(new Tuple<string, dynamic>("i_"+upgrade.upgradeName + upgrade.upgradeLevel, upgrade.unlockValue));
        }
        return elements;
    }

    public void LoadElement(SaveData saveData)
    {
        StartCoroutine(LoadElementCoroutine((saveData)));
    }

    private IEnumerator LoadElementCoroutine(SaveData saveData)
    {
        yield return new WaitForSeconds(0.2f);
        
        List<UIUpgradeSlot> upgradeList = new List<UIUpgradeSlot>();
        upgradeList = _upgradeSlots;
        
        upgradeList.Sort((i,j) => (int)i.GetUpgradeData().upgrade.upgradeEffect - (int)j.GetUpgradeData().upgrade.upgradeEffect );

        foreach (var upgradeSlot in upgradeList)
        {
            Upgrade upgrade = upgradeSlot.GetUpgradeData().upgrade;
            upgrade.unlockValue = Convert.ToInt32(saveData.saveDict["i_" + upgrade.upgradeName + upgrade.upgradeLevel]);
            if (upgrade.unlockValue == -1)
                upgradeSlot.GetUpgradeData().DebugCraftUpgrade(false, false);
        }

        //RefreshAllUpgradeIcons();
    }
    
    #endregion
}
