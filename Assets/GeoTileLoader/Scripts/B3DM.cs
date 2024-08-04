using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GeoTile
{
    /// <summary>
    /// B3DMのヘッダ情報
    /// </summary>
    public struct B3DMHeader
    {
        public byte[] magic;
        public uint version;
        public uint byteLength;
        public uint featureTableJSONByteLength;
        public uint featureTableBinaryByteLength;
        public uint batchTableJSONByteLength;
        public uint batchTableBinaryByteLength;
    }

    /// <summary>
    /// B3DMファイルのデコードを行うクラス
    /// B3DMの仕様:
    /// https://github.com/CesiumGS/3d-tiles/blob/main/specification/TileFormats/Batched3DModel/README.adoc#tileformats-batched3dmodel-batched-3d-model
    /// </summary>
    public class B3DM
    {
        /// <summary>
        /// 読み取られたfeature部のjson
        /// </summary>
        public string FeatureJsonText { get; private set; }

        /// <summary>
        /// 読み取られたbatch部のjson
        /// </summary>
        public string BatchJsonText { get; private set; }

        /// <summary>
        /// 読み取られたGLTFバイナリデータ
        /// </summary>
        public ArraySegment<byte> GltfData { get; private set; }

        /// <summary>
        /// 読み取られたGLTFのJSON部
        /// </summary>
        public string GltfJsonText { get; private set; }

        /// <summary>
        /// バイト列から情報を読み取る
        /// 特にGLTFバイナリデータおよびGLTFのJSONテキストの読み取り
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public bool Read(byte[] data)
        {
            B3DMHeader header = new B3DMHeader();
            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                header.magic = reader.ReadBytes(4);
                header.version = reader.ReadUInt32();
                header.byteLength = reader.ReadUInt32();
                header.featureTableJSONByteLength = reader.ReadUInt32();
                header.featureTableBinaryByteLength = reader.ReadUInt32();
                header.batchTableJSONByteLength = reader.ReadUInt32();
                header.batchTableBinaryByteLength = reader.ReadUInt32();

                if (!header.magic.SequenceEqual(new byte[] { (byte)'b', (byte)'3', (byte)'d', (byte)'m' }))
                {
                    Debug.LogError("magic not ok! magic: " + string.Join(",", header.magic) + " = " + string.Join(",", header.magic.Select(b => (char)b)));
                    return false;
                }
            }
            const int b3dmHeaderSize = 28;

            int featureTableBasePos = b3dmHeaderSize;
            var featureJsonData = new ArraySegment<byte>(data, featureTableBasePos, (int)header.featureTableJSONByteLength);
            if (header.featureTableJSONByteLength > 0)
            {
                FeatureJsonText = Encoding.UTF8.GetString(featureJsonData.ToArray());
            }

            int batchTableBasePos = b3dmHeaderSize + 
                (int) header.featureTableJSONByteLength + 
                (int) header.featureTableBinaryByteLength;
            var batchJsonData = new ArraySegment<byte>(data, batchTableBasePos, (int)header.batchTableJSONByteLength);
            if (header.batchTableJSONByteLength > 0)
            {
                BatchJsonText = Encoding.UTF8.GetString(batchJsonData.ToArray());
            }

            int pos = b3dmHeaderSize
                      + (int)header.featureTableJSONByteLength
                      + (int)header.featureTableBinaryByteLength
                      + (int)header.batchTableJSONByteLength
                      + (int)header.batchTableBinaryByteLength;
            if (data.Length < pos)
            {
                Debug.LogError("no GLTF block found. data.Length: " + data.Length + " pos: " + pos);
                return false;
            }

            GltfData = new ArraySegment<byte>(data, pos, data.Length - pos);

            return ReadGLTF(GltfData);
        }

        /// <summary>
        /// 参考: https://www.khronos.org/registry/glTF/specs/2.0/glTF-2.0.html#glb-file-format-specification-structure
        /// </summary>
        /// <param name="gltfData"></param>
        /// <returns></returns>
        bool ReadGLTF(ArraySegment<byte> gltfData)
        {
            using (var stream = new MemoryStream(gltfData.ToArray())) // TODO: メモリを食うのでよくない
            using (var reader = new BinaryReader(stream))
            {
                var magic = reader.ReadBytes(4);
                var version = reader.ReadUInt32();
                var length = reader.ReadUInt32();
                if (!magic.SequenceEqual(new byte[] { (byte)'g', (byte)'l', (byte)'T', (byte)'F' }))
                {
                    Debug.LogError("magic not ok! magic: " + string.Join(",", magic));
                    return false;
                }
                var chunkLength = reader.ReadUInt32();
                var chunkType = reader.ReadUInt32();

                var gltfJsonData = new ArraySegment<byte>(gltfData.ToArray(), 20, (int) chunkLength);
                if (gltfJsonData.Count > 0)
                {
                    GltfJsonText = Encoding.UTF8.GetString(gltfJsonData.ToArray());
                }
            }
            return true;
        }
    }
}
