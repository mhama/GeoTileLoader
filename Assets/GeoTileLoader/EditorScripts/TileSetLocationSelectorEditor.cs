using System;
using UnityEditor;
using UnityEngine;

namespace GeoTile.Editor
{
    /// <summary>
    /// TileSetManagerに対して、ロード対象の緯度経度をプリセットから指定する
    /// </summary>
    [CustomEditor(typeof(TileSetLocationSelector))]
    public class TileSetLocationSelectorEditor : UnityEditor.Editor
    {
        TileSetLocationSelector Target => (TileSetLocationSelector)target;

        private int selectedLocationIndex;

        private string[] locationTexts =
        {
            "渋谷ハチ公, 35.6590527, 139.7006323",
            "新宿御苑STYLY社, 35.6910750, 139.7107426",
            "新宿都庁, 35.6895724, 139.6921793",
            "東京駅, 35.68142820, 139.76587257",
            "東京ビッグサイト, 35.6297785, 139.7940755",
            "品川駅港南口, 35.6289477, 139.7414514",
            "羽田空港第一T, 35.5484899, 139.7832987",
            "天王洲ふれあい橋, 35.6232330, 139.7476432",
        };

        private void OnEnable()
        {
            
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            selectedLocationIndex = EditorGUILayout.Popup("Locations", selectedLocationIndex, locationTexts);
            if (GUILayout.Button("Select"))
            {
                var manager = Target.GetComponent<TileSetManager>();
                if (manager != null)
                {
                    if (selectedLocationIndex >= 0 && selectedLocationIndex < locationTexts.Length)
                    {
                        Undo.RecordObject(manager, "CullingInfo change");
                        var values = locationTexts[selectedLocationIndex].Split(",");
                        manager.cullingInfo.cullingLatDegree = Double.Parse(values[1]);
                        manager.cullingInfo.cullingLonDegree = Double.Parse(values[2]);
                        EditorUtility.SetDirty(manager);
                    }
                }
            }

        }
    }
}
