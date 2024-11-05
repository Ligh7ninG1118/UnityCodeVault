using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UtilityFunc;

public class UITooltip : MonoBehaviour
{
    [SerializeField] private Vector2 tooltipOffsetPerc;
    [SerializeField] private float cutoffOffsetPerc = 0.1f;
    [SerializeField] private GameObject uiElements;
    [SerializeField] private GameObject uiBackground;
    [SerializeField] private float hoverTime = 0.25f;
    [SerializeField] private TextMeshProUGUI _tmp;
    
    private string _stringKey;
    private float _tooltipBGWidth;
    private float _hoverTimer = -1.0f;

    private void Awake()
    {
        uiElements.SetActive(false);
        _stringKey = _tmp.text;
    }


    void Update()
    {
        if (_hoverTimer > 0.0f)
        {
            _hoverTimer -= Time.deltaTime;
            if(_hoverTimer < 0.0f)
                uiElements.SetActive(true);
        }
        
        if (uiElements.activeInHierarchy)
        {
            float widthRatio = Screen.width / 1920.0f;
            
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector2 cutoffOffset = Vector2.zero;
            if (mousePos.x + uiBackground.GetComponent<RectTransform>().rect.width * widthRatio > Screen.width)
            {
                cutoffOffset.x = - (Screen.width * cutoffOffsetPerc);
            }
            
            Vector2 hoverOffset = Vector2.zero;

            hoverOffset.x = Screen.width * tooltipOffsetPerc.x;
            hoverOffset.y = Screen.height * tooltipOffsetPerc.y;

            uiElements.transform.position = Mouse.current.position.ReadValue()+ hoverOffset + cutoffOffset;
        }
    }

    public void ShowUITooltip()
    {
        _hoverTimer = hoverTime;
        _tmp.text = LocalizationUtility.GetLocalizedString(_stringKey);
    }

    public void HideUITooltip()
    {
        _hoverTimer = -1.0f;
        uiElements.SetActive(false);
    }
}
