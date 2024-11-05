using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UtilityEnums;
using UtilityFunc;

public class UIItemHover : MonoBehaviour
{
    public static UIItemHover Instance;
    
    [SerializeField] private Sprite healthIcon;
    [SerializeField] private Sprite hungerIcon;
    [SerializeField] private Sprite fuelIcon;
    [SerializeField] private Sprite sanitycon;
    
    public GameObject elementGroup;
    
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text itemTypeText;
    [SerializeField] private Image effectIconImage;
    [SerializeField] private TMP_Text effectValueText;
    
    [SerializeField] private GameObject itemEffectGroup;
    [SerializeField] private GameObject item1stEffect;
    [SerializeField] private GameObject item2ndEffect;
    
    [SerializeField] private GameObject item1stAction;
    [SerializeField] private TMP_Text item1stActionText;
    [SerializeField] private TMP_Text item1stActionKey;

    [SerializeField] private GameObject itemPotAction;
    [SerializeField] private GameObject itemRuneAction;
    [SerializeField] private GameObject itemUpgradeAction;
    
    [SerializeField] private VerticalLayoutGroup _layoutGroup;

    [HideInInspector] public bool shouldBlockUIFromOpening = false;
    
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(this);
        }
        Instance = this;
    }

    public void DisplayHoverItemUI()
    {
        if(!ClimbController.Instance || !shouldBlockUIFromOpening)
            elementGroup.SetActive(true);
    }

    public void HideHoverItemUI()
    {
        elementGroup.SetActive(false);
    }
    
    // Call this method to update the UI with the info of the inventory item being hovered
    public void SetupHoverItemUI(ItemData itemData)
    {
        // Update corresponding item info

        var item = itemData.GetItem();
        itemNameText.text = LocalizationUtility.GetLocalizedString(item.itemName);
        if (item is Resource)
        {
            if ((item as Resource).materialType == MaterialType.Rune)
            {
                itemTypeText.text = LocalizationUtility.GetLocalizedString("key-item");
            }
            else
            {
                itemTypeText.text = LocalizationUtility.GetLocalizedString("resource-item");
            }
        }
        else if (item is Consumable)
        {
            var consumable = item as Consumable;
            string prefix;
            if (consumable.isProcessed)
            {
                prefix = LocalizationUtility.GetLocalizedString("cooked");
            }
            else
            {
                prefix = LocalizationUtility.GetLocalizedString("raw");
            }
            switch (consumable.effect)
            {
                case ConsumableEffect.RecoverHealth:
                    itemTypeText.text = prefix + " " + LocalizationUtility.GetLocalizedString("herb");
                    break;
                case ConsumableEffect.RecoverHunger:
                    itemTypeText.text = prefix + " " + LocalizationUtility.GetLocalizedString("food");
                    break;
                case ConsumableEffect.RecoverSanity:
                    itemTypeText.text = LocalizationUtility.GetLocalizedString("sanity-recover");
                    break;
                case ConsumableEffect.RecoverFuel:
                    itemTypeText.text = LocalizationUtility.GetLocalizedString("raw-fuel");
                    break;
            }
        }
        //_itemDescriptionText.text = item.itemDescription;

        if (item is Consumable)
        {
            var c = item as Consumable;
            itemEffectGroup.SetActive(true);
            item1stEffect.SetActive(true);

            if (!c.isProcessed && c.effect != ConsumableEffect.RecoverFuel)
            {
                item2ndEffect.SetActive(true);
            }
            else
            {
                item2ndEffect.SetActive(false);
            }

            effectValueText.text = c.consumableValue.ToString("+#;-#;0");
            switch (c.effect)
            {
                case ConsumableEffect.RecoverHealth:
                    effectIconImage.sprite = healthIcon;
                    break;
                case ConsumableEffect.RecoverHunger:
                    effectIconImage.sprite = hungerIcon;
                    break;
                case ConsumableEffect.RecoverFuel:
                    effectIconImage.sprite = fuelIcon;
                    break;
                case ConsumableEffect.RecoverSanity:
                    effectIconImage.sprite = sanitycon;
                    break;
            }
        }
        else
        {
            itemEffectGroup.SetActive(false);
            item1stEffect.SetActive(false);
            item2ndEffect.SetActive(false);
        }
        
        item1stAction.SetActive(false);
        itemPotAction.SetActive(false);
        itemRuneAction.SetActive(false);
        itemUpgradeAction.SetActive(false);
        
        if (item is Resource)
        {
            if ((item as Resource).materialType == MaterialType.Rune)
            {
                itemRuneAction.SetActive(true);
                itemRuneAction.GetComponentInChildren<TextMeshProUGUI>().text = LocalizationUtility.GetLocalizedString("chat");
            }
            else
            {
                itemUpgradeAction.SetActive(true);
                itemUpgradeAction.GetComponentInChildren<TextMeshProUGUI>().text = LocalizationUtility.GetLocalizedString("upgrade");
            }
        }
        else if (item is Consumable)
        {
            var consumable = item as Consumable;
            string prefix;
            if (!consumable.isProcessed && consumable.effect != ConsumableEffect.RecoverFuel)
            {
                itemPotAction.SetActive(true);
                itemPotAction.GetComponentInChildren<TextMeshProUGUI>().text = LocalizationUtility.GetLocalizedString("cook");
            }
            
            item1stAction.SetActive(true);
            if (consumable.effect == ConsumableEffect.RecoverSanity)
                item1stActionText.text = LocalizationUtility.GetLocalizedString("use");
            else if (consumable.effect == ConsumableEffect.RecoverFuel)
                item1stActionText.text = LocalizationUtility.GetLocalizedString("process");
            else
                item1stActionText.text = LocalizationUtility.GetLocalizedString("feed");
        }
        
        Canvas.ForceUpdateCanvases();
        _layoutGroup.enabled = false;
        _layoutGroup.enabled = true;
    }
}
