using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "ResourceData", menuName = "ScriptableObjects/ResourceData")]
public class ResourceData : ItemData
{
    public Resource resource;
    
    public override Item GetItem()
    {
        return resource;
    }
}
