using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using ProductionRules;

namespace FloraLSystem
{
    [System.Serializable]
    public class Flora
    {
        private Mesh mesh;
        protected string floraString;
        private float theta = 25f;
        private float stepLength = 1f;
        [SerializeField] protected uint age = 0;

        [SerializeField] private List<char> symbols;


        [SerializeField] private List<char> constants;

        [SerializeField] private List<Production> productionList;

        //is an additive bias when changing direction in a stalk
        [SerializeField] private Vector3 rotationBias;

        //An attraction factor
        [SerializeField] private Vector3 attractionBias;

        [SerializeField] private float radiusNoiseFactor;
        [SerializeField] private float radiusNoiseScale;
        [SerializeField] private float radiusBranchDecayFactor;

        public List<Production> GetProductionList() { return productionList; }

        public Vector3 GetRotationBias() { return rotationBias; }
        public Vector3 GetAttractionBias() { return attractionBias; }
        public float GetRadiusNoiseFactor() { return radiusNoiseFactor; }
        public float GetRadiusNoiseScale() { return radiusNoiseScale; }

        public List<char> GetConstantList() { return constants; }


        public List<char> GetSymbolList() { return symbols; }
        public uint GetAge() { return age; }
        public void SetMesh(Mesh mesh_) { mesh = mesh_; }
    };

    public abstract class Flora_L_System : MonoBehaviour
    {
        private List<Flora> floraList;
        private int seed;
        //[SerializeField] private Flora_ScriptableObject rules;
        //Symbols
        [SerializeField] protected List<char> symbols;

        //Constants
        [SerializeField] protected List<char> constants;

        //Productions
        [SerializeField] protected Dictionary<char, string> productions;
        //Age
        [SerializeField] protected uint age = 0;

        //String
        [SerializeField] protected string floraString;

        [SerializeField] private float theta = 25f;
        [SerializeField] private float stepLength = 1f;

        [SerializeField] private Texture3D perlinNoiseTexture;

        //Drawing
        List<(Vector3 from, Vector3 to)> segments;

        List<Vector3> vertices;

        [SerializeField] GameObject leaf;

        public Flora_L_System(int seed_ = 0)
        {
            seed = seed_;
            floraList = new(5);
        }

        public void GenerateFlora()
        {


            //convert production list from scriptableObject to a dictionary
            foreach (Flora f in floraList)
            {
                //Load symbols
                productions = new();
                symbols = new();
                constants = new();
                foreach (Production prod in f.GetProductionList())
                {
                    productions.Add(prod.symbol, prod.replacement);
                }
                constants = f.GetConstantList();

                symbols = f.GetSymbolList();
                //------------------------------

                //Assume all relevant data is loaded into Flora f already

                //Progress age until it meets requirements
                uint intendedAge = 3; //Should not be a constant later on
                while (age < intendedAge) ProgressAge();



                CreateMesh(f);

                //load the resulting mesh into the Flora object
            }


        }

        void Start() { }

        void Update() { }

        //Generate
        [ContextMenu("Progress Age")]
        public void ProgressAge()
        {
            //We want to gracefully fail if productions or symbols are empty
            Debug.Assert(symbols.Count > 0, "Symbols list is empty.");
            Debug.Assert(productions.Count > 0, "Production list is empty.");

            string progressedFloraString = String.Empty;

            //add first char to stack

            foreach (char c in floraString)
            {
                //be sure that we can find it, otherwise gracefully fail
                //Debug.Assert(productions.TryGetValue(c, out string prod), "Production does not exist.");

                if (!constants.Contains(c))
                {
                    progressedFloraString += productions[c];
                    continue;
                }

                progressedFloraString += c;
            }

            age++;
            floraString = progressedFloraString;
            Interpret();

        }

        private List<(Vector3 from, Vector3 to)> Interpret()
        {
            segments = new();
            Stack<(Vector3 pos, Quaternion rot)> stateStack = new();

            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            float angle = 25f;
            float stepLength = 5f;

            foreach (char c in floraString)
            {
                switch (c)
                {
                    case 'L':
                        break;
                    case 'X':
                        //can be leaves?
                        break;
                    case 'F':
                        Vector3 start = position;
                        position += rotation * Vector3.up * stepLength;
                        segments.Add((start, position));
                        break;
                    case '[':
                        stateStack.Push((position, rotation));
                        stepLength *= 0.8f;
                        break;
                    case ']':

                        var (pos, rot) = stateStack.Pop();
                        position = pos;
                        rotation = rot;
                        stepLength /= 0.8f;
                        break;
                    case '+':
                        rotation *= Quaternion.AngleAxis(angle, Vector3.right);
                        break;
                    case '-':
                        rotation *= Quaternion.AngleAxis(-angle, Vector3.right);
                        break;
                    default:
                        break;
                }

            }

            return segments;
        }

        private void NGonRingVertices(int n, float radius, Vector3 position, Vector3 forward, Vector3 right, Vector3 up)
        {
            for (int i = 0; i < n; ++i)
            {
                float nGonAngle = 2 * Mathf.PI * i / n;
                Vector3 point = Mathf.Cos(nGonAngle) * right + Mathf.Sin(nGonAngle) * up;
                vertices.Add(position + point * radius);
            }
        }

        [ContextMenu("Create Mesh")]
        public void CreateMesh(Flora f)
        {
            Mesh mesh = Create3DMesh(f);
            f.SetMesh(mesh);
            //GetComponent<MeshFilter>().mesh = mesh;
        }

        public Mesh Create3DMesh(Flora f)
        {
            //for each segment, we need to build a ring of vertices, and connect the triangles

            //do we assume a stalk is done when we hit a new state?

            //for n gon
            const int n = 6;

            float radius = 1f;

            Vector3 bias = f.GetRotationBias();
            Vector3 attraction = f.GetAttractionBias();
            float noiseFactor = f.GetRadiusNoiseFactor();
            int noiseScale = (int)f.GetRadiusNoiseScale();

            Stack<(Vector3 pos, Quaternion rot, int nextRingIndex, int age)> stateStack = new();

            Vector3 position = Vector3.zero;
            Quaternion rotation = Quaternion.AngleAxis(-90, Vector3.right);

            Vector3 forward, right, up;
            forward = rotation * Vector3.forward;
            right = rotation * Vector3.right;
            up = rotation * Vector3.up;

            Action ApplyAttraction = () =>
            {

                float baseStrength = 0.1f;

                forward = rotation * Vector3.forward;
                Vector3 forwardDir = forward.normalized;
                Vector3 attractProjected = Vector3.ProjectOnPlane(attraction, forwardDir);

                float mag = attractProjected.magnitude;

                Vector3 attractDir = attractProjected / mag;

                // Torque axis
                Vector3 axisD = Vector3.Cross(forwardDir, attractDir);
                float sinTheta = axisD.magnitude;

                if (sinTheta > 1e-5f)
                {
                    axisD /= sinTheta;

                    float angleRad = sinTheta * baseStrength * mag;
                    rotation = Quaternion.AngleAxis(angleRad * Mathf.Rad2Deg, axisD) * rotation;
                }
            };

            int age = 0;

            Mesh mesh = new();
            vertices = new();
            List<int> tris = new();

            int nextRingIndex = 0;



            NGonRingVertices(n, radius * (Mathf.Pow(0.9f, age - 5)), position, forward, right, up);

            foreach (char c in floraString)
            {
                Vector3 axis = new(UnityEngine.Random.Range(0, 2), UnityEngine.Random.Range(0, 2), UnityEngine.Random.Range(0, 2));

                switch (c)
                {
                    case 'L':
                        // Leaf forward = branch forward
                        Vector3 leafForward = rotation * Vector3.forward;

                        // Random twist around branch axis
                        Quaternion twist =
                            Quaternion.AngleAxis(UnityEngine.Random.Range(-120f, 120f), leafForward);

                        Quaternion leafRotation = twist * rotation;

                        Instantiate(leaf, position + transform.position, UnityEngine.Random.rotation);
                        Instantiate(leaf, position + transform.position, UnityEngine.Random.rotation);
                        Instantiate(leaf, position + transform.position, UnityEngine.Random.rotation);
                        break;
                    case 'R':
                        // Ring Leaf
                        float scaleFactor = 1 - ((float)age / floraString.Length);
                        for (int i = 0; i < n; ++i)
                        {
                            twist = Quaternion.AngleAxis((360f / n) * i, forward);
                            GameObject leafObj = Instantiate(leaf, position + transform.position, twist * rotation);
                            leafObj.transform.localScale *= scaleFactor;
                        }
                        break;
                    case 'X':
                        break;
                    case 'F':

                        ApplyAttraction();
                        Color centerPixel = perlinNoiseTexture.GetPixel(
                            ((int)position.x * noiseScale) % perlinNoiseTexture.width,
                            ((int)position.y * noiseScale) % perlinNoiseTexture.height,
                            ((int)position.z * noiseScale) % perlinNoiseTexture.depth
                        );
                        rotation *= Quaternion.AngleAxis(centerPixel.b * noiseFactor, axis);

                        Vector3 start = position;
                        position += rotation * Vector3.forward * stepLength;
                        age++;


                        //add vertices to points
                        forward = rotation * Vector3.forward;
                        right = rotation * Vector3.right;
                        up = rotation * Vector3.up;

                        int startIndex = nextRingIndex;
                        NGonRingVertices(n, radius * (Mathf.Pow(0.95f, age - 5)), position, forward, right, up);
                        nextRingIndex = vertices.Count - n;

                        //add triangles
                        for (int i = 0; i < n; ++i)
                        {
                            int a0 = startIndex + (i % n);
                            int b0 = startIndex + ((i + 1) % n);
                            int a1 = nextRingIndex + (i % n);
                            int b1 = nextRingIndex + ((i + 1) % n);

                            Debug.Assert(a0 >= 0 && a0 < vertices.Count, "vertexLength :" + vertices.Count + "a0 is out of bounds with value: " + a0);
                            Debug.Assert(b0 >= 0 && b0 < vertices.Count, "vertexLength :" + vertices.Count + "b0 is out of bounds with value: " + b0);
                            Debug.Assert(a1 >= 0 && a1 < vertices.Count, "vertexLength :" + vertices.Count + "a1 is out of bounds with value: " + a1);
                            Debug.Assert(b1 >= 0 && b1 < vertices.Count, "vertexLength :" + vertices.Count + "b1 is out of bounds with value: " + b1);

                            tris.Add(a0);
                            tris.Add(b0);
                            tris.Add(a1);

                            tris.Add(b0);
                            tris.Add(b1);
                            tris.Add(a1);
                        }

                        break;
                    case '[':
                        stateStack.Push((position, rotation, nextRingIndex, age));
                        radius *= 0.8f;
                        break;
                    case ']':

                        var (pos, rot, nextring, nextage) = stateStack.Pop();
                        position = pos;
                        rotation = rot;
                        nextRingIndex = nextring;
                        radius /= 0.8f;
                        age = nextage;


                        break;
                    case '+':
                        rotation *= Quaternion.AngleAxis(theta, axis);
                        break;
                    case '-':
                        rotation *= Quaternion.AngleAxis(-theta, axis);
                        break;
                    default:
                        break;
                }
            }
            mesh.vertices = vertices.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();


            return mesh;
        }
    }
}