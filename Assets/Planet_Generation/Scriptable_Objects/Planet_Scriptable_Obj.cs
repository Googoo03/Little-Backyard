using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;



[System.Serializable] public struct mesh_pair
{
    [SerializeField] public Mesh mesh;
    [SerializeField] public Material mat;
    [SerializeField] public Resource_Preset resource;
    [SerializeField] public int poissonK;
    [SerializeField] public int poissonNum;
    [SerializeField] public int poissonRadius;

    public mesh_pair(Mesh _mesh, Material _mat, Resource_Preset _resource, int _k, int _num, int _radius)
    { 
        mesh = _mesh;
        mat = _mat;
        resource = _resource;
        poissonK = _k;
        poissonNum = _num;
        poissonRadius = _radius;
    }
}

[CreateAssetMenu(menuName = "ScriptableObject/Planet_Type")]
public class Planet_Scriptable_Obj : ScriptableObject
{
    [SerializeField] protected List<mesh_pair> mesh_list;
    // Start is called before the first frame update
    public List<mesh_pair> getMesh_List() { return  mesh_list; }
}
