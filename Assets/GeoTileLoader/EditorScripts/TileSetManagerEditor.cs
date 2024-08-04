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
        TileSetManager Target => (TileSetManager)target;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var isBusy = false;
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

            if (GUILayout.Button("Adjust Rotation for lat/lon"))
            {
                Target.transform.localRotation = Quaternion.AngleAxis(90, new Vector3(0, 0, 1))
                    * Quaternion.AngleAxis((float)-Target.cullingInfo.cullingLatDegree, new Vector3(0, 1, 0))
                    * Quaternion.AngleAxis((float)-Target.cullingInfo.cullingLonDegree, new Vector3(0, 0, 1));
            }
        }
    }
}
