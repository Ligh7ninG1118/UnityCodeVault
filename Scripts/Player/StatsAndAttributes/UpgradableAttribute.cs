using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct UpgradableAttribute
{
    public float baseVal;
    public float currentVal;
    public event Action OnValueUpgraded;

    public UpgradableAttribute(float val)
    {
        baseVal = currentVal = val;
        OnValueUpgraded = null;
    }

    public void UpgradeValue(float percentage)
    {
        currentVal += baseVal * percentage * 0.01f;
        if (OnValueUpgraded != null)
            OnValueUpgraded();
    }
}