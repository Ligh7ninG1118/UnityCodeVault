using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UtilityEnums;
using UtilityFunc;
using Random = UnityEngine.Random;

public class CookingPot : MonoBehaviour, ISavable, IRaycastable
{
    public static CookingPot Instance;

    [Header("Upgradable Attributes")] 
    [Tooltip("In seconds")] 
    [SerializeField] private float baseCookingTime;
    [SerializeField] private float cookedDuplicateChance = 0.1f;
    [SerializeField] private int cookedDuplicateMin = 3;
    [SerializeField] private int cookedDuplicateMax = 7;

    [Header("Misc.")]
    [SerializeField] private float interactRange = 2.0f;
    [SerializeField] private RawToCookedFoodData rawToCookedData;
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private float burstForceMultiplier = 1.5f;
    [Tooltip("X for X Axis (Left/Right), Y for Z Axis (Forward/Backward)")]
    public Vector2 interactOffset; 
    
    [Header("UI")]
    [SerializeField] private GameObject uiElements;
    [SerializeField] private Image processItemIcon;
    [SerializeField] private TextMeshProUGUI processItemNum;
    [SerializeField] private Image processItemProgress;
    
    [Header("DEBUG DONT CHANGE")]
    public UpgradableAttribute cookingTimeAttribute;
    [SerializeField] private float _currentProgress = 0.0f;
    private Queue<Consumable> _foodProcessQueue;
    [SerializeField] private bool _isProcessing = false;
    [SerializeField] private Consumable _currentProcessingFood;
    public bool canTriggerDuplicate = false;

    private AudioSource _as;
    public bool isSpawnedDuringLoading = true;
    
    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("Two CookingPot in the scene");
        }
        Instance = this;
        
        cookingTimeAttribute = new UpgradableAttribute(baseCookingTime);
        _foodProcessQueue = new Queue<Consumable>();

        _as = GetComponent<AudioSource>();
    }

    private void Start()
    {
        GirlAI.OnGirlInteractedWithIB += CookingActionCancelledEventHandler;
        PlayerShooter.OnPlayerShooting += CookingActionCancelledEventHandler;
        
        ((ISavable)this).Subscribe();
    }

    private void OnDestroy()
    {
        GirlAI.OnGirlInteractedWithIB -= CookingActionCancelledEventHandler;
        PlayerShooter.OnPlayerShooting -= CookingActionCancelledEventHandler;
        
        ((ISavable)this).Unsubscribe();
    }

    void Update()
    {
        if (_isProcessing)
        {
            /*if (ExploreController.Instance.isMoving)
            {
                RefundFood();
                return;
            }*/
            
            _currentProgress += Time.deltaTime / cookingTimeAttribute.currentVal;
            processItemProgress.fillAmount = _currentProgress;
            
            if (_currentProgress >= 1.0f)
            {
                OnCookFinished();
            }
        }
    }

    public bool IsProcessing()
    {
        return _isProcessing;
    }

    private void CookingActionCancelledEventHandler()
    {
        /*if(_isProcessing)
            RefundFood();*/
    }

    private void QueueFoodForProcessing(Consumable food, bool resetProgress = true)
    {
        if (_foodProcessQueue.Count == 0 && !_isProcessing)
        {
            CookFood(food, resetProgress);
        }
        else
        {
            _foodProcessQueue.Enqueue(food);
        }
        
        uiElements.SetActive(true);
        processItemIcon.sprite = food.itemSprite;
        processItemNum.text = (_foodProcessQueue.Count + 1).ToString();
    }

    private void CookFood(Consumable food, bool resetProgress = true)
    {
        if(resetProgress)
            _currentProgress = 0.0f;
        _currentProcessingFood = food;
        _isProcessing = true;
        processItemProgress.fillAmount = _currentProgress;
        
        AudioManager.Instance.PlaySFXLoop("PotCooking", _as);
    }

    private void RefundFood()
    {
        int size = _foodProcessQueue.Count + 1;
        GameObject rawPrefab = null;
        foreach (var element in rawToCookedData.rawToCookedList)
        {
            if (element.consumableType == _currentProcessingFood.type)
            {
                rawPrefab = element.RawFoodPrefab;
                break;
            }
        }

        for (int i = 0; i < size; i++)
        {
            var generatedGO = Instantiate(rawPrefab, transform.position, Quaternion.identity);
            
            float angle = Random.Range(0.0f, 360.0f);
            Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0.0f, Mathf.Sin(angle * Mathf.Deg2Rad));
            
            Vector3 force = offset;
            force.y = 2.0f;
            generatedGO.GetComponent<Rigidbody>().AddForce(force * burstForceMultiplier, ForceMode.Impulse);
        }
        
        _foodProcessQueue.Clear();
        _currentProgress = 0.0f;
        _currentProcessingFood = null;
        uiElements.SetActive(false);
        _isProcessing = false;
        
        AudioManager.Instance.StopSFXLoop("PotCooking");
    }

    private void OnCookFinished()
    {
        // add processed item to player inventory
        GameObject cookedPrefab = null;
        foreach (var element in rawToCookedData.rawToCookedList)
        {
            if (element.consumableType == _currentProcessingFood.type)
            {
                cookedPrefab = element.CookedFoodPrefab;
                break;
            }
        }

        if (cookedPrefab != null)
        {
            int count = 1;
            if (canTriggerDuplicate)
            {
                if (Random.Range(0.0f, 1.0f) < cookedDuplicateChance)
                {
                    count = Random.Range(cookedDuplicateMin, cookedDuplicateMax + 1);
                }
            }

            for (int i = 0; i < count; i++)
            {
                var generatedGO = Instantiate(cookedPrefab, transform.position, Quaternion.identity);
            
                float angle = Random.Range(0.0f, 360.0f);
                Vector3 offset = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0.0f, Mathf.Sin(angle * Mathf.Deg2Rad));
            
                Vector3 force = offset;
                force.y = 2.0f;
                generatedGO.GetComponent<Rigidbody>().AddForce(force * burstForceMultiplier, ForceMode.Impulse);
            }
        }
        
        
        if (_foodProcessQueue.Count > 0)
        {
            CookFood(_foodProcessQueue.Dequeue());
        }
        else
        {
            AudioManager.Instance.StopSFXLoop("PotCooking");
            AudioManager.Instance.PlaySFXOneShot2D("PotFinishCooking");
            uiElements.SetActive(false);
            _isProcessing = false;
        }
        processItemNum.text = (_foodProcessQueue.Count + 1).ToString();

    }
    
    #region ISavable Implementation

    public List<Tuple<string, dynamic>> SaveElement()
    {
        List<Tuple<string, dynamic>> elements = new List<Tuple<string, dynamic>>();

        elements.Add(new Tuple<string, dynamic>("b_PotIsCooking", _isProcessing));
        elements.Add(new Tuple<string, dynamic>("f_PotProgress", _currentProgress));
        elements.Add(new Tuple<string, dynamic>("s_PotFoodName", _currentProcessingFood.itemName));
        elements.Add(new Tuple<string, dynamic>("i_PotFoodNum", _foodProcessQueue.Count));
        
        return elements;
    }

    public void LoadElement(SaveData saveData)
    {
        if(!isSpawnedDuringLoading)
            return;
        
        _isProcessing = (bool)saveData.saveDict["b_PotIsCooking"];

        if (_isProcessing)
        {
            int foodNum = (int)saveData.saveDict["i_PotFoodNum"];
            
            _currentProgress = (float)saveData.saveDict["f_PotProgress"];
            Consumable food = itemDatabase.GetItemDataByString((string)saveData.saveDict["s_PotFoodName"]).GetItem() as Consumable;

            for (int i = 0; i < foodNum; i++)
            {
                QueueFoodForProcessing(food, false);
            }
        }
    }

    #endregion
    #region IRaycastable Implementation
    public string hoverPrompt
    {
        get;
        set;
    }
    public bool canClickWithPrompt => true;
    public bool canClickWithoutPrompt => false;
    public Vector2 hoverPromptOffset => new Vector2(-20f, 60f);
    public void OnClickAction(UIMainCanvas mainCanvas, UIRaycastInteractor raycastInteractor, int button)
    {
        if(button != 0)
            return;
        
        if((ExploreController.Instance.transform.position - transform.position).sqrMagnitude > interactRange * interactRange)
            return;
        
        if (raycastInteractor.itemOnCursor.itemData != null && raycastInteractor.itemOnCursor.itemData.GetItem() is Consumable)
        {
            var consumable = raycastInteractor.itemOnCursor.itemData.GetItem() as Consumable;
            if (!consumable.isProcessed)
            {
                if (_foodProcessQueue.Count == 0 || consumable.type == _currentProcessingFood.type)
                {
                    QueueFoodForProcessing(consumable);
                    raycastInteractor.UsedItemOnCursor();
                    InventoryManager.Instance.DiscardItem(consumable, 1);
                }
                else
                {
                    RefundFood();
                    QueueFoodForProcessing(consumable);
                    raycastInteractor.UsedItemOnCursor();
                    InventoryManager.Instance.DiscardItem(consumable, 1);
                }
            }
        }
    }

    public string GetHoverPrompt()
    {
        return LocalizationUtility.GetLocalizedString("COOK");
    }

    public bool ShouldShowPrompt(UIMainCanvas mainCanvas, UIRaycastInteractor raycastInteractor)
    {
        if (raycastInteractor.itemOnCursor.itemData != null &&
            raycastInteractor.itemOnCursor.itemData.GetItem() is Consumable)
        {
            var consumable = raycastInteractor.itemOnCursor.itemData.GetItem() as Consumable;
            if (!consumable.isProcessed && consumable.canCook)
                return true;
        }

        return false;
    }
    
    #endregion
    
}
