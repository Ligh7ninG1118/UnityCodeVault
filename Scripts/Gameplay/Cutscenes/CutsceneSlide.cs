using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "CutsceneSlide", menuName = "ScriptableObjects/CutsceneSlide")]
public class CutsceneSlide: ScriptableObject
{
    [Header("Content")] 
    public Sprite imageSprite;
    public Sprite imageSpriteCN;
    public string subtitleText;
    public string musicToPlay;
    public string SFXToPlay;

    [Header("Customizable")] 
    public float subtitlePlayDelay = 0.5f;
    public float SFXPlayDelay = 0.5f;
    public bool shouldUseOverrideForCrossFadeTime = false;
    public float musicCrossFadeTimeOverride = 6.0f;
    public float fadeInTime = 0.5f;
    public float fadeOutTime = 0.5f;
}
