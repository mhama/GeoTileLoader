using Cysharp.Threading.Tasks;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace GeoTile
{
    /// <summary>
    /// TileSetHierarchyの操作用UIを表示する
    /// </summary>
    [CustomEditor(typeof(TileSetHierarchy))]
    public class TileSetHierarchyEditor : UnityEditor.Editor
    {
        TileSetHierarchy Target { get; set; }

        private void OnEnable()
        {
            Target = (TileSetHierarchy)target;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            if (GUILayout.Button("Load Json Recursively (limit depth 100, limit node 1000)"))
            {
                Target.LoadSubTrees(100, 1000, Target.GetCancellationTokenOnDestroy()).Forget();
            }
            if (GUILayout.Button("Load GLTF Models"))
            {
                Target.Load3DModels(100, Target.GetCancellationTokenOnDestroy()).Forget();
            }
        }
    }
}