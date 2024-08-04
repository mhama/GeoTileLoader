using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace GeoTile
{
    /// <summary>
    /// traverses tile set. create GameObject hierarchy
    /// </summary>
    class TileSetTraverser
    {
        TileSet tileSet;

        public TileSetTraverser(TileSet tileSet)
        {
            this.tileSet = tileSet;
        }

        public void Traverse(Transform parent, Action<TileSetNode, GameObject> onNodeFound)
        {
            TraverseNode("", tileSet.root, 0, parent, onNodeFound);
        }

        public void TraverseNode(string baseName, TileSetNode node, int index, Transform parent, Action<TileSetNode, GameObject> onNodeFound)
        {
            var go = new GameObject("" + index);
            var name = string.IsNullOrEmpty(baseName) ? index.ToString() : baseName + "-" + index;
            go.transform.SetParent(parent, false);
            go.name = name;
            onNodeFound?.Invoke(node, go);
            if (node.children == null)
            {
                return;
            }

            int childIndex = 0;
            foreach(var child in node.children)
            {
                TraverseNode(name, child, childIndex++, go.transform, onNodeFound);
            }
        }
    }
}
