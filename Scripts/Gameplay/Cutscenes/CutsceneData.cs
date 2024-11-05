using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityEnums;

[CreateAssetMenu(fileName = "CutsceneData", menuName = "ScriptableObjects/CutsceneData")]
public class CutsceneData : ScriptableObject
{
    public List<CutsceneSlide> cutsceneSlides;
    public CutsceneID cutsceneID;
    public Color imageMaskColor;
    public bool shouldFadeIn = true;
}
