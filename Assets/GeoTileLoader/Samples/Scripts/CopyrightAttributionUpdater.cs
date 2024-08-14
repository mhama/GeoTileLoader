using Cysharp.Threading.Tasks;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GeoTile.Samples
{
    public class CopyrightAttributionUpdater : MonoBehaviour
    {
        [SerializeField]
        Text textLabel;

        [SerializeField]
        TileSetHierarchy hierarchy;

        public void SetHierarchy(TileSetHierarchy hierarchy)
        {
            RemoveHierarchyListeners();
            this.hierarchy = hierarchy;
            AddHierarchyListeners();
        }

        // Use this for initialization
        void Start()
        {
            AddHierarchyListeners();
        }

        private void OnDestroy()
        {
            RemoveHierarchyListeners();
        }

        private void AddHierarchyListeners()
        {
            if (hierarchy != null)
            {
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