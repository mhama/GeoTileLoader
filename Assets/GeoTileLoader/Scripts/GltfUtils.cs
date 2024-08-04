using System;
using UnityEngine;

namespace GeoTile
{
    [Serializable]
    public class GltfMetadata
    {
        [SerializeField]
        public GltfMetadataExtensions extensions;
    }

    [Serializable]
    public class GltfMetadataExtensions
    {
        [SerializeField]
        public GltfMetadataCesiumTrcExtension CESIUM_RTC;
    }

    [Serializable]
    public class GltfMetadataCesiumTrcExtension
    {
        [SerializeField]
        public double[] center;
    }
}