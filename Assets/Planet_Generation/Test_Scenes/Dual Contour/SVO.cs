using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SparseVoxelOctree
{

    public class SVO
    {
        public SVONode root;

        public SVO(SVONode root = null)
        {
            this.root = root;
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
        }

        public bool IsEmpty() { return isLeaf && dualVertexIndex == -1; }


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
                    Debug.Log("Neighbor Index: " + neighborIndex);
                    if (neighbor == null)
                    {
                        Debug.Log("Neighbor is null in direction " + direction);
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
                Debug.Log("Moving up from child index: " + parentChildIndex);
                path.Add(parentChildIndex);
                current = current.parent;
            }
            Debug.Log("No neighbor found in direction " + direction);
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
