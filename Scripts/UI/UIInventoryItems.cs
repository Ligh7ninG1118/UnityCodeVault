using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIInventoryItems : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    public UISlot originalSlot;

    [SerializeField] private Image itemImage;
    [SerializeField] private TMP_Text itemNumText;

    public GameObject hoverRef;
    
    public Item _itemRef;
    
    private Vector3 startPosition;
    private CanvasGroup canvasGroup;

    private bool _isItemMovingWithCursor;

    private InputMaster _inputMaster;
    private GraphicRaycaster _graphicRaycaster;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        ClearItemSprite();
    }

    private void Start()
    {
        _graphicRaycaster = GetComponentInParent<GraphicRaycaster>();
    }

    private void Update()
    {
        // move the item with cursor if should be
        if (_isItemMovingWithCursor)
        {
            transform.position = Mouse.current.position.ReadValue();
        }
    }

    private void OnEnable()
    {
        _inputMaster = new InputMaster();
        _inputMaster.Enable();
        _inputMaster.UI.LeftClick.performed += OnLeftClickAction;
        _inputMaster.UI.RightClick.performed += OnRightClickAction;
    }

    private void OnDisable()
    {
        _inputMaster.Disable();
        _inputMaster.UI.LeftClick.performed -= OnLeftClickAction;
        _inputMaster.UI.RightClick.performed -= OnRightClickAction;
    }

    private void OnRightClickAction(InputAction.CallbackContext context)
    {
        // if is moving this item, right click cancels the move
        if (_isItemMovingWithCursor)
        {
            // If the item was not dropped on a new slot, return to the original slot.
            transform.SetParent(originalSlot.transform);
            transform.localPosition = startPosition;
            
            AudioManager.Instance.PlaySFXOnUIOneShot("OnEndDropItem");
            canvasGroup.blocksRaycasts = true;
            
            // clear the moving item data
            SceneObjectManager.Instance.mainCanvas.inventoryItemDragging = null;
            _isItemMovingWithCursor = false;
        }
    }

    private void OnLeftClickAction(InputAction.CallbackContext context)
    {
        // handles all left lick logics when this item is moving with the cursor
        if (_isItemMovingWithCursor)
        {
            // raycast to see if we hit an UI element with UISlot
            PointerEventData pointerEventData = new PointerEventData(EventSystem.current)
            {
                position = Mouse.current.position.ReadValue()
            };
            
            List<RaycastResult> results = new List<RaycastResult>();
            _graphicRaycaster.Raycast(pointerEventData, results);
            
            // try to drop the item
            bool isDropSuccess = false;
            foreach (var result in results)
            {
                UISlot slot = result.gameObject.GetComponent<UISlot>();
                if (slot)
                {
                    slot.PlaceItem();
                    isDropSuccess = true;
                    break;
                }
            }
            
            // restore item position if drop was unsuccessful
            if (!isDropSuccess && transform.parent != originalSlot.transform)
            {
                // If the item was not dropped on a new slot, return to the original slot.
                transform.SetParent(originalSlot.transform);
                transform.localPosition = startPosition;
            }
            
            AudioManager.Instance.PlaySFXOnUIOneShot("OnEndDropItem");
            canvasGroup.blocksRaycasts = true;
            
            // clear the moving item data
            SceneObjectManager.Instance.mainCanvas.inventoryItemDragging = null;
            _isItemMovingWithCursor = false;
        }
    }

    public void SetupItemSprite(Item item, int val)
    {
        _itemRef = item;
        itemImage.sprite = item.itemSprite;
        itemImage.enabled = true;
        itemNumText.text = val.ToString();
    }

    public void ClearItemSprite()
    {
        _itemRef = null;
        itemImage.enabled = false;
        itemNumText.text = "";
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (_itemRef != null)
            {
                InventoryManager.Instance.UseItemOnCharacter(_itemRef, ExploreController.Instance.GetComponent<CharacterStatus>());

                if (InventoryManager.Instance.inventoryContent.TryGetValue(_itemRef, out var value))
                {
                    itemNumText.text = value.ToString();
                }
                else
                {
                    ClearItemSprite();
                }
            }
        }
        // left click on this item AND the item is present AND we're not currently moving an item with cursor
        else if (_itemRef != null && !_isItemMovingWithCursor && eventData.button == PointerEventData.InputButton.Left)
        {
            // lift the item and make it move with cursor
            AudioManager.Instance.PlaySFXOnUIOneShot("OnBeginDragItem");
            originalSlot = GetComponentInParent<UISlot>();
            startPosition = transform.localPosition;
            canvasGroup.blocksRaycasts = false;
            transform.SetParent(originalSlot.transform.parent.parent);
            
            // register this item with main canvas
            SceneObjectManager.Instance.mainCanvas.inventoryItemDragging = gameObject;

            _isItemMovingWithCursor = true;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        //TODO: literally the worst way to check, change later
        if (itemNumText.text != "")
        {
            hoverRef.SetActive(true);
            //hoverRef.GetComponent<UIItemHover>().HoverItemUI(_itemRef);
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (itemNumText.text != "")
        {
            hoverRef.SetActive(false);
        }
    }
}
