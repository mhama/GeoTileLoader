using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace GeoTile
{
    /// <summary>
    /// PLATEAUのデータセットを選択する
    /// 実際のコードはEditorのほうにある
    /// </summary>
    public class PlateauDataSelector : MonoBehaviour
    {
        // Start is called before the first frame update
        [SerializeField]
        public TileSetManager tileSetManager;

        [SerializeField]
        public TextAsset datasetJson;

        public int SelectedRegionIndex { get; set; } = -1;
        public int SelectedDataIndex { get; set; } = -1;
    }
}
