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
        public GltfMetadataCesiumRtcExtension CESIUM_RTC;
    }

    [Serializable]
    public class GltfMetadataCesiumRtcExtension
    {
        [SerializeField]
        public double[] center;
    }
}