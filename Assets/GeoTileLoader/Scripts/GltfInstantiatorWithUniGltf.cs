#if GEOTILE_USE_UNIGLTF
using Cysharp.Threading.Tasks;
using System;
using System.IO;
using System.Threading;
using UniGLTF;
using UnityEngine;

// IGltfInstantiator のUniGLTFを利用した実装例
// UniGLTFの依存をこのファイルに閉じ込める意図がある
// UniGLTFはDraco圧縮に現時点 (2024/8/4)で対応していないため、Google 3D Map Tilesには利用できない。
// https://github.com/vrm-c/UniVRM/issues/2203
// このため、GLTFastの実装と比較して遅れており、メンテされていない部分がある (2024/8/4)

// ## インストール方法
//
// 以下のパッケージをインポートする
//  * https://github.com/vrm-c/UniVRM.git?path=/Assets/UniGLTF#v0.125.0
//
// GeoTileLoader.asmdef に以下のアセンブリのリファレンスを追加する
//  * UniGLTF
//
// Project Settings の Scripting Define Symbols に以下を追加する
//  * GEOTILE_USE_UNIGLTF

namespace GeoTile
{
    /// <summary>
    /// UniGLTFを利用したGLTF生成クラス
    /// </summary>
    public class GltfInstantiatorWithUniGltf : IGltfInstantiator
    {
        public async UniTask Instantiate(byte[] gltfData, double[] center, TileSetNodeComponent component, CancellationToken token)
        {
            // UniGLTFでロード
            var instance = await LoadGltfWithUniGLTF(gltfData, component.transform, token);
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
            instance.transform.localRotation = Quaternion.Euler(-90, 0, 0); // 3DTiles conversion (X: -90)
        }

        private async UniTask<GameObject> LoadGltfWithUniGLTF(byte[] data, Transform parent, CancellationToken token)
        {
            //var filePath = "sample.glb";
            //File.WriteAllBytes(filePath, data);
            //Debug.Log($"GLTF written to <{filePath}>");

            var gltfData = new GlbBinaryParser(data, "GLTF").Parse();
            using var loader = new UniGLTF.ImporterContext(gltfData);
            var instance = await loader.LoadAsync(new ImmediateCaller());
            instance.EnableUpdateWhenOffscreen();
            instance.ShowMeshes();
            instance.transform.SetParent(parent, false);
            return instance.gameObject;
        }
    }
}

#endif // GEOTILE_USE_UNIGLTF