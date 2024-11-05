using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIMonitorSystem : MonoBehaviour
{
    public static UIMonitorSystem Instance;

    [Header("IB Stats")] 
    [SerializeField] private GameObject emergencyFuelGroup;
    [SerializeField] private Image emergencyFuelBarFill;
    
    [Space(10.0f)]
    [SerializeField] private Image fuelBarFill;
    [SerializeField] private GameObject normalFuelIcon;
    [SerializeField] private GameObject lowFuelIcon;
    [SerializeField] private TextMeshProUGUI fuelRemainingText;
    [SerializeField] private TextMeshProUGUI fuelTotalText;
    [SerializeField] private Color normalIBStatsTextColor;
    [SerializeField] private Color lowIBStatsTextColor;


    [Header("Girl Stats")] 
    [SerializeField] private List<GameObject> girlStatsElementGroup;
    
    [SerializeField] private Image healthBarFill;
    [SerializeField] private Image hungerBarFill;
    [SerializeField] private Image sanityBarFill;
    [SerializeField] private TextMeshProUGUI healthRemainingText;
    [SerializeField] private TextMeshProUGUI hungerRemainingText;
    [SerializeField] private TextMeshProUGUI sanityRemainingText;
    [SerializeField] private TextMeshProUGUI healthTotalText;
    [SerializeField] private TextMeshProUGUI hungerTotalText;
    [SerializeField] private TextMeshProUGUI sanityTotalText;
    [SerializeField] private Color normalGirlStatsTextColor;
    [SerializeField] private Color lowGirlStatsTextColor;
    
    [SerializeField] private List<HorizontalLayoutGroup> _layoutGroups;
    
    private GirlAI _girlRef;
    private CharacterStatus _girlStatus;
    private ExploreController _ibExploreRef;
    private CharacterStatus _ibExploreStatus;
    
    
    private void Awake()
    {
        if(Instance != null)
            Destroy(this);
        Instance = this;

        healthRemainingText.color = normalGirlStatsTextColor;
        hungerRemainingText.color = normalGirlStatsTextColor;
        sanityRemainingText.color = normalGirlStatsTextColor;
        
        fuelRemainingText.color = normalIBStatsTextColor;
        
        lowFuelIcon.SetActive(false);
        normalFuelIcon.SetActive(true);
        emergencyFuelGroup.SetActive(false);

        foreach (var go in girlStatsElementGroup)
        {
            go.SetActive(false);
        }
    }

    private void Start()
    {
        _girlRef = GirlAI.Instance;
        _girlStatus = _girlRef.GetComponent<CharacterStatus>();
        _girlStatus.healthRef.OnEnteringLowState += HealthEnterLowStateEventHandler;
        _girlStatus.healthRef.OnExitingLowState += HealthExitLowStateEventHandler;
        _girlStatus.hungerRef.OnEnteringLowState += HungerEnterLowStateEventHandler;
        _girlStatus.hungerRef.OnExitingLowState += HungerExitLowStateEventHandler;
        _girlStatus.sanityRef.OnEnteringLowState += SanityEnterLowStateEventHandler;
        _girlStatus.sanityRef.OnExitingLowState += SanityExitLowStateEventHandler;

        _ibExploreRef = ExploreController.Instance;
        _ibExploreStatus = _ibExploreRef.GetComponent<CharacterStatus>();
        _ibExploreStatus.fuelRef.OnEnteringLowState += FuelEnterLowStateEventHandler;
        _ibExploreStatus.fuelRef.OnExitingLowState += FuelExitLowStateEventHandler;

        if (GameStateManager.Instance.hasCliffTutorialLevelCompleted)
            EnableGirlStats();
    }

    private void OnDestroy()
    {
        _girlStatus.healthRef.OnEnteringLowState -= HealthEnterLowStateEventHandler;
        _girlStatus.healthRef.OnExitingLowState -= HealthExitLowStateEventHandler;
        _girlStatus.hungerRef.OnEnteringLowState -= HungerEnterLowStateEventHandler;
        _girlStatus.hungerRef.OnExitingLowState -= HungerExitLowStateEventHandler;
        _girlStatus.sanityRef.OnEnteringLowState -= SanityEnterLowStateEventHandler;
        _girlStatus.sanityRef.OnExitingLowState -= SanityExitLowStateEventHandler;
        _ibExploreStatus.fuelRef.OnEnteringLowState -= FuelEnterLowStateEventHandler;
        _ibExploreStatus.fuelRef.OnExitingLowState -= FuelExitLowStateEventHandler;
    }

    private void Update()
    {
        RefreshStatsDisplay();
    }


    private void RefreshStatsDisplay()
    {
        // IB Stats

        Status fuelRef = null;

        if (ExploreController.Instance.gameObject.activeSelf)
            fuelRef = _ibExploreStatus.fuelRef;
        else if(ClimbController.Instance.gameObject.activeSelf)
        {
            fuelRef = ClimbController.Instance.GetComponent<CharacterStatus>().fuelRef;

            if (fuelRef._isLowState)
            {
                FuelEnterLowStateEventHandler();
            }
            else
            {
                FuelExitLowStateEventHandler();
            }
        }

        if (fuelRef != null)
        {
            float ibFuel = fuelRef.GetValue();
            float ibMaxFuel = fuelRef.GetMaxValue();
            fuelBarFill.fillAmount = ibFuel / ibMaxFuel;
            fuelRemainingText.text = ibFuel.ToString("0");
            fuelTotalText.text = "/" + ibMaxFuel.ToString("0");
        }
        
        // Girl Stats
        float girlHealth = _girlStatus.healthRef.GetValue();
        float girlMaxHealth = _girlStatus.healthRef.GetMaxValue();
        healthBarFill.fillAmount = girlHealth / girlMaxHealth;
        healthRemainingText.text = girlHealth.ToString("0");
        healthTotalText.text = "/" + girlMaxHealth.ToString("0");
        
        if(_girlStatus.healthRef._isLowState)
            HealthEnterLowStateEventHandler();
        else
            HealthExitLowStateEventHandler();
        
        float girlHunger = _girlStatus.hungerRef.GetValue();
        float girlMaxHunger = _girlStatus.hungerRef.GetMaxValue();
        hungerBarFill.fillAmount = girlHunger / girlMaxHunger;
        hungerRemainingText.text = girlHunger.ToString("0");
        hungerTotalText.text = "/" +girlMaxHunger.ToString("0");
        
        if(_girlStatus.hungerRef._isLowState)
            HungerEnterLowStateEventHandler();
        else
            HungerExitLowStateEventHandler();
        
        float girlSanity = _girlStatus.sanityRef.GetValue();
        float girlMaxSanity = _girlStatus.sanityRef.GetMaxValue();
        sanityBarFill.fillAmount = girlSanity / girlMaxSanity;
        sanityRemainingText.text = girlSanity.ToString("0");
        sanityTotalText.text = "/" +girlMaxSanity.ToString("0");
        
        if(_girlStatus.sanityRef._isLowState)
            SanityEnterLowStateEventHandler();
        else
            SanityExitLowStateEventHandler();
        
        Canvas.ForceUpdateCanvases();
        foreach (var layoutGroup in _layoutGroups)
        {
            layoutGroup.enabled = false;
            layoutGroup.enabled = true;
        }
    }

    public void EnableEmergencyFuelDisplay()
    {
        emergencyFuelGroup.SetActive(true);
    }

    public void EnableGirlStats()
    {
        foreach (var go in girlStatsElementGroup)
        {
            go.SetActive(true);
        }
    }

    public void SetEmergencyFuelStats(bool isSpent)
    {
        emergencyFuelBarFill.fillAmount = isSpent ? 0.0f : 1.0f;
    }

    private void HealthEnterLowStateEventHandler()
    {
        healthRemainingText.color = lowGirlStatsTextColor;
    }
    private void HealthExitLowStateEventHandler()
    {
        healthRemainingText.color = normalGirlStatsTextColor;
    }
    private void HungerEnterLowStateEventHandler()
    {
        hungerRemainingText.color = lowGirlStatsTextColor;
    }
    private void HungerExitLowStateEventHandler()
    {
        hungerRemainingText.color = normalGirlStatsTextColor;
    }
    private void SanityEnterLowStateEventHandler()
    {
        sanityRemainingText.color = lowGirlStatsTextColor;
    }
    private void SanityExitLowStateEventHandler()
    {
        sanityRemainingText.color = normalGirlStatsTextColor;
    }
    private void FuelEnterLowStateEventHandler()
    {
        fuelRemainingText.color = lowIBStatsTextColor;
        lowFuelIcon.SetActive(true);
        normalFuelIcon.SetActive(false);
    }
    private void FuelExitLowStateEventHandler()
    {
        fuelRemainingText.color = normalIBStatsTextColor;
        lowFuelIcon.SetActive(false);
        normalFuelIcon.SetActive(true);
    }
}
