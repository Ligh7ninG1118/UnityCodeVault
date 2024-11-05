using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class SeeThroughObject : MonoBehaviour
{
    [SerializeField][Range(0f, 1.0f)] private float transparencyWhenOccluded = 0.2f;
    [SerializeField][Range(0f, 2.0f)] private float transitionTimeToTransparent = 0.5f;
    [SerializeField][Range(0f, 2.0f)] private float transitionTimeToOpaque = 0.5f;

    [SerializeField] private string shaderTransparencyVar = "_AlphaMultiplier";
    
    [HideInInspector] public bool doFade = false;
    private List<Material> materials;
    private float targetAlpha = 1.0f;
    public float currentAlpha = 1.0f;
    private float resetTimer = -1.0f;

    [SerializeField] private bool dynamicDepthWrite = false;

    private void Awake()
    {
        materials = new List<Material>();

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if(!r.gameObject.name.Contains("VFX"))//avoid modifying vfx renderer
                materials.AddRange(r.materials);
        }
            
    }

    void Update()
    {
        if (doFade)
        {
            doFade = false;
            targetAlpha = transparencyWhenOccluded;
            resetTimer = 0.0f;
        }

        // Only counting the timer when it's enabled (not -1.0f)
        if (resetTimer >= 0.0f)
            resetTimer += Time.deltaTime;

        if (resetTimer >= transitionTimeToOpaque)
        {
            targetAlpha = 1.0f;
            resetTimer = -1.0f;
        }

        if (currentAlpha != targetAlpha)
        {
            float tempAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, (1.0f / transitionTimeToTransparent) * Time.deltaTime);
            currentAlpha = tempAlpha;
            foreach (var m in materials)
            {
                m.SetFloat(shaderTransparencyVar, tempAlpha);
            }
        }

        //for render layering
        SortRenderLayer();
        UpdateDepthWrite();
    }

    private void UpdateDepthWrite()
    {

        if(dynamicDepthWrite)
            foreach (var m in materials)
            {
                if (currentAlpha >= transparencyWhenOccluded + (1-transparencyWhenOccluded)/10)
                {
                    m.SetInt("_ZWrite", 1);
                }
                else
                {
                    m.SetInt("_ZWrite", 0);
                }
            }
    }

    private void SortRenderLayer() {

        SortingGroup sg;
        if (TryGetComponent<SortingGroup>(out sg))
        {
            if (currentAlpha >= 0.9999)
            {
                sg.enabled = false;//opaque
            }
            else
            {
                sg.enabled = true;//transparent
            }
        }
    }
}
