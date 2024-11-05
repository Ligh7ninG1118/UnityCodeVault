using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.UI;
using UnityEngine.UI;
using UtilityEnums;
using Random = UnityEngine.Random;

public class UIRaycastInteractor : MonoBehaviour, ISavable
{
    private InventoryManager inventoryRef;
    public List<UISlotv1> uiSlots;
    private GraphicRaycaster _graphicRaycaster;
    private InputMaster _inputMaster;

    public bool isCtrlPressed = false;

    [SerializeField] private float loseItemsRatioOnDeath = 0.35f;
    [SerializeField] private Vector2 cursorItemFollowOffsetPerc= new Vector2(-0.02f, 0.02f);
    [SerializeField] private RectTransform cursorItemGroup;
    [SerializeField] private ItemDatabase itemDatabase;

    private Image cursorItemImage;
    private TextMeshProUGUI tmp;

    public GameObject mouseOverObject;
    
    private UISlotv1 prevInteractedSlot = null;
    public UIItem itemOnCursor;

    private void Start()
    {
        inventoryRef = InventoryManager.Instance;

        inventoryRef.OnNewUniqueItemAdded += NewUniqueItemEventHandler;
        inventoryRef.OnNewExistingItemAdded += NewExistingItemEventHandler;
        
        _graphicRaycaster = GetComponentInParent<GraphicRaycaster>();

        cursorItemImage = cursorItemGroup.GetComponentInChildren<Image>();
        tmp = cursorItemGroup.GetComponentInChildren<TextMeshProUGUI>();
        
        ((ISavable)this).Subscribe();
    }

    private void OnDestroy()
    {
        ((ISavable)this).Unsubscribe();
    }

    private void OnEnable()
    {
        _inputMaster = new InputMaster();
        _inputMaster.Enable();
        _inputMaster.UI.LeftClick.performed += OnLeftClickAction;
        _inputMaster.UI.RightClick.performed += OnRightClickAction;
        _inputMaster.UI.Discard.performed += OnDiscardAction;

        _inputMaster.Gameplay.HoldLMB.performed += HoldLeftEventHandler;
        _inputMaster.Gameplay.HoldRMB.performed += HoldRightEventHandler;
    }

    private void OnDisable()
    {
        inventoryRef.OnNewUniqueItemAdded -= NewUniqueItemEventHandler;
        inventoryRef.OnNewExistingItemAdded -= NewExistingItemEventHandler;
        
        _inputMaster.Disable();
        _inputMaster.UI.LeftClick.performed -= OnLeftClickAction;
        _inputMaster.UI.RightClick.performed -= OnRightClickAction;
        _inputMaster.UI.Discard.performed -= OnDiscardAction;
        
        _inputMaster.Gameplay.HoldLMB.performed -= HoldLeftEventHandler;
        _inputMaster.Gameplay.HoldRMB.performed -= HoldRightEventHandler;
    }

    private void Update()
    {
        //_graphicRaycaster.Raycast(pointerEventData, results);

        isCtrlPressed = Input.GetKey(KeyCode.LeftControl);
        
        if (itemOnCursor.itemData != null)
        {
            Vector2 position = Mouse.current.position.ReadValue();
            Vector2 offset = new Vector2(cursorItemFollowOffsetPerc.x * Screen.width,
                cursorItemFollowOffsetPerc.y * Screen.height);
            cursorItemGroup.transform.position = position+offset;
        }

    }
    
    private void OnLeftClickAction(InputAction.CallbackContext context)
    {
        UISlotv1 targetUISlot = RaycastForUISlot();
        
        if (targetUISlot != null)
        {
            bool isEmptySlot = targetUISlot.itemInSlot.itemData == null;
            // Not holding item AND slot has item, pick up item
            if (itemOnCursor.itemData == null && !isEmptySlot)
            {
                AudioManager.Instance.PlaySFXOnUIOneShot("InventoryGrab");
                Debug.Log("Not holding item AND slot has item, pick up item");
                int takingQuantity = targetUISlot.itemInSlot.itemQuantity;
                if (isCtrlPressed)
                    takingQuantity = Mathf.RoundToInt(takingQuantity/2.0f);
                
                takingQuantity = Mathf.Clamp(takingQuantity, 1,10);
                itemOnCursor.itemData = targetUISlot.itemInSlot.itemData;
                itemOnCursor.itemQuantity = takingQuantity;
                
                targetUISlot.TakeItemByQuantity(takingQuantity);
                RefreshItemDisplayOnCursor();

                prevInteractedSlot = targetUISlot;
                prevInteractedSlot.ToggleSelectedOutline(true);
            }
            // Holding item AND is trash can slot, remove item on cursor
            else if (itemOnCursor.itemData != null && targetUISlot.isTrashCanSlot)
            {
                AudioManager.Instance.PlaySFXOnUIOneShot("InventoryDiscard");
                Debug.Log("Holding item AND is trash can slot, remove item on cursor");
                if (!itemOnCursor.itemData.GetItem().canBeRemoved)
                {
                    ReturnItemToPrevSlot();
                    return;
                }
                InventoryManager.Instance.DiscardItem(itemOnCursor.itemData.GetItem(), itemOnCursor.itemQuantity);
                
                itemOnCursor.itemData = null;
                itemOnCursor.itemQuantity = 0;
                prevInteractedSlot.ToggleSelectedOutline(false);

                RefreshItemDisplayOnCursor();
            }
            // Holding item AND slot is empty, put item down
            else if (itemOnCursor.itemData != null && isEmptySlot)
            {
                Debug.Log("Holding item AND slot is empty, put item down");

                int puttingDownQuantity = itemOnCursor.itemQuantity;
                if (isCtrlPressed)
                    puttingDownQuantity = Mathf.RoundToInt(puttingDownQuantity/2.0f);

                
                targetUISlot.itemInSlot.itemData = itemOnCursor.itemData;
                targetUISlot.itemInSlot.itemQuantity = puttingDownQuantity;

                itemOnCursor.itemQuantity -= puttingDownQuantity;
                prevInteractedSlot.ToggleSelectedOutline(false);

                targetUISlot.RefreshItemDisplay();
                RefreshItemDisplayOnCursor();
            }
            // Holding item is the same type as slot item, 
            else if (!isEmptySlot && itemOnCursor.itemData == targetUISlot.itemInSlot.itemData)
            {
                int returningQuantity = 10 - targetUISlot.itemInSlot.itemQuantity;
                returningQuantity = Mathf.Clamp(returningQuantity, 0, itemOnCursor.itemQuantity);

                if (isCtrlPressed)
                    returningQuantity = Mathf.RoundToInt(returningQuantity/2.0f);


                if (itemOnCursor.itemQuantity == 1 && isCtrlPressed)
                {
                    // Do nothing
                }
                else
                {
                    targetUISlot.itemInSlot.itemQuantity += returningQuantity;
                    itemOnCursor.itemQuantity -= returningQuantity;

                    targetUISlot.RefreshItemDisplay();
                    RefreshItemDisplayOnCursor();
                }
            }
            // Holding item is not the same type as the slot item, swap them
            else if (!isEmptySlot && itemOnCursor.itemData != targetUISlot.itemInSlot.itemData)
            {
                if (!isCtrlPressed)
                {
                    var tempData = targetUISlot.itemInSlot.itemData;
                    int tempInt = targetUISlot.itemInSlot.itemQuantity;

                    targetUISlot.itemInSlot.itemData = itemOnCursor.itemData;
                    targetUISlot.itemInSlot.itemQuantity = itemOnCursor.itemQuantity;

                    itemOnCursor.itemData = tempData;
                    itemOnCursor.itemQuantity = tempInt;
                    
                    targetUISlot.RefreshItemDisplay();
                    RefreshItemDisplayOnCursor();
                }
                else
                {
                    prevInteractedSlot.itemInSlot.itemData = itemOnCursor.itemData;
                    prevInteractedSlot.itemInSlot.itemQuantity += itemOnCursor.itemQuantity;
                    prevInteractedSlot.RefreshItemDisplay();
                    prevInteractedSlot.ToggleSelectedOutline(false);

                    int takingQuantity = Mathf.RoundToInt(targetUISlot.itemInSlot.itemQuantity/2.0f);
                    takingQuantity = Mathf.Clamp(takingQuantity, 1,10);
                    
                    itemOnCursor.itemData = targetUISlot.itemInSlot.itemData;
                    itemOnCursor.itemQuantity = takingQuantity;
                
                    targetUISlot.TakeItemByQuantity(takingQuantity);
                    RefreshItemDisplayOnCursor();

                    prevInteractedSlot = targetUISlot;
                    prevInteractedSlot.ToggleSelectedOutline(true);

                }
            }
        }
        else if (itemOnCursor.itemData != null && mouseOverObject != null)
        {
            /*if (mouseOverObject.CompareTag("Player"))
            {
                var consumable = itemOnCursor.itemData.GetItem() as Consumable;
                if (consumable != null)
                {
                    if (InventoryManager.Instance.UseItemOnCharacter(consumable, ExploreController.Instance.GetComponent<CharacterStatus>()))
                    {
                        itemOnCursor.itemQuantity--;
                        RefreshItemDisplayOnCursor();
                    }
                    else
                    {
                        ReturnItemToPrevSlot();
                    }
                }
            }*/
        }
        else if(itemOnCursor.itemData != null)
        {
            ReturnItemToPrevSlot();
            return;
        }
    }

    private void OnRightClickAction(InputAction.CallbackContext context)
    {
        // Holding item, return item to the previous interacted slot
        if (itemOnCursor.itemData != null)
        {
            Debug.Log("Holding item, return item to the previous interacted slot");
                
            ReturnItemToPrevSlot();
            return;
        }
        
        UISlotv1 targetUISlot = RaycastForUISlot();
        bool isEmptySlot = false;
        if (targetUISlot)
        {
            isEmptySlot = targetUISlot.itemInSlot.itemData == null;
        }

        // Not holding item AND slot has item, try use item
        if (targetUISlot != null && !isEmptySlot)
        {
            var consumable = targetUISlot.itemInSlot.itemData.GetItem() as Consumable;
            if (consumable != null && consumable.canBeUsedByPlayer)
            {
                if (InventoryManager.Instance.UseItemOnCharacter(consumable,
                        ExploreController.Instance.GetComponent<CharacterStatus>()))
                {
                    targetUISlot.itemInSlot.itemQuantity--;
                    targetUISlot.RefreshItemDisplay();
                }
            }
        }
    }

    private void OnDiscardAction(InputAction.CallbackContext context)
    {
        if (itemOnCursor.itemData != null)
        {
            if (!itemOnCursor.itemData.GetItem().canBeRemoved)
            {
                ReturnItemToPrevSlot();
                return;
            }
            
            InventoryManager.Instance.DiscardItem(itemOnCursor.itemData.GetItem(), itemOnCursor.itemQuantity);
            
            itemOnCursor.itemData = null;
            itemOnCursor.itemQuantity = 0;
            RefreshItemDisplayOnCursor();
        }
    }

    private void HoldLeftEventHandler(InputAction.CallbackContext context)
    {
        ReturnItemToPrevSlot();
    }
    
    private void HoldRightEventHandler(InputAction.CallbackContext context)
    {
        if (itemOnCursor.itemData != null)
        {
            ReturnItemToPrevSlot();
        }
    }

    private void OnShortcutAction(UISlotv1 uiSlot)
    {
        // Swap item, or put item into empty slot
        if (itemOnCursor.itemData != null)
        {
            var tempData = uiSlot.itemInSlot.itemData;
            int tempInt = uiSlot.itemInSlot.itemQuantity;

            uiSlot.itemInSlot.itemData = itemOnCursor.itemData;
            uiSlot.itemInSlot.itemQuantity = itemOnCursor.itemQuantity;

            itemOnCursor.itemData = tempData;
            itemOnCursor.itemQuantity = tempInt;
                    
            uiSlot.RefreshItemDisplay();
            RefreshItemDisplayOnCursor();
        }
        else if(uiSlot.itemInSlot.itemData != null)
        {
            var consumable = uiSlot.itemInSlot.itemData.GetItem() as Consumable;
            if (consumable != null)
            {
                InventoryManager.Instance.UseItemOnCharacter(consumable, ExploreController.Instance.GetComponent<CharacterStatus>());
                uiSlot.itemInSlot.itemQuantity--;
                uiSlot.RefreshItemDisplay();
            }
        }
    }

    public void UsedItemOnCursor()
    {
        itemOnCursor.itemQuantity--;
        //InventoryManager.Instance.DiscardItem(itemOnCursor.itemData.GetItem(), 1);
        RefreshItemDisplayOnCursor();
        
    }

    private UISlotv1 RaycastForUISlot()
    {
        UISlotv1 targetSlot = null;
        
        PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };
            
        List<RaycastResult> results = new List<RaycastResult>();
        _graphicRaycaster.Raycast(pointerEventData, results);
        
        foreach (var result in results)
        {
            var uiSlot = result.gameObject.GetComponent<UISlotv1>();
            if (uiSlot)
            {
                targetSlot = uiSlot;
            }
        }

        return targetSlot;
    }

    private void RefreshItemDisplayOnCursor()
    {
        if (itemOnCursor.itemData == null || itemOnCursor.itemQuantity == 0)
        {
            itemOnCursor.itemData = null;
            cursorItemImage.enabled = false;
            tmp.text = "";
        }
        else
        {
            cursorItemImage.enabled = true;
            cursorItemImage.sprite = itemOnCursor.itemData.GetItem().itemSprite;
            tmp.text = itemOnCursor.itemQuantity.ToString();
        }
    }

    public void ReturnItemToPrevSlot()
    {
        if(itemOnCursor.itemData == null)
            return;
        
        if (prevInteractedSlot && prevInteractedSlot.itemInSlot.itemData == null)
        {
            prevInteractedSlot.itemInSlot.itemData = itemOnCursor.itemData;
            prevInteractedSlot.itemInSlot.itemQuantity += itemOnCursor.itemQuantity;
            prevInteractedSlot.ToggleSelectedOutline(false);
            prevInteractedSlot.RefreshItemDisplay();
        }
        else
        {
            for (int i = 0; i < itemOnCursor.itemQuantity; i++)
            {
                var generatedGO = Instantiate(itemOnCursor.itemData.itemPrefab, ExploreController.Instance.transform.position, Quaternion.identity);
            
                float angle = Random.Range(0.0f, 360.0f);
                Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0.0f, Mathf.Sin(angle * Mathf.Deg2Rad));
            
                Vector3 force = offset;
                force.y = 2.0f;
                generatedGO.GetComponent<Rigidbody>().AddForce(force * 1.0f, ForceMode.Impulse);
            }
            InventoryManager.Instance?.DiscardItem(itemOnCursor.itemData.GetItem(), itemOnCursor.itemQuantity);
        }
        
        itemOnCursor.itemData = null;
        itemOnCursor.itemQuantity = 0;
        RefreshItemDisplayOnCursor();
    }
    
    private void NewUniqueItemEventHandler(ItemData itemData, bool isCollectedByPlayer)
    {
        foreach (var uiSlot in uiSlots)
        {
            if (uiSlot.itemInSlot.itemData == null)
            {
                uiSlot.itemInSlot.itemData = itemData;
                uiSlot.itemInSlot.itemQuantity = 1;
                uiSlot.RefreshItemDisplay();
                break;
            }
        }
    }

    private void NewExistingItemEventHandler(ItemData itemData, bool isCollectedByPlayer)
    {
        bool placed = false;
        foreach (var uiSlot in uiSlots)
        {
            if (uiSlot.itemInSlot.itemData == itemData && uiSlot.itemInSlot.itemQuantity < 10)
            {
                uiSlot.itemInSlot.itemQuantity += 1;
                uiSlot.RefreshItemDisplay();
                placed = true;
                break;
            }
        }

        if (!placed)
        {
            foreach (var uiSlot in uiSlots)
            {
                if (uiSlot.itemInSlot.itemData == null)
                {
                    uiSlot.itemInSlot.itemData = itemData;
                    uiSlot.itemInSlot.itemQuantity = 1;
                    uiSlot.RefreshItemDisplay();
                    break;
                }
            }
        }
        
    }
    
    [ContextMenu("Lose Half Slots")]
    public void LoseHalfSlots()
    {
        List<UISlotv1> slots = new List<UISlotv1>();
        foreach (var uiSlot in uiSlots)
        {
            if (uiSlot.itemInSlot.itemData != null && uiSlot.itemInSlot.itemData.GetItem().canBeRemoved)
            {
                slots.Add(uiSlot);
            }
        }

        int num = Mathf.CeilToInt(slots.Count * loseItemsRatioOnDeath);
        for (int i = 0; i < num; i++)
        {
            int index;
            do
            {
                index = Random.Range(0, slots.Count - 1);
            } while (slots[index].itemInSlot.itemData == null);

            int quantity = slots[index].itemInSlot.itemQuantity;
            Item item = slots[index].itemInSlot.itemData.GetItem();
            
            InventoryManager.Instance.DiscardItem(item, quantity);
            
            slots[index].itemInSlot.itemData = null;
            slots[index].itemInSlot.itemQuantity = 0;
            slots[index].RefreshItemDisplay();
        }
    }

    public void ClearAllSlots()
    {
        foreach (var slot in uiSlots)
        {
            slot.itemInSlot.itemData = null;
            slot.itemInSlot.itemQuantity = 0;
            slot.RefreshItemDisplay();
        }
    }

    public void RemoveRune()
    {
        foreach (var slot in uiSlots)
        {
            if (slot.itemInSlot.itemData != null)
            {
                Resource res = slot.itemInSlot.itemData.GetItem() as Resource;
                if ( res != null && res.materialType == MaterialType.Rune)
                {
                    slot.itemInSlot.itemData = null;
                    slot.itemInSlot.itemQuantity = 0;
                    slot.RefreshItemDisplay();
                }
            }
        }
    }

    public bool CheckSlotsAvailability(Item item)
    {
        foreach (var slot in uiSlots)
        {
            if (slot.itemInSlot.itemData == null)
            {
                return true;
            }
            else if (slot.itemInSlot.itemData.GetItem().Equals(item) && slot.itemInSlot.itemQuantity < 10)
            {
                return true;
            }
        }
        return false;
    }
    
    #region ISavable Implementation

    public List<Tuple<string, dynamic>> SaveElement()
    {
        List<Tuple<string, dynamic>> elements = new List<Tuple<string, dynamic>>();

        for (int i = 0; i < uiSlots.Count; i++)
        {
            int itemQuan = uiSlots[i].itemInSlot.itemQuantity;
            string itemName = itemQuan != 0 ? uiSlots[i].itemInSlot.itemData.GetItem().itemName : null;
            
            elements.Add(new Tuple<string, dynamic>("s_ItemNameAtSlot" + i, itemName));
            elements.Add(new Tuple<string, dynamic>("i_ItemQuanAtSlot" + i, itemQuan));
        }
        
        return elements;
    }

    public void LoadElement(SaveData saveData)
    {
        if (inventoryRef.inventoryContent.Count != 0)
        {
            Debug.LogError("Inventory not empty, shouldn't happen. Clearing inventory content now");
            inventoryRef.ClearAllItems();
        }
        
        for (int i = 0; i < uiSlots.Count; i++)
        {
            int itemQuan = Convert.ToInt32(saveData.saveDict["i_ItemQuanAtSlot" + i]);
            if (itemQuan != 0)
            {
                string itemName = (string)saveData.saveDict["s_ItemNameAtSlot" + i];
                ItemData itemData = itemDatabase.GetItemDataByString(itemName);

                uiSlots[i].itemInSlot.itemData = itemData;
                uiSlots[i].itemInSlot.itemQuantity = itemQuan;

                for (int j = 0; j < itemQuan; j++)
                {
                    inventoryRef.DebugAddItem(itemData);
                }
                uiSlots[i].RefreshItemDisplay();
            }
        }
        
        
        
    }

    #endregion
    
}
