using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GeoTile
{
    /// <summary>
    /// 球状の領域を表す。カリングに利用。Unity座標系
    /// </summary>
    public class SphereCoordsUnity
    {
        public VectorD3 center;
        public double radiusMeters;

        public bool IsCollidesWith(SphereCoordsUnity sphere)
        {
            var distance = (center - sphere.center).magnitude;
            return distance < (radiusMeters + sphere.radiusMeters);
        }

        public override string ToString()
        {
            return $"{{ SphereCoordsUnity center:{center}, radius: {radiusMeters} }}";
        }
    }

    /// <summary>
    /// 球状領域でカリングする
    /// </summary>
    public class SphereCulling : MonoBehaviour
    {
        public SphereCoordsUnity cullSphereUnity;

        /// <summary>
        /// カリングを行い、配下にある指定範囲以外のTileSetNodeComponentをDestroyする
        /// </summary>
        public void DoCulling()
        {
            TileSetNodeComponent comp = GetComponent<TileSetNodeComponent>();
            if (comp == null)
            {
                Debug.LogWarning("no TileSetNodeComponent found.");
                return;
            }
            CullResursive(comp, cullSphereUnity);
        }

        /// <summary>
        /// 指定のTileSetNodeComponentおよびその配下のTileSetNodeComponentについて、cullSphereUnityの範囲外だったらDestroyする
        /// </summary>
        /// <param name="tileSetNodeComponent"></param>
        /// <param name="cullSphereUnity"></param>
        private void CullResursive(TileSetNodeComponent tileSetNodeComponent, SphereCoordsUnity cullSphereUnity)
        {
            if (tileSetNodeComponent.BoundingBoxType != BoundingBoxType.Box
                && tileSetNodeComponent.BoundingBoxType != BoundingBoxType.Region)
            {
                return;
            }
            var boundingSphere = tileSetNodeComponent.GetBoundingSphere();
            if (!cullSphereUnity.IsCollidesWith(boundingSphere))
            {
                DestroyImmediate(tileSetNodeComponent.gameObject);
            }
            else
            {
                var targets = new List<TileSetNodeComponent>();
                foreach(Transform child in tileSetNodeComponent.transform)
                {
                    var component = child.GetComponent<TileSetNodeComponent>();
                    if (component)
                    {
                        targets.Add(component);
                    }
                }
                foreach (var component in targets)
                {
                    CullResursive(component, cullSphereUnity);
                }

            }
        }
    }
}