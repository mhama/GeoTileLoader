using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace GeoTile.Editor
{
    [CustomEditor(typeof(TileSetManager))]
    class TileSetManagerEditor : UnityEditor.Editor
    {
        private bool isBusy;

        TileSetManager Target => (TileSetManager)target;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var buttonName = isBusy ? "Loading..." : "Create Hierarchy";
            if (GUILayout.Button(buttonName))
            {
                isBusy = true;
                EditorUtility.SetDirty(target);
                Target.ReadJson(null, e =>
                {
                    isBusy = false;
                    EditorUtility.SetDirty(target);
                });
            }
        }
    }
}
