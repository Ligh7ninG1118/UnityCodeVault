using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UtilityEnums;

public class SceneObjectManager : MonoBehaviour
{
    public static SceneObjectManager Instance;

    public bool allNPCInitialized = false;
    
    public GameObject playerRef;
    public GameObject spiderRef;
    public GameObject spiderBaseRef;

    public List<GameObject> npcList;
    public Dictionary<CharacterType, NPCTeammate> npcDict;
    public List<GameObject> mobList;

    public List<GameObject> spawnedMobList;

    public List<GameObject> resourceList;

    public UIMainCanvas mainCanvas;
    
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(this);
        }
        Instance = this;

        npcDict = new Dictionary<CharacterType, NPCTeammate>();
        spawnedMobList = new List<GameObject>();
    }

    public void AddToNPCList(GameObject go)
    {
        if (!npcList.Contains(go))
        {
            npcList.Add(go);

            var npc = go.GetComponent<NPCTeammate>();
            npcDict.Add(npc.characterType, npc);

            if (npcList.Count >= 3)
                allNPCInitialized = true;
        }
    }

    public int GetMissingNPCCOunt()
    {
        int count = 0;
        foreach (var pair in npcDict)
        {
            if (pair.Value.isMIA)
            {
                count++;
            }
        }

        return count;
    }
    
    public void AddToMobList(GameObject go)
    {
        if(!mobList.Contains(go))
            mobList.Add(go);
    }
    
    public void AddToResourceList(GameObject go)
    {
        if(!resourceList.Contains(go))
            resourceList.Add(go);
    }
    
    public void RemoveFromNPCList(GameObject go)
    {
        if (npcList.Contains(go))
            npcList.Remove(go);
    }
    
    public void RemoveFromMobList(GameObject go)
    {
        if (mobList.Contains(go))
            mobList.Remove(go);
    }
    
    public void RemoveFromResourceList(GameObject go)
    {
        if (resourceList.Contains(go))
            resourceList.Remove(go);
    }

    public void RemoveFromSpawnedMobList(GameObject go)
    {
        spawnedMobList.Remove(go);
        if (spawnedMobList.Count == 0)
        {
            AudioManager.Instance.CrossFadeMusic("ExploringBGM");
            AudioManager.Instance.StopSFXLoop("MonsterAlert");
            
            GameStateManager.Instance.isPlayerInGroundCombat = false;
            // player barks after all enemies are cleared
            ExploreController.TriggerAllEnemiesClearedEvent();
        }
    }

    public bool IsSpawnedMobEmpty()
    {
        return spawnedMobList.Count == 0;
    }
}
