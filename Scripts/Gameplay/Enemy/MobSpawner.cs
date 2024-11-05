using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;
using UtilityEnums;
using UtilityFunc;

public class MobSpawner : MonoBehaviour, ISavable
{
    [Serializable]
    public struct SpawnMonsterInfo
    {
        public GameObject monsterPrefab;
        public int spawnCountMin;
        public int spawnCountMax;
    }

    [Serializable]
    public struct SpawnMonsterInfoList
    {
        public SpawnMonsterInfo[] infoList;
    }
    
    [Header("Spawn")]
    [Tooltip("Monster spawn infos for different waves of monsters")]
    public SpawnMonsterInfoList[] waveInfos;
    [HideInInspector] public MobSpawnStage mobSpawnStage;
    
    [Tooltip("Cooldown time in seconds before spawning next wave of monster")]
    [SerializeField][Range(0.1f, 600.0f)] private float spawnCooldown = 60.0f;
    
    [Tooltip("Cooldown time in seconds before spawning next wave of monster")]
    [SerializeField][Range(0.1f, 4200.0f)] private float firstSpawnCooldown = 60.0f;

    [Tooltip("The radius at which the monster will spawn (monsters spawn around this spawner in a circle with some radius")] 
    [SerializeField] [Range(0.0f, 60.0f)] private float spawnPosDeviation = 60f;
    
    [Tooltip("Add a small delay when spawning monster")] 
    [SerializeField] [Range(0.0f, 5.0f)] private float spawnTimeMinDelay = 0.5f;
    [Tooltip("Add a small delay when spawning monster")] 
    [SerializeField] [Range(0.0f, 5.0f)] private float spawnTimeMaxDelay = 1.5f;

    [SerializeField] private NavMeshAgent girlAgent;

    
    [Header("Leveling Up")]
    [Tooltip("Determine how monsters become more powerful each wave. Additive means linear growth. Multiply means exponential growth")]
    [SerializeField] private LevelingMethod levelingMethod;
    
    [Tooltip("Factor to use when leveling the monster's power level.")]
    [SerializeField][Range(0.01f, 2.0f)] private float powerLevelScalingFactor = 1.0f;
    
    private float _powerLevel = 1.0f;
    private float _cooldownTimer = 0.0f;
    private Camera _mainCam;

    private bool _isSpawningInProgress;

    private int _currentWaveCount;

    private void Awake()
    {
        ((ISavable)this).Subscribe();
    }

    private void OnDestroy()
    {
        ((ISavable)this).Unsubscribe();
    }

    private void Start()
    {
        _mainCam = GameObject.FindGameObjectWithTag("ExploreMainCamera").GetComponent<Camera>();
        
        // don't spawn right away
        _cooldownTimer = firstSpawnCooldown;
        mobSpawnStage = MobSpawnStage.InProgress;
    }


    private void Update()
    {
        // Pause spawn logic when in dialogue
        if(DialogueQuestManager.Instance != null && DialogueQuestManager.Instance.isInDialogue)
            return;
        
        // do not generate enemies when the cliff tutorial is not completed yet
        if (!GameStateManager.Instance.hasCliffTutorialLevelCompleted)
        {
            return;
        }
        
        // should continue the spawn timer after the last wave is finished
        if (!_isSpawningInProgress && SceneObjectManager.Instance.IsSpawnedMobEmpty())
        {
            _cooldownTimer -= Time.deltaTime;
            
            // transition back to in progress spawn stage
            if (mobSpawnStage == MobSpawnStage.Spawned)
            {
                mobSpawnStage = MobSpawnStage.InProgress;
                SceneObjectManager.Instance.mainCanvas.newEnemyAlert.SetCorrectState(mobSpawnStage);
            }
            
            // transition to imminent mob spawn stage
            if (_cooldownTimer is > 0.0f and <= 60.0f && mobSpawnStage == MobSpawnStage.InProgress)
            {
                mobSpawnStage = MobSpawnStage.Imminent;
                SceneObjectManager.Instance.mainCanvas.newEnemyAlert.SetCorrectState(mobSpawnStage);
            }
        }
        
        // Spawn new wave
        if (_cooldownTimer <= 0.0f && !_isSpawningInProgress)
        {
            // transition to spawned mob spawn stage
            if (mobSpawnStage == MobSpawnStage.Imminent)
            {
                mobSpawnStage = MobSpawnStage.Spawned;
                SceneObjectManager.Instance.mainCanvas.newEnemyAlert.SetCorrectState(mobSpawnStage);
            }
            
            _isSpawningInProgress = true;
            StartCoroutine(SpawnMobs(_currentWaveCount));

            _currentWaveCount++;
            
            // cap at last wave
            _currentWaveCount = Mathf.Min(_currentWaveCount, waveInfos.Length - 1);
            
            // don't level up for now
            // LevelingUp();
        }
        
        // calculate progress towards spawning enemies
        float progress = spawnCooldown - _cooldownTimer;
        if (progress < 0f)
        {
            progress = 1.0f;
        }
        
        float enemyAlertFillRate = progress / spawnCooldown;
        
        // feed enemy alert info to dialogue system
        if (enemyAlertFillRate is <= 0.99f and >= 0.5f)
        {
            DialogueQuestManager.Instance.isEnemyAlertMed = true;
        }
        else
        {
            DialogueQuestManager.Instance.isEnemyAlertMed = false;
        }

        if (enemyAlertFillRate > 0.99f)
        {
            DialogueQuestManager.Instance.isEnemyAlertHigh = true;
        }
        else
        {
            DialogueQuestManager.Instance.isEnemyAlertHigh = false;
        }
        
        SceneObjectManager.Instance.mainCanvas.newEnemyAlert.UpdateEnemySpawnTimer(_cooldownTimer);
    }

    private IEnumerator SpawnMobs(int waveNum)
    {
        // invalid wave infos
        if (waveNum >= waveInfos.Length)
        {
            yield break;
        }
        
        // first wave show shoot tutorial
        if (waveNum == 0 && GameStateManager.Instance.currentActivePlayerSet == PlayerSetType.Explore)
        {
            TutorialManager.Instance.ShowShootingTutorial();
        }
        
        bool spawnedAnything = false;
        int actualNumSpawned = 0;
        // support spawning a mixture of monsters
        for (int i = 0; i < waveInfos[waveNum].infoList.Length; i++)
        {
            int spawnCount = Random.Range(waveInfos[waveNum].infoList[i].spawnCountMin, waveInfos[waveNum].infoList[i].spawnCountMax);
            
            for (int j = 0; j < spawnCount; j++)
            {
                // random angle on the unit circle
                float randomAng = Random.Range(0f, 2f * Mathf.PI);
                float cosAng = Mathf.Cos(randomAng);
                float sinAng = Mathf.Sin(randomAng);
                Vector3 spawnDir = Vector3.right * cosAng + Vector3.forward * sinAng;
                Vector3 spawnOffset = spawnPosDeviation * spawnDir;
                Vector3 potentialSpawnPoint = transform.position + spawnOffset;
                
                // check if the spawn point is visible on the screen, only spawn if it is not
                // Vector3 viewPos = _mainCam.WorldToViewportPoint(potentialSpawnPoint);
                // bool isInView = viewPos.x is > 0f and < 1f && viewPos.y is > 0f and < 1f;
                
                // check if the spawn point is inside something else
                bool noCollision = false;
                Collider[] results = new Collider[] { };
                Physics.OverlapSphereNonAlloc(potentialSpawnPoint + 0.5f * Vector3.up, 0.4f, results);
                if (results.Length <= 0)
                {
                    noCollision = true;
                }
                
                // check if the spawn point is reachable from current position
                NavMeshPath path = new NavMeshPath();
                bool isReachable = girlAgent.CalculatePath(potentialSpawnPoint, path);
                
                // spawn the monster if its position is not inside other object AND is reachable by girl AND is on navmesh
                if (noCollision && isReachable && NavMeshUtility.isPositionOnNavMesh(potentialSpawnPoint))
                {
                    // spawn the monster on the circle
                    GameObject mob = Instantiate(waveInfos[waveNum].infoList[i].monsterPrefab, potentialSpawnPoint, quaternion.identity);
                    
                    // fade in monster
                    SpriteRenderer sp = mob.GetComponentInChildren<SpriteRenderer>();
                    sp.color = new Color(sp.color.r, sp.color.g, sp.color.b, 0.0f);
                    sp.DOFade(1.0f, 2.0f);
                    
                    actualNumSpawned++;
                    SceneObjectManager.Instance.spawnedMobList.Add(mob);
                    var monster = mob.GetComponent<Monster>();
                    monster.movingSpeed *= _powerLevel;
                    monster._combatant.attackPower *= _powerLevel;
                    monster._combatant.attackRange *= _powerLevel;
                    monster._combatant.attackInterval /= _powerLevel;
                    
                    // will only wait if we actually spawned a monster
                    yield return new WaitForSeconds(Random.Range(spawnTimeMinDelay, spawnTimeMaxDelay));

                    spawnedAnything = true;
                }
            }
        }

        if (spawnedAnything)
        {
            _cooldownTimer = spawnCooldown;
            DialogueQuestManager.Instance.hasMonsterWaveJustSpawned = true;
            
            // fade to combat music if there's no previous monster alive in scene
            if (SceneObjectManager.Instance.spawnedMobList.Count == actualNumSpawned && GameStateManager.Instance.currentActivePlayerSet != PlayerSetType.Climbing)
            {
                AudioManager.Instance.CrossFadeMusic("GroundBattleBGM");
                GameStateManager.Instance.isPlayerInGroundCombat = true;
            }
        }
        
        _isSpawningInProgress = false;
    }

    private void LevelingUp()
    {
        switch (levelingMethod)
        {
            case LevelingMethod.Additive:
                _powerLevel += powerLevelScalingFactor;
                break;
            case LevelingMethod.Multiply:
                _powerLevel *= powerLevelScalingFactor;
                break;
        }
    }

    [ContextMenu("Spawn Mob NOW")]
    private void SpawnMobNow()
    {
        _cooldownTimer = 3f;
    }

    public List<Tuple<string, dynamic>> SaveElement()
    {
        List<Tuple<string, dynamic>> elements = new List<Tuple<string, dynamic>>();
        
        elements.Add(new Tuple<string, dynamic>("i_CurrentWave", _currentWaveCount));
        elements.Add(new Tuple<string, dynamic>("f_CooldownTimer", _cooldownTimer));

        return elements;
    }

    public void LoadElement(SaveData saveData)
    {
        _currentWaveCount = Convert.ToInt32(saveData.saveDict["i_CurrentWave"]);
        _cooldownTimer = (float)Convert.ToDouble(saveData.saveDict["f_CooldownTimer"]);
    }
}
