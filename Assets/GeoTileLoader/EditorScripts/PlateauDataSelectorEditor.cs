using GeoTile.PlateauStreaming;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace GeoTile.Editor
{

    public class TileSetItem
    {
        public string TileSetUrl { get; set; }
        public string Region { get; set; }
        public string DataName { get; set; }

        public override string ToString()
        {
            return $"Region:{Region} DataName:{DataName} TileSetUrl:{TileSetUrl}";
        }
    }
    
    /// <summary>
    /// PLATEAUのデータセットを選択する
    /// データとして、以下のような並びのCSVをUnityプロジェクト直下のtilesets.csvから読み込む
    ///   地域名、データセット名、URL
    /// </summary>
    [CustomEditor(typeof(PlateauDataSelector))]
    public class PlateauDataSelectorEditor : UnityEditor.Editor
    {
        List<TileSetItem> allTileSetItems = new List<TileSetItem>();
        List<TileSetItem> regionTileSetItems = new List<TileSetItem>();
        private string[] regionTexts = new string[0];
        //int Target.SelectedRegionIndex = -1;
        //int Target.SelectedDataIndex = -1;
        string[] regionDataSetTexts = null;

        PlateauDataSelector Target => (PlateauDataSelector)target;

        int frameCount;

        private void OnEnable()
        {
            if (allTileSetItems.Count == 0)
            {
                LoadDataSets();
            }
        }

        private void LoadDataSets()
        {
            allTileSetItems.Clear();

            var jsonPath = AssetDatabase.GetAssetPath(Target.datasetJson);
            var text = File.ReadAllText(jsonPath);
            var datasetsArray = JsonUtility.FromJson<DatasetArray>(text);
            foreach(var dataset in datasetsArray.datasets)
            {
                allTileSetItems.Add(new TileSetItem()
                {
                    TileSetUrl = dataset.url,
                    Region = dataset.pref,
                    DataName = $"[{dataset.pref}] {dataset.name} lod:{dataset.lod} {(dataset.texture ? "texture" : "no texture")}",
                });
            }
            Debug.Log("LoadDataSets tileSetItems.Count: " + allTileSetItems.Count + " regionTexts.Length: " + regionTexts?.Length);
            Debug.Log("allTileSetItems: " + string.Join("\n", allTileSetItems));
            regionTexts = allTileSetItems.Select(i => i.Region).Distinct().ToArray();
        }

        public override void OnInspectorGUI()
        {
            frameCount++;

            //Debug.Log("OnInspectorGUI tileSetItems.Count: " + allTileSetItems.Count + " regionTexts.Length: " + regionTexts?.Length);
            base.OnInspectorGUI();


            // Region
            var prevSelectedRegionIndex = Target.SelectedRegionIndex;
            Target.SelectedRegionIndex = EditorGUILayout.Popup("Regions", Target.SelectedRegionIndex, regionTexts);

            // Region選択が変更されたら、Region内のデータのドロップダウンは一旦リセット
            if (Target.SelectedRegionIndex != prevSelectedRegionIndex) {
                    Target.SelectedDataIndex = -1;
            }

            if (Target.SelectedRegionIndex != prevSelectedRegionIndex || frameCount == 1)
            {
                if (Target.tileSetManager != null && Target.SelectedRegionIndex >= 0)
                {
                    string region = regionTexts[Target.SelectedRegionIndex];
                    Debug.Log("region: " + region + " Target.SelectedRegionIndex:" + Target.SelectedRegionIndex + " tileSetItems.Count: " + allTileSetItems.Count);; ;

                    regionTileSetItems.Clear();
                    var foundDataSets = allTileSetItems.Where(i => i.Region == region);
                    Debug.Log($"foundDataSets Count:{foundDataSets.Count()} data: " + String.Join(", ", foundDataSets.Select(i => i.DataName)));
                    regionTileSetItems.AddRange(foundDataSets);
                    regionDataSetTexts = regionTileSetItems.Select(i => i.DataName).ToArray();
                }
            }

            if (regionDataSetTexts != null)
            {
                var prevselectedDataIndex = Target.SelectedDataIndex;
                Target.SelectedDataIndex = EditorGUILayout.Popup("DataSets", Target.SelectedDataIndex, regionDataSetTexts);
                if (Target.SelectedDataIndex != prevselectedDataIndex)
                {
                    if (Target.tileSetManager != null && Target.SelectedRegionIndex >= 0 && Target.SelectedDataIndex >= 0)
                    {
                        Debug.Log("Target.SelectedDataIndex:" + Target.SelectedDataIndex + " regionTileSetItems.Count: " + regionTileSetItems.Count);
                        var tileSetItem = regionTileSetItems[Target.SelectedDataIndex];
                        Undo.RecordObject(Target.tileSetManager, "field change");
                        Target.tileSetManager.tileSetJsonUrl = tileSetItem.TileSetUrl;
                        Target.tileSetManager.tileSetTitle = tileSetItem.Region + ":" + tileSetItem.DataName;
                        EditorUtility.SetDirty(Target.tileSetManager);
                    }
                }
            }
        }

    }
}
