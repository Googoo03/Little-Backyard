using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using worley_3d;
using Simplex;

public class ThreeD_Texture_Manager : MonoBehaviour
{

    [SerializeField] private List<Vector3> worleyPoints = new List<Vector3>();
    [SerializeField] private Worley3D worley3D;
    [SerializeField] private Noise simplex3D;
    [SerializeField] private int resolution;
    public FloatParameter frequency;
    public FloatParameter lacunarity;
    public FloatParameter persistence;
    public IntParameter octaves;
    public FloatParameter amplitude;
    // Start is called before the first frame update
    void Start()
    {

        //generateWorleyPoints(25);
        worley3D = new Worley3D(ref worleyPoints, transform.position, resolution, (float)frequency, (float)persistence, (int)octaves, (float)lacunarity, (float)amplitude);

        simplex3D = new Noise();
    }

    // Update is called once per frame
    public void Generate3DPerlinTexture()
    {
        if (simplex3D == null) return;
        simplex3D.CreateTexture3D((int)resolution, (int)4, (float)frequency, (float)lacunarity, (float)0.5f, (float)1);
    }

    public void Generate3DWorleyTexture()
    {
        if (worley3D == null) return;
        worley3D.CreateTexture3D();
    }

}
