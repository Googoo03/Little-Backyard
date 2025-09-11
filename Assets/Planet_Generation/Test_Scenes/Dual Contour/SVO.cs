using System.Collections;
using System.Collections.Generic;
using DualContour;
using UnityEngine;

namespace SparseVoxelOctree
{

    public class SVO
    {
        /// <summary>
        /// Traverses the SVO from the root to the leaf node containing the given position.
        /// Returns the leaf node, or null if the path does not exist.
        /// </summary>
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

        public Dual_Contour meshingAlgorithm;
        public SVONode root;
        public List<Vector3> vertices = new();

        public Dictionary<Vector3, GameObject> chunks = new();

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

        private void TraverseNodes(System.Action<SVONode> action)
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
            int chunkSize = 32; // Example chunk size
            void generateChunk(SVONode node)
            {

                if (node.size == chunkSize && !node.IsEmpty())
                {

                    GameObject chunkObject;
                    //Generate gameObject for chunk
                    chunkObject = !chunks.ContainsKey(node.position) ? new("Chunk_" + node.position.ToString())
                        : chunks[node.position];

                    // Generate mesh for this chunk

                    //Gather vertex nodes for home chunk
                    Dictionary<Vector3Int, SVONode> verticesDict = new();
                    List<Vector3> verticesList = new();

                    //should have a dictionary and a list of vertices?
                    List<int> indices = new();

                    //adds to local list of vertices and dictionary of vertex nodes
                    node.GatherChunkVertices(this, verticesDict, verticesList);

                    //for each of the vertex nodes, generate indices
                    Debug.Log("dictionary values: " + verticesDict.Values.Count);

                    foreach (var v in verticesDict.Values)
                    {
                        if (v.dualVertexIndex == -1) continue;

                        UnityEngine.Debug.Log("Meshing quad");
                        meshingAlgorithm.SVOQuad(v, indices);

                    }

                    //Apply mesh data to gameObject
                    MeshFilter mf = chunkObject.AddComponent<MeshFilter>();
                    MeshRenderer rend = chunkObject.AddComponent<MeshRenderer>();
                    Mesh m = mf.sharedMesh = new Mesh();


                    m.vertices = verticesList.ToArray();
                    m.normals = new Vector3[verticesList.Count]; //placeholders


                    //IF WE WANT TEXTURES, WE HAVE TO CALCULATE THE UVS MANUALLY
                    //m.uv = uvs;
                    ////////////////////////////////////////////////////////////
                    if (indices.Count < 3) return;


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
                if (node.dualVertexIndex == -1)
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
        public int dualVertexIndex; // Index in the mesh vertex list (if leaf)


        public SVONode(Vector3Int pos, int s, SVONode parent = null, int childIndex = -1)
        {
            position = pos;
            size = s;
            children = null;
            isLeaf = true;
            dualVertexIndex = -1;
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
            dualVertexIndex = -1; // No dual vertex for non-leaf nodes

            //what happens to vertices that are no longer referenced? Do we simply keep them?
        }

        public void Collapse()
        {
            if (isLeaf) return; // Already a leaf

            children = null;
            isLeaf = true;
            dualVertexIndex = -1; // Reset dual vertex index
        }

        public bool IsEmpty() { return isLeaf && dualVertexIndex == -1; }

        public Vector3 GetCenter() { return position + 0.5f * size * Vector3.one; }


        public void GatherChunkVertices(SVO svo, Dictionary<Vector3Int, SVONode> existingVertices = null, List<Vector3> vertexList = null)
        {
            void gatherVertex(SVONode node)
            {
                if (node.dualVertexIndex == -1) return; // No vertex to gather
                Vector3Int key = node.position;
                if (!existingVertices.ContainsKey(key))
                {
                    existingVertices[key] = node;
                    vertexList?.Add(svo.vertices[node.dualVertexIndex >> 6 & 0x7FFF]);

                }
            }
            TraverseLeaves(gatherVertex);

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
            List<int> path = new() { childIndex };

            // Traverse up until we can move sideways in the given direction
            while (current.parent != null)
            {
                int parentChildIndex = current.childIndex;
                bool xbit = ((parentChildIndex >> 2) & 1) == 1;
                bool ybit = ((parentChildIndex >> 1) & 1) == 1;
                bool zbit = ((parentChildIndex) & 1) == 1;

                bool xdir = ((direction >> 2) & 1) == 1;
                bool ydir = ((direction >> 1) & 1) == 1;
                bool zdir = ((direction) & 1) == 1;

                // If we can move in the given direction at this level
                if ((!xdir || !xbit) && (!ydir || !ybit) && (!zdir || !zbit))
                {
                    int neighborIndex = parentChildIndex ^ direction;
                    SVONode neighbor = current.parent.children[neighborIndex];

                    if (neighbor == null)
                    {
                        return null;
                    }

                    // Descend to the deepest adjacent node(s)
                    // For each level down, flip the axis bit for the direction moved
                    for (int i = path.Count - 1; i >= 0; i--)
                    {
                        if (neighbor.isLeaf || neighbor.children == null) break;
                        int descendIndex = path[i];
                        // Flip the axis bit for the direction moved
                        if (xdir) descendIndex ^= (1 << 2);
                        if (ydir) descendIndex ^= (1 << 1);
                        if (zdir) descendIndex ^= 1;
                        neighbor = neighbor.children[descendIndex];
                    }
                    return neighbor;
                }
                path.Add(parentChildIndex);
                current = current.parent;
            }
            return null;
        }

        /// <summary>
        /// Returns the child index of this node in its parent (0-7), or -1 if no parent.
        /// </summary>
        public int GetChildIndex()
        {
            return childIndex;
        }


    }
}

