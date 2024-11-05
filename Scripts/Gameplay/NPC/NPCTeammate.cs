using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using UnityEngine;
using UnityEngine.AI;
using UtilityEnums;
using Random = UnityEngine.Random;
using UtilityEnums;

public class NPCTeammate : MonoBehaviour
{
    public enum NPCTask
    {
        Idle,
        FindResource,
        MoveToResource,
        GatherResource,
        Attack,
        WalkToPlace,
        FollowPlayer,
    }
    
    public enum NPCname
    {
        Red,
        Blue,
        Green,
        Purple,
    }

    public NPCname npcName;
    public CharacterType characterType;
    
    [Header("Movement")]
    [SerializeField] private float movingSpeed;
    [Tooltip("Backward movement speed when this NPC is hit")]
    [SerializeField] [Range(0.1f, 1.0f)] private float pushSpeedFromBeingHit = 1.0f;
    
    [Header("Resource Gathering")]
    [Tooltip("Range for NPC to search for resources")]
    [SerializeField][Range(0.1f, 50.0f)] private float resourceScanRange = 30.0f;
    [Tooltip("Cooldown time in seconds while collecting ")]
    [SerializeField] private float collectInterval = 0.5f;
    [Tooltip("Collect radius")]
    [SerializeField] private float collectRadius = 1.0f;

    [SerializeField][Range(0.01f, 3.0f)] private float miningDamage = 0.2f;
    [SerializeField][Range(0.01f, 3.0f)] private float woodChoppingDamage = 0.2f;
    [SerializeField][Range(0.01f, 3.0f)] private float foragingDamage = 0.2f;
    
    
    [Header("VFX")]
    [Tooltip("Blood VFX when NPC is in low health state")]
    [SerializeField] private GameObject NPCLowHealthBloodVFX;
    
    
    private CharacterStatus _status;
    private Combatant _combatant;
    private NavMeshAgent _agent;
    private AnimationControllerNPC _animator;

    [HideInInspector] public NPCTask currentTask;
    [HideInInspector] public ResourceType resourceType;
    [HideInInspector] public GameObject resourceTarget;
    [HideInInspector] public Vector3 movingVec;
    [HideInInspector] public bool isAutoUsingItem = false;
    [HideInInspector] public bool isMIA = false;
    
    private GameObject _playerRef;
    private NPCTask _prevTask;
    private Vector3 _followOffset = Vector3.zero; 
    private Vector3 _destination;
    
    private float _collectTimer = 0.0f;
    private bool _usingConsumable = false;
    private bool _hasPlayedAttackAnim = false;
    
    private List<GameObject> resourceList;

    private bool _isNPCStiff;

    private void Start()
    {
        _combatant = GetComponent<Combatant>();
        _status = GetComponent<CharacterStatus>();
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<AnimationControllerNPC>();

        _playerRef = ExploreController.Instance.gameObject;

        _followOffset.x = Random.Range(-2.0f, 2.0f);
        _followOffset.z = Random.Range(-2.0f, 2.0f);

        _agent.speed = movingSpeed;
        resourceList = SceneObjectManager.Instance.resourceList;
        
        StartCoroutine((Util.ConditionalCallbackTimer(
            () => SceneObjectManager.Instance != null,
            () => { SceneObjectManager.Instance.AddToNPCList(this.gameObject); }
        )));
        
        _status.healthRef.OnEnteringLowState += HealthEnteringLowStateEventHandler;
        _status.healthRef.OnExitingLowState += HealthExitingLowStateEventHandler;
        
        // event handler for health deplete
        _status.healthRef.OnDepleting += HealthDepleteEventHandler;

        _agent.stoppingDistance = _combatant.attackRange - 1.0f;
        
        _combatant.OnStartingAttack += StartingAttackEventHandler;
        _combatant.OnBeingHit += BeingHitEventHandler;
        _combatant.shouldDoAttack = true;

        _animator.OnAttackHit += AttackHitEventHandler;
    }


    private void OnDisable()
    {
        _status.healthRef.OnEnteringLowState -= HealthEnteringLowStateEventHandler;
        _status.healthRef.OnExitingLowState -= HealthExitingLowStateEventHandler;
    }

    private void OnDestroy()
    {
        SceneObjectManager.Instance.RemoveFromNPCList(this.gameObject);
        
        _status.healthRef.OnDepleting -= HealthDepleteEventHandler;
    }
    
    private void StartingAttackEventHandler()
    {
        if(!_hasPlayedAttackAnim)
        {
            _animator.SetAnimatorTrigger("Attack Collect");
            _hasPlayedAttackAnim = true;
        }
    }

    private void AttackHitEventHandler()
    {
        _combatant.ProcessAttack();
        
        _hasPlayedAttackAnim = false;
    }
    
    private void BeingHitEventHandler(float damage, Vector3 attackerPos)
    {
        if (!_combatant.isInvincible)
        {
            _combatant.isInvincible = true;
            _isNPCStiff = true;
            _agent.isStopped = true;
            
            _hasPlayedAttackAnim = false;
        
            _status.healthRef.ModifyValue(damage);
            _animator.SetAnimatorTrigger("NPC Hurt");
            
            Vector3 pushDirection = transform.position - attackerPos;
            pushDirection.y = 0f;

            StartCoroutine(NPCPushedBack(pushDirection, _combatant.invincibleDuration));
            Invoke(nameof(DisableInvincibility), _combatant.invincibleDuration);
        }
    }
    
    protected IEnumerator NPCPushedBack(Vector3 pushDirection, float pushDuration)
    {
        float timer = 0f;
        while (timer < pushDuration)
        {
            timer += Time.deltaTime;
            transform.position += pushDirection * (pushSpeedFromBeingHit * Time.deltaTime);

            yield return null;
        }

        _isNPCStiff = false;
        // re-enable nav mesh agent
        _agent.isStopped = false;
    }
    
    protected void DisableInvincibility()
    {
        _combatant.isInvincible = false;
    }

    private void HealthEnteringLowStateEventHandler()
    {
        _usingConsumable = true;
        
        // enable NPC low health VFX
        NPCLowHealthBloodVFX.SetActive(true);
        SceneObjectManager.Instance.mainCanvas.SetNPCLowHealthUIActive(characterType, true);
    }
    
    private void HealthExitingLowStateEventHandler()
    {
        _usingConsumable = false;
        
        // disable NPC low health VFX
        NPCLowHealthBloodVFX.SetActive(false);
        SceneObjectManager.Instance.mainCanvas.SetNPCLowHealthUIActive(characterType, false);
    }

    private void HealthDepleteEventHandler()
    {
        // modify UI to show death
        SceneObjectManager.Instance.mainCanvas.SetNPCDead(characterType);
        
        // inform dialogue quest system that npc is dead
        DialogueQuestManager.Instance.SetNPCAliveStatus(characterType, false);
        
        // SFX
        AudioSource ad = GetComponentInChildren<AudioSource>();
        if (ad)
        {
            AudioManager.Instance.PlaySFXOneShot3DAtPosition("NPCDie", transform.position);
        }
        
        // delay self destroy for SFX
        Destroy(gameObject);
    }
    
    // debug function for killing this NPC
    [ContextMenu("Kill NPC")]
    public void KillNPC()
    {
        _status.healthRef.ModifyValue(_status.healthRef.GetMaxValue());
    }
    
    private void Update()
    {
        if (DialogueQuestManager.Instance && DialogueQuestManager.Instance.isInDialogue && !_isNPCStiff)
        {
            _status.shouldPauseDebuff = true;
            return;
        }
        
        _status.shouldPauseDebuff = false;
        
        movingVec = _agent.velocity;
        
        if (_usingConsumable)
        {
            foreach (var item in InventoryManager.Instance.inventoryContent.ToList())
            {
                if (item.Key is Consumable)
                {
                    var consumable = item.Key as Consumable;
                    if(consumable.effect == ConsumableEffect.RecoverHealth)
                        InventoryManager.Instance.UseItemOnCharacter(item.Key, _status);
                }
            }
        }
        
        //monitoring combatant component
        //also additional check to avoid overwrite prevTask field
        if(_combatant.attackingTarget != null && currentTask != NPCTask.Attack)
            GiveTask(NPCTask.Attack);
        
        ExecuteTask();
        
        Vector3 adjustedPos = transform.position;
        adjustedPos.y = 0.0f;
        transform.position = adjustedPos;
    }

    public void GiveTask(NPCTask newTask)
    {
        _prevTask = currentTask;
        currentTask = newTask;

        //TODO: Or put this as a check under case NPCTask.GatherResource
        if (_prevTask == NPCTask.GatherResource)
            _prevTask = NPCTask.MoveToResource;
    }

    private void ExecuteTask()
    {
        switch (currentTask)
        {
            case NPCTask.Idle:
                //TODO: could've use isStopped, but it need to be reset in every other states
                _agent.SetDestination(transform.position);
                break;
            case NPCTask.FindResource:
                resourceTarget = null;
                resourceTarget = FindNearestResource(resourceType);
                if (resourceTarget == null)
                    GiveTask(NPCTask.FollowPlayer);
                else
                    GiveTask(NPCTask.MoveToResource);
                break;
            case NPCTask.MoveToResource:
                if(resourceTarget == null)
                    GiveTask(NPCTask.FindResource);

                //Vector3 resourceOffset = new Vector3(Random.Range(-1.5f, 1.5f), 0.0f, Random.Range(-1.5f, 1.5f));
                _agent.SetDestination(resourceTarget.transform.position);
                _agent.stoppingDistance = collectRadius;
                
                //Reached target
                if(CheckIfReachedDestination())
                    GiveTask(NPCTask.GatherResource);

                break;
            case NPCTask.GatherResource:
                if(resourceTarget == null)
                    GiveTask(NPCTask.FindResource);

                if (_collectTimer > 0.0f)
                {
                    _collectTimer -= Time.deltaTime;
                }
                else
                {
                    var _target = resourceTarget.GetComponent<ItemWorldObject>();
                    
                    float dmgVal = 0.0f;
                    switch (_target.type)
                    {
                        case ResourceType.Tree:
                            dmgVal = woodChoppingDamage;
                            break;
                        case ResourceType.Bush:
                            dmgVal = foragingDamage;
                            break;
                        case ResourceType.Mineral:
                            dmgVal = miningDamage;
                            break;
                    }
                    _target.ReduceCollectHP(dmgVal, transform);
                    _animator.SetAnimatorTrigger("Attack Collect");
                    _collectTimer = collectInterval;
                }
                
                break;
            case NPCTask.Attack:
                //if no target remaining, continue previous task
                if(_combatant.attackingTarget == null)
                {
                    GiveTask(_prevTask);
                }
                break;
            case NPCTask.WalkToPlace:
                _agent.SetDestination(_destination);
                
                if(CheckIfReachedDestination())
                    GiveTask(NPCTask.Idle);
                break;
            case NPCTask.FollowPlayer:
                _agent.SetDestination(_playerRef.transform.position + _followOffset);
                break;
        }
    }

    private GameObject FindNearestResource(ResourceType rType)
    {
        GameObject nearestResource = null;
        float nearestDistance = Mathf.Infinity;
        
        foreach (var item in resourceList)
        {
            if(item == null)
                continue;
            
            var resource = item.GetComponent<ItemWorldObject>();
            
            if(resource.type != rType)
                continue;

            float dis = (transform.position - item.transform.position).magnitude;
            
            if(dis > resourceScanRange)
                continue;
            
            if (dis < nearestDistance)
            {
                nearestDistance = dis;
                nearestResource = item;
            }
        }

        return nearestResource;
    }

    private bool CheckIfReachedDestination()
    {
        if (!_agent.pathPending)
        {
            if (_agent.remainingDistance <= _agent.stoppingDistance)
            {
                if (!_agent.hasPath || _agent.velocity.sqrMagnitude == 0f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void SetResourceType(ResourceType type)
    {
        resourceType = type;
    }
    

    public void SetDestination(Vector3 pos)
    {
        _destination = pos;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, resourceScanRange);
    }

    
}
