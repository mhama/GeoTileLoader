
using System;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using GLTFast;
using GLTFast.Logging;
using UnityEngine;

namespace GeoTile
{
    /// <summary>
    /// glTFastを利用したGLTFインスタンス化クラス
    /// </summary>
    public class GltfInstantiatorWithGltFast : IGltfInstantiator
    {
        public async UniTask Instantiate(byte[] gltfData, double[] center, TileSetNodeComponent component, CancellationToken token)
        {
            // GLTFastでロード
            var instance = await LoadGltfWithGLTFast(gltfData, component.transform, new Uri(component.BaseJsonUrl), token);
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;
            Debug.Log("BoundingBoxType: " + component.BoundingBoxType);
            VectorD3 modelOffset = - component.TileSetInfoProvider.ModelCenterEcefCoordinate;
            if (component.BoundingBoxType == BoundingBoxType.Box)
            {
                if (center != null)
                {
                    pos = new Vector3(
                        (float)(center[0] + modelOffset.x),
                        (float)(center[1] + modelOffset.y),
                        (float)(center[2] + modelOffset.z)
                    );
                }
                else
                {
                    pos = new Vector3(
                        (float)(- component.GlobalBasePos[0] + modelOffset.x),
                        (float)(- component.GlobalBasePos[1] + modelOffset.y),
                        (float)(- component.GlobalBasePos[2] + modelOffset.z)
                    );
                }
            }
            else if (component.BoundingBoxType == BoundingBoxType.Region)
            {
                if (component.RootCenterLatLng == null)
                {
                    Debug.LogError("component.RootCenterLatLng is null");
                }
                var RootBasePos = CoordinateUtil.Vector3FromLatLngAlt(component.RootCenterLatLng);
                Debug.Log("RootBasePos: " + string.Join(", ", RootBasePos));

                if (center != null)
                {
                    // cullingCenterの緯度経度を地図中央にするテスト
                    pos = new Vector3(
                        (float)(center[0] + modelOffset.x),
                        (float)(center[1] + modelOffset.y),
                        (float)(center[2] + modelOffset.z)
                    );
                }
                else
                {
                    pos = new Vector3(
                        (float)(- RootBasePos[0] + modelOffset.x),
                        (float)(- RootBasePos[1] + modelOffset.y),
                        (float)(- RootBasePos[2] + modelOffset.z)
                    );
                }
            }
            instance.transform.localPosition = pos;
            instance.transform.localRotation = Quaternion.Euler(90, 180, 0); // GLTFast conversion (Y: 180) * 3DTiles conversion (X: -90)          
        }

        /// <summary>
        /// GLTFast と PlateauのGLTFの相性に問題があり、実質ロードできない！！！
        /// 
        /// CESIUM_RTC がRequired Extensionになっているが、GLTFastがサポートしておらず、
        /// そもそもGLTF2.0の仕様にCESIUM_RTCは存在しない！（1.0にはある。。。）
        /// </summary>
        /// <param name="data"></param>
        /// <param name="parent"></param>
        /// <param name="baseUri"></param>
        private async UniTask<GameObject> LoadGltfWithGLTFast(byte[] data, Transform parent, Uri baseUri, CancellationToken token)
        {
            GltFastInitializeDeferAgent();

            //var filePath = "sample.glb";
            //File.WriteAllBytes(filePath, data);
            //Debug.Log($"GLTF written to <{filePath}>");

            // 強引な手法
            // CESIUM_RTC拡張がないといわれてロードできない問題の対策
            // gltf(json形式)内のキー名 extensionsRequired をいじってしまうことで無効化する。
            //
            var result = SearchBinaryBlock.IndexOfSequence(data, Encoding.ASCII.GetBytes("extensionsRequired"), 0);
            if (result.Count > 0)
            {
                data[result[0]] = (byte)'X';
            }

            var gltf = new GltfImport(logger: new UnityLogger());
            try
            {
                bool success = await gltf.LoadGltfBinary(data, baseUri, cancellationToken: token);
                if (!success)
                {
                    Debug.LogError("load gltf failed.");
                    return null;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("load gltf error. " + e);
                return null;
            }

            var go = new GameObject("GLTF");
            go.transform.SetParent(parent, false);
            {
                bool success = await gltf.InstantiateMainSceneAsync(go.transform, cancellationToken: token);
                Debug.Log("load gltf result: " + success);
            }

            return go;
        }

        void GltFastInitializeDeferAgent()
        {
            if (Application.isEditor)
            {
                GltfImport.SetDefaultDeferAgent(new UninterruptedDeferAgent());
            }
        }
        
        /// <summary>
        /// glTFast用ロガー
        /// </summary>
        public class UnityLogger : ICodeLogger
        {

            public void Log(LogType logType, LogCode code, params string[] messages)
            {
                switch (logType)
                {
                    case LogType.Log:
                        Debug.Log("Code: " + code + " " + string.Join("\n, ", messages));
                        break;
                    case LogType.Error:
                        Debug.LogError("Code: " + code + " " + string.Join("\n, ", messages));
                        break;
                    case LogType.Warning:
                        Debug.LogWarning("Code: " + code + " " + string.Join("\n, ", messages));
                        break;
                    case LogType.Exception:
                        Debug.LogError("Code: " + code + " " + string.Join("\n, ", messages));
                        break;
                    default:
                        Debug.Log("Code: " + code + " " + string.Join("\n, ", messages));
                        break;
                }
            }

            public void Error(LogCode code, params string[] messages)
            {
                Debug.LogError(string.Join("\n, ", messages));
            }

            public void Error(string message)
            {
                Debug.LogError(message);
            }

            public void Info(LogCode code, params string[] messages)
            {
                Debug.Log(string.Join("\n, ", messages));
            }

            public void Info(string message)
            {
                Debug.Log(message);
            }

            public void Warning(LogCode code, params string[] messages)
            {
                Debug.LogWarning(string.Join("\n, ", messages));
            }

            public void Warning(string message)
            {
                Debug.LogWarning(message);
            }
        }
    }
}