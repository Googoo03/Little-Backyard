using ProceduralNoiseProject;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

//[CustomEditor(typeof(Wave))]
public class EditorScript : MonoBehaviour
{
    //Wave wave;
    ComputeShader perlinNoise;
    int perlinNoiseHandle;
    int resolution = 16;
    public RenderTexture texture;
    private void Start()
    {

        texture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.RFloat)
        {
            enableRandomWrite = true
        };
        texture.Create();

        ComputeShader perlinNoise = (ComputeShader)Resources.Load("Simplex Noise");
        perlinNoiseHandle = perlinNoise.FindKernel("CSMain");
        perlinNoise.SetInt("seed", 125643);
        //perlinNoise.SetFloat("_DisplacementStrength", 2);
        perlinNoise.SetTexture(perlinNoiseHandle, "Result", texture);


        perlinNoise.Dispatch(perlinNoiseHandle, resolution / 8, resolution / 8, 1);
        transform.GetComponent<Renderer>().material.SetTexture("_HeightMap", texture);

    }

    private void OnSceneGUI()
    {
    }

   /* public override void OnInspectorGUI()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.ObjectField("Texture", texture, typeof(Texture), false);
        EditorGUILayout.EndHorizontal();
    }*/
}