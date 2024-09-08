using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace GeoTile.Samples
{
    /// <summary>
    /// カリングの中心座標をInputFieldから設定する
    /// InputFieldには、"lat, lon" の形式で入力される
    /// 例: "35.2, 139.45"
    /// この形式にした理由としては、GoogleMapsからコピペしやすいようにするため。
    /// </summary>
    public class CullingLocationChanger : MonoBehaviour
    {
        [SerializeField]
        private TileSetManager manager;

        [SerializeField]
        private InputField latLonInputField;

        private void Start()
        {
            ReflectLatLonFromCullingInfo();
            latLonInputField.onEndEdit.AddListener(OnEndEditLatLonInputField);
        }

        private void OnDestroy()
        {
            latLonInputField.onEndEdit.RemoveListener(OnEndEditLatLonInputField);
        }

        private void OnEndEditLatLonInputField(string text)
        {
            var values = text.Split(",");
            if (values.Length != 2)
            {
                ReflectLatLonFromCullingInfo();
                return;
            }
            try
            {
                var floatValues = values.Select(v => v.Trim()).Select(v => float.Parse(v)).ToArray();
                ApplyToCullingInfo(floatValues[0], floatValues[1]);
            }
            catch (Exception e)
            {
                Debug.LogError("LatLon InputField Parse Exception. error: "+ e);
                ReflectLatLonFromCullingInfo();
                return;
            }
        }

        private void ReflectLatLonFromCullingInfo()
        {
            latLonInputField.text = $"{manager.cullingInfo.cullingLatDegree:F8}, {manager.cullingInfo.cullingLonDegree:F8}";
        }

        private void ApplyToCullingInfo(float lat, float lon)
        {
            manager.cullingInfo.cullingLatDegree = lat;
            manager.cullingInfo.cullingLonDegree = lon;
            ReflectLatLonFromCullingInfo();
        }        
    }
}
