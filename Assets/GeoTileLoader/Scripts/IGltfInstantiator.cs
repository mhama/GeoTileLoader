using Cysharp.Threading.Tasks;
using System.Threading;

namespace GeoTile
{
    /// <summary>
    /// GLTFのインスタンス化インタフェース
    /// 利用するGLTFライブラリ(glTFast, UniGLTF etc)を変更できるようにするレイヤー
    /// </summary>
    public interface IGltfInstantiator
    {
        UniTask Instantiate(byte[] gltfData, double[] center, TileSetNodeComponent component, CancellationToken token);
    }
}