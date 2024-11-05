using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityEnums;

[CreateAssetMenu(fileName = "RawToCookedFoodData", menuName = "ScriptableObjects/RawToCookedFoodData")]
public class RawToCookedFoodData : ScriptableObject
{
    [Serializable]
    public struct RawToCooked
    {
        public ConsumableType consumableType;
        public GameObject RawFoodPrefab;
        public GameObject CookedFoodPrefab;
    }

    public List<RawToCooked> rawToCookedList;
}
