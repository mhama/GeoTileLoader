using Cysharp.Threading.Tasks;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GeoTile.Samples
{
    /// <summary>
    /// マップの Copyright表示を TileSetHierarchy から取得してTextに反映するコンポーネント
    /// 後から TileSetHierarchy を変更しても良い。
    /// </summary>
    public class CopyrightAttributionUpdater : MonoBehaviour
    {
        [SerializeField]
        Text textLabel;

        [SerializeField]
        TileSetHierarchy hierarchy;

        /// <summary>
        /// Copyright取得元となる TileSetHierarchy を変更する。
        /// </summary>
        /// <param name="hierarchy"></param>
        public void SetHierarchy(TileSetHierarchy hierarchy)
        {
            RemoveHierarchyListeners();
            this.hierarchy = hierarchy;
            InitHierarchyListeners();
        }

        // Use this for initialization
        void Start()
        {
            InitHierarchyListeners();
        }

        private void OnDestroy()
        {
            RemoveHierarchyListeners();
        }

        private void InitHierarchyListeners()
        {
            if (hierarchy != null)
            {
                // 現時点の情報を更新
                OnCopyrightAttributionTextChanged(hierarchy.CopyrightAttributionText);
                hierarchy.OnCopyrightAttributionTextChanged += OnCopyrightAttributionTextChanged;
            }
        }

        private void RemoveHierarchyListeners()
        {
            if (hierarchy != null)
            {
                hierarchy.OnCopyrightAttributionTextChanged -= OnCopyrightAttributionTextChanged;
            }
        }

        void OnCopyrightAttributionTextChanged(string text)
        {
            textLabel.text = text;
        }
    }
}