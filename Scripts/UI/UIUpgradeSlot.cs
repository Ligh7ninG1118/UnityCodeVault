using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIUpgradeSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    
    [SerializeField] private UpgradeData upgradeData;

    [Space(10)]
    [SerializeField] private Sprite disabledIcon;
    [SerializeField] private Sprite lockedIcon;
    [SerializeField] private Sprite unlockedIcon;

    [Space(10)] 
    [SerializeField] private GameObject reminderIconPrefab;
    [SerializeField] private GameObject completeIconPrefab;

    [Space(10)] 
    [SerializeField] private bool preventSubsequentUnlockOverride = false;

    private List<Image> _activeAnchorPoints;
    private List<Image> _activeAnchorLines;

    private Image _iconSlot;
    private UIUpgradeSystem _upgradeSystem;
    private EventTrigger _eventTrigger;

    private GameObject _reminderIconGO;
    private GameObject _completeIconGO;
    
    public void Init()
    {
        _iconSlot = GetComponentInChildren<Image>();
        if(_iconSlot == null)
            Debug.LogError("UIUpgradeSlot::Awake(): Image component acquired failed in " + transform.name);

        _eventTrigger = GetComponent<EventTrigger>();
        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerClick;
        entry.callback.AddListener((arg0 => { TryUnlockUpgrade();}));
        _eventTrigger.triggers.Add(entry);
        
        upgradeData.ResetUnlockValue();
        

        _reminderIconGO = Instantiate(reminderIconPrefab, _iconSlot.transform);
        _reminderIconGO.SetActive(false);
        _completeIconGO = Instantiate(completeIconPrefab, _iconSlot.transform);
        _completeIconGO.SetActive(false);

        _upgradeSystem = GetComponentInParent<UIUpgradeSystem>();

        _activeAnchorLines = new List<Image>();
        _activeAnchorPoints = new List<Image>();
        
        foreach (var image in GetComponentsInChildren<Image>())
        {
            if (image.gameObject.CompareTag("AnchorLine"))
            {
                _activeAnchorLines.Add(image);
            }

            if (image.gameObject.CompareTag("AnchorPoint"))
            {
                _activeAnchorPoints.Add(image);
            }
        }
        
        RefreshIcon();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9) && DebugManager.Instance.isDebugMode)
        {
            upgradeData.DebugCraftUpgrade();
            RefreshIcon();
        }
    }

    public void TryUnlockUpgrade()
    {
        if (upgradeData.TryCraftUpgrade())
        {
            _upgradeSystem.RefreshFulfillmentText(upgradeData);
            if(!preventSubsequentUnlockOverride)
                StartCoroutine(DoAnchorAnimation());

            _upgradeSystem.RefreshAllUpgradeIcons();
            AudioManager.Instance.PlaySFXOnUIOneShot("UpgradeIconPressSuccess");
        }
        else
        {
            AudioManager.Instance.PlaySFXOnUIOneShot("UpgradeIconPressFailure");
        }
    }

    public void RefreshIcon()
    {
        if (upgradeData.upgrade.unlockValue <= -1)
        {
            _iconSlot.sprite = unlockedIcon;
            _reminderIconGO.SetActive(false);
            _completeIconGO.SetActive(true);
            foreach (var point in _activeAnchorPoints)
            {
                point.gameObject.SetActive(true);
                point.fillAmount = 1.0f;
            }
            foreach (var line in _activeAnchorLines)
            {
                line.gameObject.SetActive(true);
                line.fillAmount = 1.0f;
            }
        }
        else if (upgradeData.upgrade.unlockValue == 0)
        {

            _iconSlot.sprite = lockedIcon;
            _completeIconGO.SetActive(false);
            if(upgradeData.CheckMaterials(true))
                _reminderIconGO.SetActive(true);
            else
                _reminderIconGO.SetActive(false);
        }
        else
        {
            _iconSlot.sprite = disabledIcon;
            _reminderIconGO.SetActive(false);
            _completeIconGO.SetActive(false);
        }
    }

    public UpgradeData GetUpgradeData()
    {
        return upgradeData;
    }

    private IEnumerator DoAnchorAnimation()
    {
        Tween tween;
        foreach (var point in _activeAnchorPoints)
        {
            tween = point.DOFillAmount(1.0f, 0.5f);
            tween.SetUpdate(true);
        }
        
        yield return new WaitForSecondsRealtime(0.5f);
        
        foreach (var line in _activeAnchorLines)
        {
            tween = line.DOFillAmount(1.0f, 0.5f);
            tween.SetUpdate(true);
        }
        
        yield return new WaitForSecondsRealtime(0.5f);

        _upgradeSystem.RefreshAllUpgradeIcons();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _upgradeSystem.SetHoverData(upgradeData);
        AudioManager.Instance.PlaySFXOnUIOneShot("UpgradeIconHover");
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        _upgradeSystem.SetHoverData(null);
    }
}
