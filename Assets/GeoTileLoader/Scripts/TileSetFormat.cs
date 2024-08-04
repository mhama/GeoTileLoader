using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace GeoTile
{
    [Serializable]
    public class TileSet
    {
        [SerializeField]
        public TileSetProperties properties;

        [SerializeField]
        public TileSetNode root;
    }

    [Serializable]
    public class TileSetProperties
    {
        [SerializeField]
        public TileSetPositionRange _height;
        [SerializeField]
        public TileSetPositionRange _x;
        [SerializeField]
        public TileSetPositionRange _y;
        [SerializeField]
        public TileSetPositionRange _z;
    }

    /// <summary>
    /// An array of 12 numbers that define an oriented bounding box. 
    /// The first three elements define the x, y, and z values for the center of the box.
    /// The next three elements(with indices 3, 4, and 5) define the x axis direction and half-length.
    /// The next three elements(indices 6, 7, and 8) define the y axis direction and half-length.
    /// The last three elements(indices 9, 10, and 11) define the z axis direction and half-length.
    /// </summary>
    [Serializable]
    public class TileSetBoundingVolume
    {
        [SerializeField]
        public double[] box;

        // [west, south, east, north, minimum height, maximum height] (緯度経度はラジアン値）
        [SerializeField]
        public double[] region;
    }

    [Serializable]
    public class TileSetPositionRange
    {
        [SerializeField]
        public double minimum;
        [SerializeField]
        public double maximum;
    }


    [Serializable]
    public class TileSetNode
    {
        [SerializeField]
        public TileSetBoundingVolume boundingVolume;

        [SerializeField]
        public double geometricError;

        /// <summary>
        /// REPLACE または ADD
        /// REPLACE: 親タイルを詳細化するとき、親タイルを消して置き換える
        /// ADD: 親タイルを詳細化するとき、親タイルはそのままで詳細を追加する
        /// </summary>
        [SerializeField]
        public string refine;

        [SerializeField]
        public TileSetContent content;

        [SerializeField]
        public List<TileSetNode> children;

        /// <summary>
        /// クローンメソッド
        /// クラスにSerialize対象フィールドを追加した場合、このメソッドもあわせて修正すること！
        /// </summary>
        /// <returns></returns>
        public TileSetNode CloneWithoutChildren()
        {
            return new TileSetNode()
            {
                boundingVolume = this.boundingVolume,
                geometricError = this.geometricError,
                content = this.content,
                refine = this.refine,
                children = new List<TileSetNode>(),
            };
        }
    }

    [Serializable]
    public class TileSetContent
    {
        [SerializeField]
        public TileSetBoundingVolume boundingVolume;

        [SerializeField]
        public string url;

        [SerializeField]
        public string uri;

        public string Url => string.IsNullOrEmpty(url) ? uri : url;

        public string contentUrlFileName
        {
            get
            {
                var url = Url;
                if (string.IsNullOrEmpty(url))
                {
                    return "";
                }
                if (url.StartsWith("/"))
                {
                    url = "https://x.com" + url;
                }
                // data/data0.b3dm のような値がくることもある
                if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
                {
                    url = "https://x.com/" + url;
                }
                var segments = new Uri(url).Segments;
                if (segments.Length == 0)
                {
                    return "";
                }
                return segments.Last();
            }
        }
        public string contentUrlQuery
        {
            get
            {
                var url = Url;
                if (string.IsNullOrEmpty(url))
                {
                    return "";
                }
                if (url.StartsWith("/"))
                {
                    url = "https://x.com" + url;
                }
                var query = new Uri(url).Query;
                if (query.StartsWith("?"))
                {
                    query = query.Substring(1);
                }
                return query;
            }
        }
    }
}
