using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public struct InstanceData
{
    public Mesh mesh;
    public Material mat;
    public float size;
    public Vector3 forward;
};

[CreateAssetMenu(menuName = "ScriptableObject/Star")]
public class StarScriptableObj : ScriptableObject
{
    public InstanceData instanceData;
}
