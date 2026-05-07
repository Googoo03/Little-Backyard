using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DualContour;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using faces;
using UnityEngine.SocialPlatforms;
using Unity.Collections;
using Unity.Mathematics;
using SignedDistanceFields;

namespace SparseVoxelOctree
{

    [System.Serializable]
    public class SVO
    {
        /// <summary>
        /// Traverses the SVO from the root to the leaf node containing the given position.
        /// Returns the leaf node, or null if the path does not exist.
        /// </summary>
        public Dual_Contour meshingAlgorithm;
        public SVONode root;
        public Face[] faceNeighbors;
        public int chunkSize = 1024;
        public int faceNum;

        public List<FlatNode> flatList = new();
        public List<Vector3> vertices = new();
        private Material testMat;

        public Dictionary<Vector3, Tuple<bool, GameObject>> chunks = new();

        public List<ISDF> sdfEdits = new();

        public GameObject parentObj;

        private const int X_AXIS = 4;
        private const int Y_AXIS = 2;
        private const int Z_AXIS = 1;

        public SVONode TraversePath(Vector3 targetPos)

        {
            SVONode node = root;
            while (node != null && !node.isLeaf)
            {
                float half = node.size / 2;
                int childIndex = 0;
                if (targetPos.x >= node.position.x + half) childIndex |= X_AXIS;
                if (targetPos.y >= node.position.y + half) childIndex |= Y_AXIS;
                if (targetPos.z >= node.position.z + half) childIndex |= Z_AXIS;
                if (node.children == null || node.children[childIndex] == null) return node;
                node = node.children[childIndex];
            }
            return node;
        }

        public void MarkChunk(SVONode start)
        {
            SVONode node = start;

            // climb up until we find a node at least chunkSize
            while (node != null && node.size < chunkSize)
            {
                node = node.parent;
            }

            if (node == null) return; // went past root

            if (!chunks.TryGetValue(node.position, out var entry))
                return;

            //if already marked, dont allocate more memory for gc
            if (chunks[node.position].Item1 == true) return;


            // mark for renewal
            chunks[node.position] = new Tuple<bool, GameObject>(true, entry.Item2);
        }

        public void MarkChunk(Vector3 pos)
        {
            //takes the local position and finds the chunk associated
            SVONode targetNode = TraversePath(pos);

            MarkChunk(targetNode);
        }

        public SVO(SVONode root = null, Dual_Contour meshingAlgorithm = null, GameObject parentObj = null, Face[] faceNeighbors = null, int faceNum = 0, PlanetWrapper planetWrapper = null)
        {
            this.root = root;
            this.meshingAlgorithm = meshingAlgorithm;
            this.parentObj = parentObj;
            this.faceNeighbors = faceNeighbors;
            this.faceNum = faceNum;
            meshingAlgorithm.SetVertexList(vertices);
            meshingAlgorithm.SetSDFEditList(sdfEdits);
            SetMaterial(planetWrapper);
        }

        public void AddSDFEdit(ISDF edit) { sdfEdits.Add(edit); }


        public void TraverseLeaves(System.Action<SVONode> action)
        {
            if (root == null) return;
            TraverseLeavesRecursive(root, action);
        }

        private void TraverseLeavesRecursive(SVONode node, System.Action<SVONode> action)
        {
            if (node.isLeaf)
            {
                action(node);
                return;
            }
            if (node.children == null) return;
            for (int i = 0; i < 8; i++)
            {
                TraverseLeavesRecursive(node.children[i], action);
            }
        }

        public void TraverseNodes(System.Action<SVONode> action)
        {
            if (root == null) return;
            TraverseNodesRecursive(root, action);
        }

        private void TraverseNodesRecursive(SVONode node, System.Action<SVONode> action)
        {
            action(node);
            if (node.children == null) return;
            foreach (SVONode child in node.children)
                TraverseNodesRecursive(child, action);
        }

        //linearize the tree into a list for easier processing
        int Flatten(SVONode node)
        {
            int currentIndex = flatList.Count;

            // placeholder, may be filled later
            flatList.Add(new FlatNode { IsLeaf = node.isLeaf, Data = 0, ChildBaseIndex = -1 });

            if (!node.isLeaf && node.children != null)
            {
                int childBaseIndex = flatList.Count;
                for (int i = 0; i < 8; i++)
                    Flatten(node.children[i]);

                // now patch in child base index
                FlatNode temp = flatList[currentIndex];
                temp.ChildBaseIndex = childBaseIndex;
                flatList[currentIndex] = temp;
            }

            return currentIndex;
        }

        void ResetLocalIndex()
        {
            TraverseLeaves((node) => { node.localIndex = -1; });
        }

        private void SetMaterial(PlanetWrapper planetWrapper)
        {
            Atmosphere_Manager atmosphere_Manager = planetWrapper.GetAtmosphere_Manager();
            testMat = atmosphere_Manager.GetPlanetMat();
        }

        public void GenerateChunks()
        {

            List<Vector3> verts = new();
            List<int> indices = new();
            List<SVONode> nodes = new();

            //should have a dictionary and a list of vertices?

            void generateChunk(SVONode node)
            {



                if (node.size != chunkSize || node.IsEmpty()) return;

                bool chunkExists = chunks.ContainsKey(node.position);
                //Generate gameObject for chunk
                GameObject chunkObject = !chunkExists ? new("Chunk_" + node.position.ToString())
                    : chunks[node.position].Item2;

                //if not marked for renewal, dont regenerate

                if (!chunkExists || chunks[node.position].Item1 == true)
                {
                    //add if not present already, renew
                    chunks[node.position] = new Tuple<bool, GameObject>(false, chunkObject);
                    chunkObject.tag = "Chunk";
                    // Assumed all chunks beyond this point are brand new or marked for renewal. Regenerate mesh

                    //Gather vertex nodes for home chunk

                    nodes.Clear();

                    verts.Clear();
                    indices.Clear();

                    //adds to local list of vertices and dictionary of vertex nodes
                    node.GatherChunkVertices(nodes, verts);


                    SVONode xNeighbor = SVONode.GetNeighborLOD(node, X_AXIS);
                    SVONode zNeighbor = SVONode.GetNeighborLOD(node, Z_AXIS);
                    SVONode yNeighbor = SVONode.GetNeighborLOD(node, Y_AXIS);
                    SVONode zxNeighbor = SVONode.GetNeighborLOD(zNeighbor, X_AXIS);
                    SVONode xyNeighbor = SVONode.GetNeighborLOD(xNeighbor, Y_AXIS);
                    SVONode yzNeighbor = SVONode.GetNeighborLOD(yNeighbor, Z_AXIS);
                    SVONode xyzNeighbor = SVONode.GetNeighborLOD(xyNeighbor, Z_AXIS);

                    xNeighbor?.GatherChunkVerticesFace(nodes, verts, X_AXIS);
                    yNeighbor?.GatherChunkVerticesFace(nodes, verts, Y_AXIS);
                    zNeighbor?.GatherChunkVerticesFace(nodes, verts, Z_AXIS);
                    zxNeighbor?.GatherChunkVerticesFace(nodes, verts, X_AXIS | Z_AXIS);
                    xyNeighbor?.GatherChunkVerticesFace(nodes, verts, X_AXIS | Y_AXIS);
                    yzNeighbor?.GatherChunkVerticesFace(nodes, verts, Y_AXIS | Z_AXIS);
                    //xyzNeighbor?.GatherChunkVerticesShell(nodes, verts);

                    indices.Capacity = 3 * verts.Count;


                    //New DC quad generation
                    meshingAlgorithm.quads.Clear(); //clear quad list
                    meshingAlgorithm.CellProcHelper(node); //do the recursion from the paper, and fill the quad list. Constains vertex indices

                    //This may need to be flipped
                    if (xNeighbor != null) meshingAlgorithm.RefreshNeighborChunk(node, xNeighbor, X_AXIS);
                    if (yNeighbor != null) meshingAlgorithm.RefreshNeighborChunk(node, yNeighbor, Y_AXIS);
                    if (zNeighbor != null) meshingAlgorithm.RefreshNeighborChunk(node, zNeighbor, Z_AXIS);

                    if (zxNeighbor != null) meshingAlgorithm.RefreshNeighborCorner(node, zNeighbor, zxNeighbor, xNeighbor, X_AXIS | Z_AXIS);
                    if (xyNeighbor != null) meshingAlgorithm.RefreshNeighborCorner(node, yNeighbor, xyNeighbor, xNeighbor, X_AXIS | Y_AXIS);
                    if (yzNeighbor != null) meshingAlgorithm.RefreshNeighborCorner(node, zNeighbor, yzNeighbor, yNeighbor, Y_AXIS | Z_AXIS);
                    //if (xyzNeighbor != null) meshingAlgorithm.RefreshNeighborCorner(node, xyNeighbor, xyzNeighbor, yzNeighbor, X_AXIS | Z_AXIS);


                    foreach (var quad in meshingAlgorithm.quads)
                    {
                        if ((quad.sign & quad.dir) == 0) //nothing set, negative flip
                        {
                            indices.Add(quad.v0);
                            indices.Add(quad.v1);
                            indices.Add(quad.v2);

                            indices.Add(quad.v2);
                            indices.Add(quad.v3);
                            indices.Add(quad.v0);
                        }
                        else
                        {
                            indices.Add(quad.v0);
                            indices.Add(quad.v2);
                            indices.Add(quad.v1);

                            indices.Add(quad.v2);
                            indices.Add(quad.v0);
                            indices.Add(quad.v3);
                        }

                    }

                    foreach (SVONode n in nodes) { n.localIndex = -1; } //clear local indices after use



                    //Apply mesh data to gameObject
                    MeshFilter mf = chunkObject.GetComponent<MeshFilter>();
                    mf = mf != null ? mf : chunkObject.AddComponent<MeshFilter>();
                    MeshRenderer rend = chunkObject.GetComponent<MeshRenderer>();
                    rend = rend != null ? rend : chunkObject.AddComponent<MeshRenderer>();

                    chunkObject.transform.parent = parentObj.transform;
                    chunkObject.transform.localPosition = Vector3.zero;


                    if (mf.sharedMesh == null)
                    {
                        mf.sharedMesh = new Mesh();
                    }
                    else
                    {
                        mf.sharedMesh.Clear();
                    }
                    Mesh m = mf.sharedMesh;
                    rend.material = testMat;

                    if (indices.Count < 3) return;

                    m.vertices = verts.ToArray();
                    m.normals = new Vector3[verts.Count]; //placeholders

                    m.SetIndices(indices.ToArray(), MeshTopology.Triangles, 0);
                    m.RecalculateNormals();

                    MeshCollider col = chunkObject.GetComponent<MeshCollider>();
                    if (col == null)
                    {
                        chunkObject.AddComponent<MeshCollider>().sharedMesh = m;
                    }
                    else
                    {
                        col.sharedMesh = m;
                    }
                }

            }

            //we dont need to be doing a DFS every frame. just store the chunk positions and their corresponding nodes?
            TraverseNodes(generateChunk);

        }



        public void GenerateVerticesForLeaves(System.Action<SVONode> vertexFunc = null)
        {
            vertexFunc ??= meshingAlgorithm.SVOVertex; //default to class vertex generation

            TraverseLeaves((node) =>
            {
                if (node.edge == -1)
                {
                    //assigns index and axis information
                    //adds to vertex list

                    vertexFunc(node);
                }
            });

        }

        //given a master octree list, traverse the leaves from the starting index
        public void TraverseLeavesList(System.Action<SVONode, int> action, List<SVONode> masterList, int index)
        {
            SVONode node = masterList[index];
            if (node.isLeaf)
            {
                action(node, index);
                return;
            }
            if (node.children == null) return;
            for (int i = 0; i < 8; i++)
            {
                int newIndex = index * 8 + (i + 1);
                if (newIndex >= masterList.Count) continue;
                TraverseLeavesList(action, masterList, newIndex);
            }
        }

        public void TraverseNodesList(System.Action<SVONode, int> action, List<SVONode> masterList, int index)
        {

            SVONode node = masterList[index];
            action(node, index);
            if (node.children == null) return;
            for (int i = 0; i < 8; i++)
            {
                int newIndex = index * 8 + (i + 1);
                if (newIndex >= masterList.Count) continue;
                TraverseNodesList(action, masterList, newIndex);
            }
        }
    }












    public struct FlatNode
    {
        public bool IsLeaf;
        public int ChildBaseIndex; // index of first child, -1 if leaf
        public int Data;
    }


    public class SVONode
    {
        // Start is called before the first frame update

        public SVO parentOBJ;

        public Vector3 position; // Min corner of the node

        public Vector3 transformedPosition; // Transformed position if needed

        public float size;            // Length of the node's edge
        public Vector3 center;
        public int childIndex;     // Index in parent's children array (0-7) Also corresponds to direction
        public SVONode parent; // Reference to parent node
        public SVONode[] children;  // 8 children, null if not subdivided
        public bool isLeaf;         // True if this node is a leaf
        public Vector3 vertex; // Index in the mesh vertex list (if leaf)
        public int localIndex; // Local index in the chunk mesh (if leaf)
        public int gpuBufferIndex;

        public int edge;
        public bool voteToCollapse;

        public float minSDF, maxSDF;

        public SVONode(Vector3 pos, float s, SVONode parent = null, int childIndex = -1, System.Func<Vector3, Vector3> transformFunc = null, SVO parentOBJ = null)
        {
            position = pos;
            transformedPosition = transformFunc != null ? transformFunc(pos) : pos;
            size = s;
            center = position + (0.5f * size * Vector3.one);
            children = null;
            isLeaf = true;
            vertex = Vector3.zero;
            edge = -1;
            localIndex = -1;
            voteToCollapse = false;
            this.parentOBJ = parentOBJ;
            this.parent = parent;
            this.childIndex = childIndex;
            gpuBufferIndex = -1;
        }

        public void Subdivide(System.Func<Vector3, Vector3> transformFunc = null)
        {
            if (!isLeaf) return; // Already subdivided

            children = new SVONode[8];
            float halfSize = size / 2;

            for (int i = 0; i < 8; i++)
            {
                Vector3 childPos = position + new Vector3(
                    ((i >> 2) & 1) * halfSize,
                    ((i >> 1) & 1) * halfSize,
                    ((i) & 1) * halfSize
                );
                //UnityEngine.Debug.Log("parentOBJ in subdivide: " + (parentOBJ == null ? "null" : parentOBJ));
                children[i] = new SVONode(childPos, halfSize, this, i, transformFunc, parentOBJ);
            }

            isLeaf = false;
            voteToCollapse = false;
            //vertex = Vector3.zero; // No dual vertex for non-leaf nodes
            //edge = -1;

            //what happens to vertices that are no longer referenced? Do we simply keep them? Should remove?
        }


        //THIS CAUSES OVERFLOW ISSUES BECAUSE WE NEVER REMOVE THE VERTEX FROM THE MASTER LIST
        public void Collapse()
        {
            if (isLeaf) return; // Already a leaf

            children = null;
            isLeaf = true;
            vertex = Vector3.zero; // Reset dual vertex index
            edge = -1;

            //mark for removal
        }

        public bool IsEmpty() { return isLeaf && edge == -1; }

        public void SetSVO(SVO svo) { parentOBJ = svo; }

        public Vector3 Center => center;

        public int GetChildIndex => childIndex;

        public bool MayContainCrossing() { return (minSDF <= maxSDF && minSDF <= size) || (maxSDF <= minSDF && maxSDF <= size); }


        public void GatherChunkVertices(List<SVONode> nodes = null, List<Vector3> vertexList = null)
        {
            void gatherVertex(SVONode node)
            {
                if (node.edge == -1) return; // No vertex to gather

                //Add to vertex list
                node.localIndex = vertexList.Count;
                vertexList?.Add(node.vertex);
                nodes?.Add(node);
            }

            TraverseLeaves(gatherVertex);

        }

        public void GatherChunkVerticesFace(List<SVONode> nodes = null, List<Vector3> vertexList = null, int dir = 0)
        {
            void gatherVertex(SVONode node)
            {
                if (node.edge == -1) return; // No vertex to gather

                //Add to vertex list
                node.localIndex = vertexList.Count;
                vertexList?.Add(node.vertex);
                nodes?.Add(node);
            }

            TraverseLeavesFace(gatherVertex, dir);

        }

        public void GatherChunkVerticesShell(List<SVONode> nodes = null, List<Vector3> vertexList = null)
        {
            void gatherVertex(SVONode node)
            {
                if (node.edge == -1) return; // No vertex to gather

                //Add to vertex list
                node.localIndex = vertexList.Count;
                vertexList?.Add(node.vertex);
                nodes?.Add(node);
            }

            TraverseLeavesShell(gatherVertex);

        }



        public void GenerateVerticesForLeaves(System.Action<SVONode> vertexFunc, bool forceGenerate = false, ISDF sdf = null)
        {
            TraverseLeaves((node) =>
            {
                if (node.edge != -1 && !forceGenerate) return;

                if (forceGenerate && sdf?.Evaluate(node.position) > 2 * node.size) return; //if a new SDF is specified and were outside, dont calculate
                if (forceGenerate) { node.edge = -1; vertex = Vector3.zero; }
                //if not determined to be empty
                vertexFunc(node);

            });

        }

        public void AppendLeavesToBuffer(List<GPUSVONode> buffer, List<SVONode> updateList, bool forceGenerate = false)
        {
            TraverseLeaves((node) =>
            {
                if (node.edge != -1 && !forceGenerate) return;

                if (forceGenerate) { node.edge = -1; vertex = Vector3.zero; }
                //if not determined to be empty
                node.gpuBufferIndex = buffer.Count;
                buffer.Add(new(node));
                updateList.Add(node);
            });

        }

        public void TraverseLeaves(System.Action<SVONode> action)
        {
            if (isLeaf)
            {
                action(this);
                return;
            }
            if (children == null) return;
            for (int i = 0; i < 8; i++)
            {
                children[i].TraverseLeaves(action);
            }
        }

        public void TraverseLeavesShell(System.Action<SVONode> action)
        {
            if (isLeaf)
            {
                action(this);
                return;
            }
            if (children == null) return;
            for (int i = 0; i < 8; i++)
            {
                //do the shell that aligns with the childindex
                if ((i ^ childIndex) == 0b111) continue;
                children[i].TraverseLeavesShell(action);
            }
        }

        public void TraverseLeavesFace(System.Action<SVONode> action, int dir)
        {
            if (isLeaf)
            {
                action(this);
                return;
            }
            if (children == null) return;
            for (int i = 0; i < 8; i++)
            {
                //do the shell that aligns with the childindex
                if (((~i) & dir) == 0) continue;
                children[i].TraverseLeavesShell(action);
            }
        }


        // Finds the neighbor node in the given direction, handling differing LODs.
        // direction: 000 2bit is x, 1bit is y, 0bit is z
        // Returns the deepest adjacent node (may be larger or smaller than this node).
        public static SVONode GetNeighborLOD(SVONode node, int direction)
        {
            if (node == null) return null;

            int baseDirection = direction;


            SVONode current = node;
            int[] path = new int[32]; // max depth of 16
            int pathLength = 1;

            // Go upward until all requested directions can flip
            while (current?.parent != null)
            {
                int parentChildIndex = current.childIndex;

                // Condition: for every axis we want to move in, we must NOT already be on the far side
                bool canMove = (parentChildIndex & baseDirection) == 0;

                if (canMove)
                {
                    // XOR full direction at once (can include multiple axes)
                    int neighborIndex = parentChildIndex ^ baseDirection;
                    SVONode neighbor = current.parent.children[neighborIndex];

                    if (neighbor == null)
                        return null;

                    // Descend down, flipping axes as needed
                    for (int i = pathLength - 1; i > 0; i--)
                    {
                        if (neighbor.isLeaf || neighbor.children == null) break;

                        int descendIndex = path[i];
                        descendIndex ^= baseDirection;

                        neighbor = neighbor.children[descendIndex];
                    }

                    return neighbor; // Found orthogonal or diagonal neighbor
                }

                // Otherwise, keep going up
                path[pathLength++] = parentChildIndex;
                current = current.parent;
            }

            return null;
        }


        public static List<SVONode> GetFace(SVONode current, int dir)
        {
            List<SVONode> neighborFace = new(8);
            void action(SVONode node)
            {

                if (node.isLeaf && IsOnFace(node, current, dir))
                {
                    neighborFace.Add(node);
                }
            }

            TraverseNodes(action, current);

            return neighborFace;
        }

        private static bool IsOnFace(SVONode node, SVONode root, int dir)
        {
            bool xdir = ((dir >> 2) & 1) == 1;
            bool ydir = ((dir >> 1) & 1) == 1;
            bool zdir = ((dir) & 1) == 1;

            // Walk upward until we reach the subtree root
            SVONode current = node;
            while (current != null && current != root)
            {
                int idx = current.childIndex;
                bool xbit = ((idx >> 2) & 1) == 1;
                bool ybit = ((idx >> 1) & 1) == 1;
                bool zbit = ((idx >> 0) & 1) == 1;

                // Check required bits
                if (xdir && xbit) return false;  // +X face requires all xbits=1
                if (ydir && ybit) return false;
                if (zdir && zbit) return false;

                current = current.parent;
            }

            return true;
        }

        private static void TraverseNodes(System.Action<SVONode> action, SVONode node)
        {
            if (node == null) return;
            TraverseNodesRecursive(node, action);
        }

        private static void TraverseNodesRecursive(SVONode node, System.Action<SVONode> action)
        {
            action(node);
            if (node.children == null) return;
            for (int i = 0; i < 8; i++)
            {
                TraverseNodesRecursive(node.children[i], action);
            }
        }
    }
}

