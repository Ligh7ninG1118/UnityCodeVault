using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityEnums;

[RequireComponent(typeof(DropTable))]
public abstract class Drop: MonoBehaviour
{
    [Tooltip("Master chance to execute this drop")]
    [SerializeField][Range(0.0f, 1.0f)] protected float unitDropChance;

    public WorldObjectRegenerationStage dropLifeStage;
    
    public abstract List<ItemObject> GenerateDrop();
}
