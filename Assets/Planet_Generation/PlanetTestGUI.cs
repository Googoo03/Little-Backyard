using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Sphere))]
public class PlanetTestGUI : Editor
{
    [SerializeField] Sphere planet;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Generate"))
        {
            Debug.Log("Generate Test Handle");
        }

        if (GUILayout.Button("Center Shaders"))
        {
            planet.SetAtmoShader();
            planet.SetRingShader();
            
        }
    }

    private void OnEnable()
    {
        planet = (Sphere)target;
    }
}
