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
        /// カリング範囲を示すコライダー
        /// </summary>
        private Collider cullCollider;

        public static readonly string ColliderGameObjectName = "Collider";

        /// <summary>
        /// カリングを行い、配下にある指定範囲以外のTileSetNodeComponentをDestroyする
        /// </summary>
        public void DoCulling(Collider cullCollider)
        {
            this.cullCollider = cullCollider;
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
            // UnityのColliderがある場合はそれで判定
            var colliderGO = tileSetNodeComponent.transform.Find(SphereCulling.ColliderGameObjectName);
            var collider = colliderGO?.GetComponent<Collider>();
            if (cullCollider != null && collider != null)
            {
                Vector3 direction;
                float distance;
                bool areCollide = Physics.ComputePenetration(
                    cullCollider, cullCollider.transform.position, cullCollider.transform.rotation,
                    collider, collider.transform.position, collider.transform.rotation,
                    out direction, out distance);
                Debug.Log($"Check Collision node: {tileSetNodeComponent} areCollide: {areCollide}");
                if (!areCollide)
                {
                    DestroyImmediate(tileSetNodeComponent.gameObject);
                    return;
                }
            }

            /* UnityのColliderを利用しない方法は一旦利用しないことにする
            else
            {
                // UnityのColliderがない場合は包含Sphereで判定

                if (tileSetNodeComponent.BoundingBoxType != BoundingBoxType.Box
                    && tileSetNodeComponent.BoundingBoxType != BoundingBoxType.Region)
                {
                    return;
                }

                var boundingSphere = tileSetNodeComponent.GetBoundingSphere();
                if (!cullSphereUnity.IsCollidesWith(boundingSphere))
                {
                    DestroyImmediate(tileSetNodeComponent.gameObject);
                    return;
                }
            }
            */

            // Destroyしない場合は子nodeも確認
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