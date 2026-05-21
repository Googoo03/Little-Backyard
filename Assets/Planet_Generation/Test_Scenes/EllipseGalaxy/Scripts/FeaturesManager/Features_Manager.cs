using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FloraLSystem;

public class Features_Manager : MonoBehaviour
{
    //This class will have the l system namespace
    //upon provokation, it will generate a set of features (trees, rocks, etc)
    //from a set seed

    //When finished, the resulting meshes will exist in the manager, and can be pulled at will by other managers

    //Will the features manager need to generate more than once?
    //Let's say it does, any additional meshes generated will be sent to the object calling it.
    //We'll separate functions accordingly

    [SerializeField] public PlanetWrapper planetWrapper;
    [SerializeField] public Floating_Origin_Manager floating_Origin_Manager;
    [SerializeField] public Flora_L_System flora_L_System;

    [SerializeField] private Texture3D noiseTexture;
    [SerializeField] private Material floraMat;
    [SerializeField] private Mesh grass;

    //the seed will propagate from the planet, from the solar system manager
    int seed;

    //GPU Instancing buffers
    Matrix4x4[] featuresBuffer;

    // Start is called before the first frame update
    void Start()
    {
        floating_Origin_Manager = Floating_Origin_Manager.Instance;
        featuresBuffer = new Matrix4x4[10000];

        flora_L_System = new(seed, noiseTexture);
        flora_L_System.GenerateFlora();

    }

    // Update is called once per frame
    void Update()
    {
        Mesh mesh = grass;//flora_L_System.GetMesh(0);
        Graphics.DrawMeshInstanced(mesh, 0, floraMat, featuresBuffer);
    }

    public void GenerateFeatures(List<Vector3> positions = null)
    {
        Vector3 planetCenter = planetWrapper.GetPlanetOffset();
        Vector3 floating_origin_position = floating_Origin_Manager.GetFloatingOriginPosition();
        int i = 0;
        for (; i < positions.Count && i < featuresBuffer.Length; ++i)
        {
            Vector3 dir = (floating_origin_position + positions[i] + planetCenter).normalized;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir);
            featuresBuffer[i] = Matrix4x4.TRS(positions[i] + transform.position + planetCenter, rot, Vector3.one);
        }
        while (i < featuresBuffer.Length) { featuresBuffer[i] = Matrix4x4.zero; i++; } //reset remaining buffer
    }

    //Will direct the Flora L System class to generate the flora in question
    public void GenerateFlora()
    {


    }
}
