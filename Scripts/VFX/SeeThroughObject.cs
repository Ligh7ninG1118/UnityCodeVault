using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


// Used for fading out object when it's blocking the view from the camera to the character
// Need to set the DoFade property to true in another camera controlling class
public class SeeThroughObject : MonoBehaviour
{
    [SerializeField][Range(0f, 1.0f)] private float transparencyWhenOccluded = 0.2f;
    [SerializeField][Range(0f, 2.0f)] private float transitionTimeToTransparent = 0.5f;
    [SerializeField][Range(0f, 2.0f)] private float transitionTimeToOpaque = 0.5f;


    [Tooltip("Set this to the alpha variable name used in this object's shader/material")]
    [SerializeField] private string shaderTransparencyVar = "_AlphaMultiplier";
    
    [HideInInspector] public bool DoFade {set;};
    private List<Material> _materials;
    private float _targetAlpha = 1.0f;
    private float _currentAlpha = 1.0f;
    private float _resetTimer = -1.0f;


    private void Awake()
    {
        _materials = new List<Material>();

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            _materials.AddRange(r.materials);
        }
    }

    void Update()
    {
        if (doFade)
        {
            doFade = false;
            _targetAlpha = transparencyWhenOccluded;
            _resetTimer = 0.0f;
        }

        // Only counting the timer when it's enabled (not -1.0f)
        if (_resetTimer >= 0.0f)
            _resetTimer += Time.deltaTime;

        if (_resetTimer >= transitionTimeToOpaque)
        {
            _targetAlpha = 1.0f;
            _resetTimer = -1.0f;
        }

        if (_currentAlpha != _targetAlpha)
        {
            float tempAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, (1.0f / transitionTimeToTransparent) * Time.deltaTime);
            _currentAlpha = tempAlpha;
            foreach (var m in _materials)
            {
                m.SetFloat(shaderTransparencyVar, tempAlpha);
            }
        }
    }
}
