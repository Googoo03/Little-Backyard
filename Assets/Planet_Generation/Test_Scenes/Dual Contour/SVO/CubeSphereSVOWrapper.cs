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
    [SerializeField] Features_Manager features_Manager;

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

    public Features_Manager GetFeatures_Manager()
    {
        return features_Manager;
    }

    public SVOTest GetSVOTest()
    {
        return SVOParent;
    }

    public float GetPlanetRadius() { return planetRadius; }
    public Vector3 GetPlanetOffset() { return -Vector3.one * planetRadius; }
}
