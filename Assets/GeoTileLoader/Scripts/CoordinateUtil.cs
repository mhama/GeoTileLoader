using System;
using System.Collections.Generic;
using System.Linq;

/*
 * LatLngAlt(Geodetic) から ECEF への変換
 * 参考:
 *  https://en.wikipedia.org/wiki/Geographic_coordinate_conversion#From_geodetic_to_ECEF_coordinates
 *  https://www.oc.nps.edu/oc2902w/coord/llhxyz.htm
 */
namespace GeoTile
{
    /// <summary>
    /// 緯度経度高度を表す値クラス
    /// </summary>
    public class LatLngAlt
    {
        public double LatRad { get; set; }
        public double LngRad { get; set; }
        public double AltitudeMeter { get; set; }

        public LatLngAlt() { }

        public LatLngAlt(double latRad, double lngRad, double altMeters)
        {
            LatRad = latRad;
            LngRad = lngRad;
            AltitudeMeter = altMeters;
        }
    }
    
    /// <summary>
    /// 参考
    /// UniGLTFでGLTF→Unityで座標系変換しているところ
    /// https://github.com/vrm-c/UniVRM/blob/master/Assets/UniGLTF/Runtime/UniGLTF/IO/NodeImporter.cs#L132
    /// 
    /// 基本はZ軸を反転している模様。
    /// </summary>
    public static class CoordinateUtil
    {
        /// <summary>
        /// 3DTileの座標系をUnity座標系に変換する。
        /// 右手系 → 左手系
        /// 
        /// 上がZ → 上がY に変換
        /// じゃなくてZを反転させるだけ。
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        public static double[] TileCoordToUnity(double[] src)
        {
            return new double[]
            {
                //src[1], -src[2], src[0]
                src[0], src[1], -src[2]
            };
        }

        public static VectorD3 DoubleArrayToVector3(double[] src)
        {
            return new VectorD3((float)src[0], (float)src[1], (float)src[2]);
        }

        /// <summary>
        /// regionの中心の緯度経度を求める
        /// 
        /// regionの定義: [west(経度), south（緯度）, east（経度）, north（緯度）, minimum height, maximum height]
        /// (緯度経度はラジアン値）
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        public static LatLngAlt CenterLatLngOfRegion(double[] src)
        {
            // 経度０をまたぐような微妙な場合の計算をはしょっている。
            return new LatLngAlt()
            {
                LatRad = (src[1] + src[3]) * 0.5,
                LngRad = (src[0] + src[2]) * 0.5,
                AltitudeMeter = (src[4] + src[5]) * 0.5,
            };
        }

        /// <summary>
        /// 緯度経度からEcef座標を求める
        /// </summary>
        /// <param name="latLngAlt"></param>
        /// <returns></returns>
        public static double[] EcefFromLatLngAlt(LatLngAlt latLngAlt)
        {
            double radiusEquatorial = 6378137.0;
            double radiusPolar = 6356752.3142;
            double e2 = 1 - ((radiusPolar * radiusPolar) / (radiusEquatorial * radiusEquatorial));

            double radiusOnTheLatitude = radiusEquatorial / Math.Sqrt(1 - e2 * Math.Pow(Math.Sin(latLngAlt.LatRad), 2));

            double x = Math.Cos(latLngAlt.LngRad) * Math.Cos(latLngAlt.LatRad) * (radiusOnTheLatitude + latLngAlt.AltitudeMeter);
            double y = Math.Sin(latLngAlt.LngRad) * Math.Cos(latLngAlt.LatRad) * (radiusOnTheLatitude + latLngAlt.AltitudeMeter);
            double z = Math.Sin(latLngAlt.LatRad) * (
                ((radiusPolar * radiusPolar) / (radiusEquatorial * radiusEquatorial)) * radiusOnTheLatitude + latLngAlt.AltitudeMeter
                );

            return new double[] { x, y, z };
        }

        /// <summary>
        /// 緯度軽度からUnity座標を求める
        /// </summary>
        /// <param name="latLngAlt"></param>
        /// <returns></returns>
        public static double[] Vector3FromLatLngAlt(LatLngAlt latLngAlt)
        {
            return TileCoordToUnity(EcefFromLatLngAlt(latLngAlt)); // こっちのほうが正しいんじゃなかったっけ？謎がある。

            //return EcefFromLatLngAlt(latLngAlt);
        }

        public static double DegreeToRadian(double degree)
        {
            return degree * Math.PI / 180.0;
        }
        
        /// <summary>
        /// Boxから衝突検出用球体を求める
        /// Boxデータはタイルの座標系でXYZの座標が、中央、X方向ベクトル、Y方向ベクトル、Z方向ベクトル と並んでいる。
        /// 詳細は3D Tiles仕様のBounding Volumesを参照
        /// </summary>
        /// <param name="boxCoords">12要素のBoxデータ</param>
        /// <returns></returns>
        public static SphereCoordsUnity GetBoundingSphereFromBoxCoords(double[] boxCoords)
        {
            var centerVec = new VectorD3(
                CoordinateUtil.TileCoordToUnity(new double[] { boxCoords[0], boxCoords[1], boxCoords[2] })
            );
            var xDirVec = new VectorD3(
                CoordinateUtil.TileCoordToUnity(new double[] { boxCoords[3], boxCoords[4], boxCoords[5] })
            );
            var yDirVec = new VectorD3(
                CoordinateUtil.TileCoordToUnity(new double[] { boxCoords[6], boxCoords[7], boxCoords[8] })
            );
            var zDirVec = new VectorD3(
                CoordinateUtil.TileCoordToUnity(new double[] { boxCoords[9], boxCoords[10], boxCoords[11] })
            );
            var maxRadius = Math.Max(Math.Max(xDirVec.magnitude, yDirVec.magnitude), zDirVec.magnitude);

            return new SphereCoordsUnity()
            {
                center = centerVec,
                radiusMeters = maxRadius
            };
        }

        /// <summary>
        /// Regionから衝突検出用球体を求める
        /// regionの要素は以下のような並び。
        /// west（経度）, south（緯度）, east（経度）, north（緯度）, minimum height（高度）, maximum height（高度）
        /// 詳細は3D Tiles仕様のBounding Volumesを参照
        /// </summary>
        /// <param name="region">6要素のRegion</param>
        /// <returns></returns>
        public static SphereCoordsUnity GetBoundingSphereFromRegionCoords(double[] region)
        {
            // west, south, east, north, minimum height, maximum height
            var westLng = region[0];
            var southLat = region[1];
            var eastLng = region[2];
            var northLat = region[3];
            var minAlt = region[4];
            var maxAlt = region[5];

            var targetPointsLatLngAlt = new LatLngAlt[]
            {
                new(southLat, westLng, minAlt),
                new(southLat, eastLng, minAlt),
                new(northLat, westLng, minAlt),
                new(northLat, eastLng, minAlt),
                new(southLat, westLng, maxAlt),
                new(southLat, eastLng, maxAlt),
                new(northLat, westLng, maxAlt),
                new(northLat, eastLng, maxAlt),
            };
            var targetPointsInUnityCoords = targetPointsLatLngAlt.Select(
                t => CoordinateUtil.TileCoordToUnity(CoordinateUtil.EcefFromLatLngAlt(t))
                );
            double[] center = new double[3]
            {
                targetPointsInUnityCoords.Average(v => v[0]),
                targetPointsInUnityCoords.Average(v => v[1]),
                targetPointsInUnityCoords.Average(v => v[2])
            };
            double radius = targetPointsInUnityCoords.Max(v =>
                Math.Sqrt(Math.Pow(v[0] - center[0], 2)
                          + Math.Pow(v[1] - center[1], 2)
                          + Math.Pow(v[1] - center[1], 2)));

            return new SphereCoordsUnity()
            {
                center = new VectorD3(center[0], center[1], center[2]),
                radiusMeters = radius,
            };
        }
    }
}