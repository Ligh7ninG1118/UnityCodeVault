using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Unity.Serialization.Json;
using Unity.VisualScripting;
using UnityEngine.SceneManagement;
using UtilityEnums;

public static class SaveLoadManager
{
    public static List<ISavable> savableElements = new List<ISavable>();
    public static string path = Application.persistentDataPath + "/";
    public static string fileExt = ".oxy";
    public static SaveData saveData = new SaveData();
    
    public static void SaveToFile(int slotID)
    {
        if (saveData == null)
            saveData = new SaveData();
        
        saveData.saveDict.Clear();
        
        saveData.saveDict.Add("s_SavingDate", DateTime.Now.Date.ToString("d"));
        saveData.saveDict.Add("s_SavingTime", DateTime.Now.ToShortTimeString());
        saveData.saveDict.Add("id_slotID", slotID);

        foreach (var savableElement in savableElements)
        {
            var list = savableElement.SaveElement();
            foreach (var tuple in list)
            {
                saveData.saveDict.Add(tuple.Item1, tuple.Item2);
            }
        }
        Debug.Log("Saving Time: " + DateTime.Now);

        string fileName = "slot" + slotID;
        File.WriteAllText(path + fileName + fileExt, JsonSerialization.ToJson(saveData));
    }

    public static void LoadFromSaveData(SaveData inSaveData)
    {
        saveData = inSaveData;
        ReloadScene();
    }

    public static List<SaveData> ReadAllSaveFiles()
    {
        List<SaveData> saveFilesList = new List<SaveData>();

        var fileInfo = (new DirectoryInfo(path)).GetFiles();
        foreach (var file in fileInfo)
        {
            if (file.Extension == fileExt && file.Name.Contains("slot"))
            {
                saveFilesList.Add(JsonSerialization.FromJson<SaveData>(File.ReadAllText(file.FullName)));
            }
        }
        
        return saveFilesList;
    }

    public static void RemoveSaveFile(int slotID)
    {
        string fileName = "slot" + slotID;
        string fullPath = path + fileName + fileExt;
        
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        else
        {
            Debug.LogError("Cant find save file");
        }
    }

    public static void ClearCurrentSaveFile()
    {
        saveData = null;
    }

    private static void ReloadScene()
    {
        InventoryManager.Instance?.ClearAllItems();
        Time.timeScale = 1f;
      
        SceneManager.LoadScene("_Ground_Startup");
    }
    
}
