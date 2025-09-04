using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

[CustomEditor(typeof(DC_Chunk))]
public class ChunkEditorUI : Editor
{
    // Start is called before the first frame update
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var myScript = target as DC_Chunk;
        if (GUILayout.Button("Subdivide"))
        {
            myScript.subdivide = true;
        }

    }
}