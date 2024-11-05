using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UtilityEnums;

public class UIInteractionRing : MonoBehaviour
{
    public static UIInteractionRing Instance;
    
    [SerializeField] private GameObject moveOrderPrefab;
    private GameObject _moveOrderCursor;
    [SerializeField] private Canvas mainCanvas;

    [SerializeField] private GameObject elementGroup;
    
    /*[SerializeField] private Image talkText;
    [SerializeField] private Image patText;
    [SerializeField] private Image moveText;
    [SerializeField] private Image forageText;
    [SerializeField] private Image mineText;
    [SerializeField] private Image woodcutText;

    [SerializeField] private List<Image> defaultIcons;
    [SerializeField] private List<Image> highlightIcons;

    [SerializeField] private Image patImage;
    [SerializeField] private Image patHighlightImage;
    [SerializeField] private Image talkImage;
    [SerializeField] private Image talkHighlightImage;

    [SerializeField] private Sprite cooldownPat;
    [SerializeField] private Sprite cooldownPatHighlight;
    [SerializeField] private Sprite redDotPat;
    [SerializeField] private Sprite redDotPatHighlight;

    [SerializeField] private Sprite cooldownTalk;
    [SerializeField] private Sprite cooldownTalkHighlight;
    [SerializeField] private Sprite redDotTalk;
    [SerializeField] private Sprite redDotTalkHighlight;*/


    [SerializeField] private GameObject chatRedDotGO;
    [SerializeField] private GameObject followActiveGO;
    [SerializeField] private GameObject gatherActiveGO;
    
    public static bool isMoveOrderActive;
    private List<Image> _imageElements;
    public bool _canReceiveInput = false;

    private bool hasUsedRing = false;
    
    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("Two UIInteractionRing in the scene");
        }
        Instance = this;
    }

    /*public void SwitchTextDisplay(int index)
    {
        for (int i = 0; i < 6; i++)
        {
            if (index == i)
                _imageElements[i].gameObject.SetActive(true);
            else
                _imageElements[i].gameObject.SetActive(false);
        }
    }*/

    private void Update()
    {
        if(elementGroup.activeInHierarchy && _canReceiveInput && Input.GetKey(KeyCode.Mouse0))
        {
            HideRing();
        }

        if (GirlAI.Instance != null)
        {
            if(GirlAI.Instance.currentTask == GirlTask.Follow)
                followActiveGO.SetActive(true);
            else
                followActiveGO.SetActive(false);
            
            if(GirlAI.Instance.currentTask == GirlTask.FindingResource 
               || GirlAI.Instance.currentTask == GirlTask.MovingToResource 
               ||GirlAI.Instance.currentTask == GirlTask.GatheringResource)
                gatherActiveGO.SetActive(true);
            else
                gatherActiveGO.SetActive(false);
        }
    }

    public void AssignTask(int index)
    {
        switch (index)
        {
            case 0:
                GirlAI.Instance.TryGiveTask(GirlTask.InDialogue);
                break;
            case 1:
                Debug.LogError("Deprecated Task. Should never happen");
                break;
                GirlAI.Instance.TryGiveTask(GirlTask.Pat, true);
            case 2:
                GirlAI.Instance.TryGiveTask(GirlTask.Follow);
                AudioManager.Instance.PlaySFXOnUIOneShot("A1Follow");
                /*isMoveOrderActive = true;
                SpawnMovePointer();*/
                break;
            case 3:
                GirlAI.Instance.TryGiveTask(GirlTask.FindingResource);
                AudioManager.Instance.PlaySFXOnUIOneShot("A1Collect");
                break;
        }

        HideRing();
    }
    
    public void DeactivateMoveOrder()
    {
        _moveOrderCursor = null;
        Cursor.visible = true;
        isMoveOrderActive = false;
    }

    public void ShowRing()
    {
        if(elementGroup.activeInHierarchy)
            HideRing();
        else
        {
            elementGroup.SetActive(true);
            _canReceiveInput = false;
            StartCoroutine(Util.BoolCallbackTimer(0.2f, (canReceiveInput) => 
            {
                _canReceiveInput = true;
            }));

            /*foreach (var icon in defaultIcons)
            {
                icon.gameObject.SetActive(true);
            }

            foreach (var icon in highlightIcons)
            {
                icon.gameObject.SetActive(false);
            }

            SwitchTextDisplay(-1);*/
            
            AudioManager.Instance.PlaySFXOnUIOneShot("InteractionRingOn");
        }
        
    }

    public void HideRing()
    {
        elementGroup.SetActive(false);
        _canReceiveInput = false;
        
        SceneObjectManager.Instance.mainCanvas.shouldBlockUI = false;
        
        if (!hasUsedRing)
        {
            hasUsedRing = true;
            // CutsceneManager.Instance.KickstartCutscene(CutsceneID.Tutorial2);
        }
    }

    public void SwapTalkSprites(bool swapToCooldownSet)
    {
        /*if (swapToCooldownSet)
        {
            chatRedDotGO.SetActive(false);
        }
        else
        {
            chatRedDotGO.SetActive(true);
        }*/
    }

    /*public void SwapPatSprites(bool swapToCooldownSet)
    {
        if (swapToCooldownSet)
        {
            patImage.sprite = cooldownPat;
            patHighlightImage.sprite = cooldownPatHighlight;
        }
        else
        {
            patImage.sprite = redDotPat;
            patHighlightImage.sprite = redDotPatHighlight;
        }
    }*/
    
    public void SpawnMovePointer()
    {
        isMoveOrderActive = true;
        
        _moveOrderCursor = Instantiate(moveOrderPrefab, transform.position, Quaternion.identity);
        _moveOrderCursor.GetComponent<UIMoveOrderCursor>().Setup(mainCanvas, this);
                    
        // set move order cursor to be at system cursor position
        RectTransformUtility.ScreenPointToLocalPointInRectangle
        (mainCanvas.transform as RectTransform, Input.mousePosition, mainCanvas.worldCamera,
            out var movePos);
        _moveOrderCursor.transform.position = mainCanvas.transform.TransformPoint(movePos);
                    
        // parent the move order cursor to main canvas
        RectTransform rect = _moveOrderCursor.GetComponent<RectTransform>();
        rect.SetParent(mainCanvas.transform);
                    
        // hide the system cursor
        Cursor.visible = false;
    }

    public void SetPos(Vector3 worldPos)
    {
        if(!elementGroup.activeInHierarchy)
            return;

        var cam = GameObject.FindGameObjectWithTag("ExploreMainCamera");

        if (cam == null)
        {
            HideRing();
            return;
        }
        
        var mainCam = cam.GetComponent<Camera>();
        Vector3 screenPos= mainCam.WorldToScreenPoint(worldPos);
        
        var refResolution = transform.root.GetComponent<CanvasScaler>().referenceResolution;

        
        screenPos.z = 0.0f;
        screenPos.x = screenPos.x * (refResolution.x / mainCam.pixelWidth) - refResolution.x / 2.0f;
        screenPos.y = screenPos.y * (refResolution.y / mainCam.pixelHeight) - refResolution.y / 2.0f;
        
        GetComponent<RectTransform>().anchoredPosition  = screenPos;
    }

}
