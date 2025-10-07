using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        public int chunkSize = 128;

        public List<FlatNode> flatList = new();
        public List<Vector3> vertices = new();
        private Material testMat = Resources.Load("Test") as Material;

        public Dictionary<Vector3, Tuple<bool, GameObject>> chunks = new();

        public SVONode TraversePath(Vector3 targetPos)

        {
            SVONode node = root;
            while (node != null && !node.isLeaf)
            {
                float half = node.size / 2;
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
                TraverseNodesRecursive(node.children[i], action);
            }
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

        public void GenerateChunks()
        {

            List<Vector3> verts = new();
            List<int> indices = new();

            //flatList.Clear();
            //Flatten(root);


            //should have a dictionary and a list of vertices?

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


                    Dictionary<Vector3, SVONode> verticesDict = new();
                    List<SVONode> nodes = new();
                    List<SVONode> startNodes = new();
                    Dictionary<Vector3, int> globalToLocal = new();

                    verts.Clear();
                    indices.Clear();

                    //Reset local indices
                    //ResetLocalIndex(); //Does a DFS of entire tree

                    //adds to local list of vertices and dictionary of vertex nodes
                    node.GatherChunkVertices(nodes, verts);
                    startNodes.AddRange(nodes);

                    indices.Capacity = 3 * verts.Count;


                    //for each of the vertex nodes, generate indices
                    foreach (SVONode n in startNodes)
                    {
                        if (n.edge == -1) continue;

                        meshingAlgorithm.SVOQuad(n, nodes, indices, globalToLocal, verts);

                    }

                    foreach (SVONode n in nodes) { n.localIndex = -1; } //clear local indices after use



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
                    rend.material = testMat;

                    //IF WE WANT TEXTURES, WE HAVE TO CALCULATE THE UVS MANUALLY
                    //m.uv = uvs;
                    ////////////////////////////////////////////////////////////
                    if (indices.Count < 3) return;

                    m.vertices = verts.ToArray();
                    m.normals = new Vector3[verts.Count]; //placeholders
                    Vector2[] uvs = new Vector2[verts.Count];

                    for (int i = 0; i < uvs.Length; ++i) { uvs[i] = verts[i]; }

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
        public Vector3 position; // Min corner of the node
        public float size;            // Length of the node's edge
        public Vector3 center;
        public int childIndex;     // Index in parent's children array (0-7) Also corresponds to direction
        public SVONode parent; // Reference to parent node
        public SVONode[] children;  // 8 children, null if not subdivided
        public bool isLeaf;         // True if this node is a leaf
        public Vector3 vertex; // Index in the mesh vertex list (if leaf)
        public int localIndex; // Local index in the chunk mesh (if leaf)
        public int edge;

        public int startChildIndex; // when in a list, this points to its first child index

        public bool voteToCollapse;

        public float minSDF, maxSDF;


        public SVONode(Vector3 pos, float s, SVONode parent = null, int childIndex = -1)
        {
            position = pos;
            size = s;
            center = position + (0.5f * size * Vector3.one);
            children = null;
            isLeaf = true;
            vertex = Vector3.zero;
            edge = -1;
            localIndex = -1;
            voteToCollapse = false;
            this.parent = parent;
            this.childIndex = childIndex;
        }

        public void Subdivide()
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

        public Vector3 Center => center;

        public int GetChildIndex => childIndex;

        public bool MayContainCrossing() { return Mathf.Min(minSDF, maxSDF) <= size; }


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
                children[i].TraverseLeaves(action);
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
                TraverseNodesRecursive(node.children[i], action);
            }
        }
    }
}

