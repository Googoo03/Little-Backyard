using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using worley_3d;

public class ThreeD_Texture_Manager : MonoBehaviour
{

    [SerializeField] private List<Vector3> worleyPoints = new List<Vector3>();
    [SerializeField] private Worley3D worley3D;
    public FloatParameter frequency;
    public FloatParameter lacunarity;
    public FloatParameter persistence;
    public IntParameter octaves;
    public FloatParameter amplitude;
    // Start is called before the first frame update
    void Start()
    {

        //generateWorleyPoints(25);
        worley3D = new Worley3D(ref worleyPoints, transform.position, 32,(float)frequency,(float)persistence,(int)octaves,(float)lacunarity,(float)amplitude);
        worley3D.CreateTexture3D();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void generateWorleyPoints(int num)
    {
        for (int i = 0; i < num; ++i)
        {
            Vector3 point = new Vector3(UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100), UnityEngine.Random.Range(-100, 100));
            point.Normalize();
            point *= 1;
            //this should be multiplied by the radius in the future
            point += transform.position;

            worleyPoints.Add(point);
        }
    }
}
