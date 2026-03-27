using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObject/Atmosphere_Preset")]
public class Atmosphere_Scriptable_Obj : ScriptableObject
{
    public Vector3 wavelengths;

    public Vector4 testParams = new Vector4(7, 1.26f, 0.1f, 3);
    public float scatteringStrength = 20;

    [Range(1, 10)]
    public float intensity = 1;

    public float ditherStrength = 1f;
    public float ditherScale = 4;

    [Range(1, 10)]
    public float densityFalloff = 4f;
    // Start is called before the first frame update
}
