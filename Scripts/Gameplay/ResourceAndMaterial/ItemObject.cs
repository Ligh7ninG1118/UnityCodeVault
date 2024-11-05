using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityEnums;

//[RequireComponent(typeof(Rigidbody))]
public class ItemObject : MonoBehaviour
{
    public ItemData itemData;
    public GameObject itemPrefab;
    [SerializeField] private float preventCollectDelay = 0.5f;
    [SerializeField] private float flyingTime = 0.25f;
    [SerializeField] private float flyTargetYOffset = 0.3f;
    [SerializeField] private float autoDestroyTime = 180.0f;
    [SerializeField] private bool isClimbingScene;
    private Rigidbody _rb;
    [HideInInspector] public bool movingToPlayer = false;
    private Transform _playerTransform;
    private Vector3 smoothVelo = Vector3.zero;
    private bool isCollected = false;

    private float _preventCollectTimer;

    public ResourceType type;
    
    private void Start()
    {
        _playerTransform = ExploreController.Instance.transform;

        _preventCollectTimer = preventCollectDelay;
        
        Destroy(gameObject, autoDestroyTime);
    }

    private void Update()
    {
        if (_preventCollectTimer > 0.0f)
            _preventCollectTimer -= Time.deltaTime;
        
        // debug test
        if (DebugManager.Instance != null && DebugManager.Instance.isDebugMode && Input.GetKeyDown(KeyCode.R))
        {
            Collect();
        }
        
        if (movingToPlayer && _preventCollectTimer <= 0.0f)
        {
            Vector3 targetPos = _playerTransform.position;
            targetPos.y += flyTargetYOffset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref smoothVelo, flyingTime);
        }
    }

    public void Collect(bool isCollectedByPlayer = true)
    {
        if(isCollected)
            return;

        if (InventoryManager.Instance.TryAddItem(itemData, isCollectedByPlayer))
        {
            isCollected = true;

            if (GameStateManager.Instance && GameStateManager.Instance.currentActivePlayerSet == PlayerSetType.Explore)
            {
                switch (type)
                {
                    case ResourceType.Bush:
                        AudioManager.Instance.PlaySFXOneShot3DAtPosition("CollectLight", transform.position);
                        break;
                    case ResourceType.Mineral:
                        AudioManager.Instance.PlaySFXOneShot3DAtPosition("CollectHeavy", transform.position);
                        break;
                    case ResourceType.Tree:
                        AudioManager.Instance.PlaySFXOneShot3DAtPosition("CollectMedium", transform.position);
                        break;
                }
            }
            else
            {
                AudioManager.Instance.PlaySFXOneShot3DAtPosition("CliffResourcesCollected", transform.position, 0.5f);
            }
            
            Destroy(gameObject);
        }
        else
        {
            movingToPlayer = false;
        }
    }

    // For Climbing scene, this is necessary for fuel flower collecting
    // For Explore scene, this will not execute tho
    // Let player collects items when walking over them
    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && !isCollected && _preventCollectTimer <= 0.0f)
        {
            Collect();
        }
    }
}
