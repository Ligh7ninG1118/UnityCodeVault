using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class RaycastManager : MonoBehaviour
{
    public enum RaycastBehavior
    {
        ReservedDefault = 0,
        
    }
    
    public static RaycastManager Instance;
    
    private Camera _mainCam;
    private UIRaycastInteractor _uiRaycaster;
    private UIMainCanvas _mainCanvas;
    private TextMeshProUGUI _mouseHoverPrompt;
    private RaycastHit[] _hitResults = new RaycastHit[100];
    private Ray _ray;

    private bool _hasInit = false;
    
    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("Two RaycastManager in the scene");
        }
        Instance = this;
        
        _mainCam = GameObject.FindGameObjectWithTag("ExploreMainCamera").GetComponent<Camera>();
    }

    private void Start()
    {
        StartCoroutine((Util.ConditionalCallbackTimer(
            () => SceneObjectManager.Instance.mainCanvas != null,
            () =>
            {
                _mainCanvas = SceneObjectManager.Instance.mainCanvas;
                _uiRaycaster = _mainCanvas.GetComponent<UIRaycastInteractor>();
                _mouseHoverPrompt = _mainCanvas.mouseHoverPrompt.GetComponent<TextMeshProUGUI>();
                _hasInit = true;
            }
        )));
    }

    private void Update()
    {
        if(!_hasInit)
            return;

        ProcessSceneRaycast();
    }

    private void ProcessSceneRaycast()
    {
        ResetHoverPrompt();
        _uiRaycaster.mouseOverObject = null;
        _ray = _mainCam.ScreenPointToRay(Input.mousePosition);
        _hitResults = Physics.RaycastAll(_ray);
        if (_hitResults.Length == 0)
            return;
        
        if(ClimbController.Instance && ClimbController.Instance.gameObject.activeInHierarchy)
            return;
        
        if(_mainCanvas.shouldBlockUI)
            return;

        Transform targetCache = null;
        IRaycastable targetInterfaceCache = null;
            
        foreach (var hitResult in _hitResults)
        {
            if(hitResult.transform == null)
                continue;
            
            if (hitResult.transform.TryGetComponent<IRaycastable>(out IRaycastable raycastTarget))
            {
                if (targetCache == null)
                {
                    targetCache = hitResult.transform;
                    targetInterfaceCache = raycastTarget;
                }
                else if (hitResult.transform.CompareTag("NPC"))
                {
                    targetCache = hitResult.transform;
                    targetInterfaceCache = raycastTarget;
                }
                else
                {
                    Debug.Log("This should never happen");
                }
            }
        }

        if (targetCache != null)
        {
            if (targetInterfaceCache.ShouldShowPrompt(_mainCanvas, _uiRaycaster))
            {
                SetHoverPrompt(targetInterfaceCache.GetHoverPrompt(), targetInterfaceCache.hoverPromptOffset);
                _uiRaycaster.mouseOverObject = targetCache.transform.gameObject;
                        
                if(targetInterfaceCache.canClickWithPrompt)
                    ProcessClickRaycast(targetInterfaceCache);
            }
            else
            {
                if(targetInterfaceCache.canClickWithoutPrompt)
                    ProcessClickRaycast(targetInterfaceCache);
            }
        }
    }

    private void ProcessClickRaycast(IRaycastable raycastTarget)
    {
        if (Input.GetMouseButtonDown(0))
        {
            raycastTarget.OnClickAction(_mainCanvas, _uiRaycaster, 0);
        }
        else if(Input.GetMouseButtonDown(1))
        {
            raycastTarget.OnClickAction(_mainCanvas, _uiRaycaster, 1);
        }
    }
    
    private void ResetHoverPrompt()
    {
        _mouseHoverPrompt.gameObject.SetActive(false);
        _mouseHoverPrompt.text = "";
    }

    private void SetHoverPrompt(string text, Vector2 offset)
    {
        _mouseHoverPrompt.text = text;
        _mouseHoverPrompt.gameObject.SetActive(true);
        _mouseHoverPrompt.transform.position =
            Mouse.current.position.ReadValue() + offset;
    }
}
