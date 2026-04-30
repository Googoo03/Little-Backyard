using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SparseVoxelOctree;
using faces;

public class PlanetWrapper : MonoBehaviour
{
    //Faces must be assigned in this order X -X Y -Y Z -Z
    [SerializeField] private SVOTest SVOParent;
    [SerializeField] private int planetRadius;
    [SerializeField] Atmosphere_Manager atmosphere_Manager;

    public Face[] neighbors;

    void Awake()
    {
        //Initialize SVO settings
        SVOParent.SetPatchSize(planetRadius * 2);
        SVOParent.transform.localPosition = -Vector3.one * planetRadius;

        //Initialize Atmosphere Manager Settings
        atmosphere_Manager.SetPlanetRadius(planetRadius * 0.8f);
    }

    public Atmosphere_Manager GetAtmosphere_Manager()
    {
        return atmosphere_Manager;
    }

    public float GetPlanetRadius() { return planetRadius; }
}
