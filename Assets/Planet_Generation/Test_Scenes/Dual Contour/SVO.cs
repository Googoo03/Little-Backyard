using System;
using System.Collections;
using System.Collections.Generic;
using DualContour;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace SparseVoxelOctree
{

    public class SVO
    {
        /// <summary>
        /// Traverses the SVO from the root to the leaf node containing the given position.
        /// Returns the leaf node, or null if the path does not exist.
        /// </summary>
        public Dual_Contour meshingAlgorithm;
        public SVONode root;
        public int chunkSize = 32;
        public List<Vector3> vertices = new();

        public Dictionary<Vector3, Tuple<bool, GameObject>> chunks = new();

        public SVONode TraversePath(Vector3Int targetPos)

        {
            SVONode node = root;
            while (node != null && !node.isLeaf)
            {
                int half = node.size / 2;
                int childIndex = 0;
                if (targetPos.x >= node.position.x + half) childIndex |= 1 << 2;
                if (targetPos.y >= node.position.y + half) childIndex |= 1 << 1;
                if (targetPos.z >= node.position.z + half) childIndex |= 1;
                if (node.children == null || node.children[childIndex] == null) return null;
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

            // mark for renewal
            chunks[node.position] = new Tuple<bool, GameObject>(true, entry.Item2);
        }
        public SVO(SVONode root = null, Dual_Contour meshingAlgorithm = null)
        {
            this.root = root;
            this.meshingAlgorithm = meshingAlgorithm;
            meshingAlgorithm.SetVertexList(vertices);
        }


        // Methods for building, updating, and querying the tree
        // Methods for mesh extraction in a region (for chunk mesh generation)

        /// <summary>
        /// Traverse all leaf nodes in the SVO and apply the given action.
        /// </summary>
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
                if (node.children[i] != null)
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
            for (int i = 0; i < 8; i++)
            {
                if (node.children[i] != null)
                    TraverseNodesRecursive(node.children[i], action);
            }
        }

        public void GenerateChunks()
        {
            void generateChunk(SVONode node)
            {

                if (node.size != chunkSize || node.IsEmpty()) return;

                //Generate gameObject for chunk
                GameObject chunkObject = !chunks.ContainsKey(node.position) ? new("Chunk_" + node.position.ToString())
                    : chunks[node.position].Item2;



                //if not marked for renewal, dont regenerate

                if (!chunks.ContainsKey(node.position) || chunks[node.position].Item1 == true)
                {



                    //add if not present already, renew
                    chunks[node.position] = new Tuple<bool, GameObject>(false, chunkObject);

                    // Assumed all chunks beyond this point are brand new or marked for renewal. Regenerate mesh

                    //Gather vertex nodes for home chunk
                    Dictionary<Vector3Int, SVONode> verticesDict = new();
                    Dictionary<Vector3, int> globalToLocal = new();
                    List<Vector3> verts = new();

                    //should have a dictionary and a list of vertices?
                    List<int> indices = new();

                    //adds to local list of vertices and dictionary of vertex nodes
                    node.GatherChunkVertices(this, verticesDict, verts, globalToLocal);

                    //for each of the vertex nodes, generate indices

                    foreach (var v in verticesDict.Values)
                    {
                        if (v.edge == -1) continue;


                        meshingAlgorithm.SVOQuad(v, indices, globalToLocal, verts);

                    }

                    //Apply mesh data to gameObject
                    MeshFilter mf = chunkObject.GetComponent<MeshFilter>();
                    if (mf == null) mf = chunkObject.AddComponent<MeshFilter>();
                    MeshRenderer rend = chunkObject.GetComponent<MeshRenderer>();
                    if (rend == null) rend = chunkObject.AddComponent<MeshRenderer>();


                    if (mf.sharedMesh == null)
                    {
                        mf.sharedMesh = new Mesh();
                    }
                    else
                    {
                        mf.sharedMesh.Clear();
                    }
                    Mesh m = mf.sharedMesh;
                    rend.material = Resources.Load("Test") as Material;

                    //IF WE WANT TEXTURES, WE HAVE TO CALCULATE THE UVS MANUALLY
                    //m.uv = uvs;
                    ////////////////////////////////////////////////////////////
                    if (indices.Count < 3) return;



                    m.vertices = verts.ToArray();
                    m.normals = new Vector3[verts.Count]; //placeholders
                    Vector2[] uvs = new Vector2[verts.Count];

                    for (int i = 0; i < uvs.Length; ++i)
                    {
                        uvs[i] = verts[i];
                    }

                    m.uv = uvs;

                    m.SetIndices(indices.ToArray(), MeshTopology.Triangles, 0);
                    m.RecalculateBounds();
                    m.RecalculateNormals();
                }

            }

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
    }

    public class SVONode
    {
        // Start is called before the first frame update
        public Vector3Int position; // Min corner of the node
        public int size;            // Length of the node's edge
        public int childIndex;     // Index in parent's children array (0-7) Also corresponds to direction
        public SVONode parent; // Reference to parent node
        public SVONode[] children;  // 8 children, null if not subdivided
        public bool isLeaf;         // True if this node is a leaf
        public Vector3 vertex; // Index in the mesh vertex list (if leaf)
        public int edge;

        public bool voteToCollapse;

        public float minSDF, maxSDF;


        public SVONode(Vector3Int pos, int s, SVONode parent = null, int childIndex = -1)
        {
            position = pos;
            size = s;
            children = null;
            isLeaf = true;
            vertex = Vector3.zero;
            edge = -1;
            voteToCollapse = false;
            this.parent = parent;
            this.childIndex = childIndex;
        }

        public void Subdivide()
        {
            if (!isLeaf) return; // Already subdivided

            children = new SVONode[8];
            int halfSize = size / 2;

            for (int i = 0; i < 8; i++)
            {
                Vector3Int childPos = position + new Vector3Int(
                    ((i >> 2) & 1) * halfSize,
                    ((i >> 1) & 1) * halfSize,
                    ((i) & 1) * halfSize
                );
                children[i] = new SVONode(childPos, halfSize, this, i);
            }

            isLeaf = false;
            voteToCollapse = false;
            vertex = Vector3.zero; // No dual vertex for non-leaf nodes
            edge = -1;

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

        public Vector3 GetCenter() { return position + 0.5f * size * Vector3.one; }


        public void GatherChunkVertices(SVO svo, Dictionary<Vector3Int, SVONode> existingVertices = null, List<Vector3> vertexList = null, Dictionary<Vector3, int> globalToLocal = null)
        {
            void gatherVertex(SVONode node)
            {
                if (node.edge == -1) return; // No vertex to gather
                Vector3Int key = node.position;
                if (!existingVertices.ContainsKey(key))
                {
                    existingVertices[key] = node;

                    //change dualgrid index to vertexList length

                    // ensure local index assignment
                    int index = vertexList.Count;
                    globalToLocal[vertex] = index;
                    //Add to vertex list
                    vertexList?.Add(vertex);


                }
            }
            TraverseLeaves(gatherVertex);

        }



        public void GenerateVerticesForLeaves(System.Action<SVONode> vertexFunc)
        {
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
                children[i]?.TraverseLeaves(action);
            }
        }
        /// <summary>
        /// Finds the neighbor node in the given direction, handling differing LODs.
        /// direction: 000 2bit is x, 1bit is y, 0bit is z
        /// Returns the deepest adjacent node (may be larger or smaller than this node).
        /// </summary>
        public SVONode GetNeighborLOD(int direction)
        {
            SVONode current = this;
            List<int> path = new();

            // Decode requested direction
            bool xdir = ((direction >> 2) & 1) == 1;
            bool ydir = ((direction >> 1) & 1) == 1;
            bool zdir = ((direction) & 1) == 1;

            // Go upward until all requested directions can flip
            while (current.parent != null)
            {
                int parentChildIndex = current.childIndex;

                bool xbit = ((parentChildIndex >> 2) & 1) == 1;
                bool ybit = ((parentChildIndex >> 1) & 1) == 1;
                bool zbit = ((parentChildIndex) & 1) == 1;

                // Condition: for every axis we want to move in, we must NOT already be on the far side
                bool canMove =
                    (!xdir || !xbit) &&
                    (!ydir || !ybit) &&
                    (!zdir || !zbit);

                if (canMove)
                {
                    // XOR full direction at once (can include multiple axes)
                    int neighborIndex = parentChildIndex ^ direction;
                    SVONode neighbor = current.parent.children[neighborIndex];

                    if (neighbor == null)
                        return null;

                    // Descend down, flipping axes as needed
                    for (int i = path.Count - 1; i >= 0; i--)
                    {
                        if (neighbor.isLeaf || neighbor.children == null) break;

                        int descendIndex = path[i];

                        if (xdir) descendIndex ^= (1 << 2);
                        if (ydir) descendIndex ^= (1 << 1);
                        if (zdir) descendIndex ^= 1;

                        neighbor = neighbor.children[descendIndex];
                    }

                    return neighbor; // Found orthogonal or diagonal neighbor
                }

                // Otherwise, keep going up
                path.Add(parentChildIndex);
                current = current.parent;
            }

            return null; // No neighbor in that direction
        }

        public List<SVONode> GetFace(int dir)
        {
            List<SVONode> neighborFace = new();
            void action(SVONode node)
            {

                if (node.isLeaf && IsOnFace(node, this, dir))
                {
                    neighborFace.Add(node);
                }
            }

            TraverseNodes(action, this);

            return neighborFace;
        }

        private bool IsOnFace(SVONode node, SVONode root, int dir)
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

        private void TraverseNodes(System.Action<SVONode> action, SVONode node)
        {
            if (node == null) return;
            TraverseNodesRecursive(node, action);
        }

        private void TraverseNodesRecursive(SVONode node, System.Action<SVONode> action)
        {
            action(node);
            if (node.children == null) return;
            for (int i = 0; i < 8; i++)
            {
                if (node.children[i] != null)
                    TraverseNodesRecursive(node.children[i], action);
            }
        }
        /// <summary>
        /// Returns the child index of this node in its parent (0-7), or -1 if no parent.
        /// </summary>
        public int GetChildIndex()
        {
            return childIndex;
        }

        public bool MayContainCrossing()
        {
            return Mathf.Min(minSDF, maxSDF) <= size;
        }


    }
}

