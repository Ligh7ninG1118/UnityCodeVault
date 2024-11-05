using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = System.Object;

[Serializable]
public class SaveData
{
    public Dictionary<string, Object> saveDict = new Dictionary<string, Object>();
}
