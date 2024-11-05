using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Hat", menuName = "Hat")]
public class Hat : ScriptableObject
{
    public string hatName;
    public string hatDescription;
    public string hatLockedHint;
    public string hatUnlockedHint;

    public GameObject hatPrefab;
    public Sprite lockedSprite;
    public Sprite unlockedSprite;

    [HideInInspector] public GameObject HatGameObj;     //For storing runtime reference
    [HideInInspector] public bool isUnlocked = false;

}
