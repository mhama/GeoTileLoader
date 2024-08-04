namespace GeoTile
{
    /// <summary>
    /// GLTFのインスタンス化インタフェース
    /// 利用するGLTFライブラリ(glTFast, UniGTTF etc)を変更できるようにするレイヤー
    /// TODO: async化
    /// </summary>
    public interface IGltfInstantiator
    {
        void Instantiate(byte[] gltfData, double[] center, TileSetNodeComponent component);
    }
}