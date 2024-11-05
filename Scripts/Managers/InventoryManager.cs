using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Schema;
using MoreMountains.Tools;
using UnityEngine;
using UnityEngine.SceneManagement;
using UtilityEnums;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [HideInInspector] [Range(0.01f, 500.0f)] public float totalLoadCapacity = 200.0f;
    [HideInInspector] [SerializeField] [Range(0.0f, 1.0f)] private float mediumStateThreshold = .5f;
    [HideInInspector] [SerializeField] [Range(0.0f, 1.0f)] private float heavyStateThreshold = .8f;
    [HideInInspector] [SerializeField] [Range(0.0f, 1.0f)] private float encumberedStateThreshold = 1.0f;
    [HideInInspector] public int onBoardNPCCount;
    [HideInInspector] public float currentLoad;
    [HideInInspector] public LoadState currentLoadState;
    
    public Dictionary<Item, int> inventoryContent;
    [Range(0, 15)] public int itemSlotNum = 8;
    
    public event Action<ItemData, bool> OnNewUniqueItemAdded;
    public event Action<ItemData, bool> OnNewExistingItemAdded;

    public event Action OnRuneAcquired;
    [HideInInspector] public bool hasAcquiredRune = false;

    public event Action OnItemRemoved;

    public event Action OnInventoryAddFailed;
    
    
    private void Awake()
    {
        if (Instance != this && Instance != null)
        {
            Destroy(this);
            return;
        }
        
        Instance = this;
        
        inventoryContent = new Dictionary<Item, int>();
        
        DontDestroyOnLoad(gameObject);
    }

    public bool TryAddItem(ItemData itemData, bool isCollectedByPlayer, bool isTestOnly = false)
    {
        Item item = itemData.GetItem();

        if (!SceneObjectManager.Instance.mainCanvas.GetComponent<UIRaycastInteractor>().CheckSlotsAvailability(item))
        {
            OnInventoryAddFailed?.Invoke();
            return false;
        }

        if (!isTestOnly)
        {
            if (inventoryContent.ContainsKey(item))
            {
                inventoryContent[item]++;
                if (OnNewExistingItemAdded != null)
                    OnNewExistingItemAdded(itemData, isCollectedByPlayer);
            }
            else
            {
                Item itemCopy = item;
                inventoryContent.Add(itemCopy, 1);
                if (OnNewUniqueItemAdded != null)
                    OnNewUniqueItemAdded(itemData, isCollectedByPlayer);

                if (itemCopy is Resource res)
                {
                    if(res.materialType == MaterialType.Rune)
                    {
                        OnRuneAcquired?.Invoke();
                        hasAcquiredRune = true;
                    }
                }
            }
        }
        

        return true;
    }

    public void DebugAddItem(ItemData itemData)
    {
        Item item = itemData.GetItem();

        if (inventoryContent.ContainsKey(item))
        {
            inventoryContent[item]++;
        }
        else
        {
            Item itemCopy = item;
            inventoryContent.Add(itemCopy, 1);
            if (itemCopy is Resource res)
            {
                if(res.materialType == MaterialType.Rune)
                {
                    hasAcquiredRune = true;
                }
            }
        }
    }

    public void ClearAllItems()
    {
        inventoryContent.Clear();
        if(SceneObjectManager.Instance)
            SceneObjectManager.Instance.mainCanvas.GetComponent<UIRaycastInteractor>().ClearAllSlots();
    }

    public bool TestIfCanUseItemOnCharacter(Item item, CharacterStatus status)
    {
        var consumable = item as Consumable;
        return consumable.Use(status, true);
    }

    public bool UseItemOnCharacter(Item item, CharacterStatus status)
    {
        var consumable = item as Consumable;

        if (consumable.Use(status))
        {
            AudioManager.Instance.PlaySFXOneShot2D("Eat", true);
            RemoveItem(item, 1);
            return true;
        }
        return false;
    }

    public bool HasFuelFlower()
    {
        foreach (var pair in inventoryContent)
        {
            if (pair.Key.itemName == "Raw Fuel Flower")
            {
                return true;
            }
        }

        return false;
    }

    public bool HasRune()
    {
        foreach (var pair in inventoryContent)
        {
            if (pair.Key.itemName == "Rune")
            {
                return true;
            }
        }

        return false;
    }

    public void RemoveRune()
    {
        Item targetItem = null;
        foreach (var pair in inventoryContent)
        {
            if (pair.Key.itemName == "Rune")
            {
                targetItem = pair.Key;
            }
        }

        if (targetItem != null)
        {
            DiscardItem(targetItem, 1);
        }
    }

    private void CalculateWeight(Item item)
    {
        currentLoad += item.itemWeight;
        CheckLoadStateChange();
    }

    // Public: called by UpgradeSystem to reevaluate load when upgrading load capacity 
    public void CheckLoadStateChange()
    {
        if (currentLoad >= encumberedStateThreshold * totalLoadCapacity)
            currentLoadState = LoadState.Encumbered;
        else if (currentLoad >= heavyStateThreshold * totalLoadCapacity)
            currentLoadState = LoadState.Heavy;
        else if (currentLoad >= mediumStateThreshold * totalLoadCapacity)
            currentLoadState = LoadState.Medium;
        else
            currentLoadState = LoadState.Light;
    }

    public void DiscardItem(Item item, int val)
    {
        if(inventoryContent[item] < val)
            Debug.LogError("Illegal function call");

        RemoveItem(item, val);
        OnItemRemoved?.Invoke();
    }

    private void RemoveItem(Item item, int val)
    {
        inventoryContent[item] = inventoryContent[item] - val;
        currentLoad -= item.itemWeight * val;
        CheckLoadStateChange();
        if (inventoryContent[item] <= 0)
        {
            inventoryContent.Remove(item);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    // on scene loaded
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // if loaded the climbing scene, add the total weight of remaining NPCs
        if (scene.name == "_Climbing_Startup")
        {
            currentLoad += (onBoardNPCCount + 1) * 8f;
        }
    }
}
