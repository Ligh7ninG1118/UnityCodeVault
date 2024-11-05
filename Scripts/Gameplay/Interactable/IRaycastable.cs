using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IRaycastable
{
    string hoverPrompt { get; set; }
    bool canClickWithPrompt { get; }
    bool canClickWithoutPrompt { get; }

    Vector2 hoverPromptOffset { get; }
    public void OnClickAction(UIMainCanvas mainCanvas, UIRaycastInteractor raycastInteractor, int button);
    public string GetHoverPrompt();
    public bool ShouldShowPrompt(UIMainCanvas mainCanvas, UIRaycastInteractor raycastInteractor);
}
