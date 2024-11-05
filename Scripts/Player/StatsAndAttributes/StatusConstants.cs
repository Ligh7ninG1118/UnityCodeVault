using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityEnums;

[CreateAssetMenu(fileName = "StatusConstants", menuName = "ScriptableObjects/StatusConstants")]
public class StatusConstants : ScriptableObject
{
    public StatusType statusType;
    public float maxValue;
    [Tooltip("Determine when this status should enter\"Low State\"")]
    public float lowStateThreshold;
}
