using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GeoTile
{
    /// <summary>
    /// 著作権者名称を収集するクラス
    /// Google 3D Map Tilesでは著作権表示が必要のため、Google 3D Map Tiles 向けに作成されている
    /// 他の場合の仕様と合致しているかはわからない。
    /// </summary>
    public class TileCopyrightCollector
    {
        private string lastLogText = "";

        /// <summary>
        /// 著作権者の名称のリストを収集する
        /// ノードをすべてまわり、著作権表示を出現回数順（降順）に並べ、合体させた文字列を返す
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        public string Collect(TileSetNodeComponent root)
        {
            var stats = new Dictionary<string, int>();
            CollectRecursive(stats, root);

            // 前回と異なる場合のみログを出す
            var logText = "Copyright holders: \n" + string.Join("\n", stats.Select(pair => $"{pair.Key} : {pair.Value}"));
            if (logText != lastLogText)
            {
                lastLogText = logText;
                Debug.Log(logText);
            }

            // 出現回数(Value)順にソートした後、Keyに入っている著作権者名称を出現回数順に並べる。
            return string.Join(", ", stats.OrderBy(pair => pair.Value).Select(pair => pair.Key));
        }

        private void CollectRecursive(Dictionary<string, int> stats, TileSetNodeComponent node)
        {
            if (node.Copyright != null)
            {
                foreach(var text in node.Copyright)
                {
                    if (stats.ContainsKey(text)) {
                        stats[text]++;
                    }
                    else
                    {
                        stats[text] = 1;
                    }
                }
            }

            foreach(Transform child in node.transform)
            {
                var childNode = child.GetComponent<TileSetNodeComponent>();
                if (childNode == null)
                {
                    continue;
                }
                CollectRecursive(stats, childNode);
            }
        }
    }
}