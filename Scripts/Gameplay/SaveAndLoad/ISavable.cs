using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = System.Object;

public interface ISavable
{
    public void Subscribe()
    {
        SaveLoadManager.savableElements.Add(this);
        // If had a save loaded before initialization, load it
        if(SaveLoadManager.saveData != null && SaveLoadManager.saveData.saveDict.Count != 0)
            LoadElement(SaveLoadManager.saveData);
    }
    public void Unsubscribe()
    {
        SaveLoadManager.savableElements.Remove(this);
    }
    public List<Tuple<string, Object>> SaveElement();
    public void LoadElement(SaveData saveData);
}
