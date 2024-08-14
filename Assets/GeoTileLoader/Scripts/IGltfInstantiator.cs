using Cysharp.Threading.Tasks;
using System.Threading;

namespace GeoTile
{
    /// <summary>
    /// タイルメッシュに付属するメタデータ情報
    /// 主にCopyright文字列を扱うイメージ
    /// </summary>
    public class TileMeshMetadata
    {
        public string Copyright { get; set; }
    }


    /// <summary>
    /// GLTFのインスタンス化インタフェース
    /// 利用するGLTFライブラリ(glTFast, UniGLTF etc)を変更できるようにするレイヤー
    /// </summary>
    public interface IGltfInstantiator
    {
        UniTask<(bool, TileMeshMetadata)> Instantiate(byte[] gltfData, double[] center, TileSetNodeComponent component, CancellationToken token);
    }

}