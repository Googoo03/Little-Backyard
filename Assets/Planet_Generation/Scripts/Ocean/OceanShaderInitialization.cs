using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OceanShaderInitialization : MonoBehaviour
{
    // Start is called before the first frame update
    public Material oceanMaterial;
    [SerializeField] private Color Shallow_Color;
    [SerializeField] private Color Deep_Color;
    [SerializeField] private float Ocean_Density;
    void Start()
    {
        oceanMaterial = this.GetComponent<Renderer>().material;
        //Set Material Parameters /////////////////
        oceanMaterial.SetColor("SHALLOW", Shallow_Color);
        oceanMaterial.SetColor("DEEP", Deep_Color);
        oceanMaterial.SetFloat("_DepthCoef", Ocean_Density);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
