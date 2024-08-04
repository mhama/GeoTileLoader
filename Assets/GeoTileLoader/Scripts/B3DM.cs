using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GeoTile
{

    //[StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct B3DMHeader
    {
        //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] magic;
        public uint version;
        public uint byteLength;
        public uint featureTableJSONByteLength;
        public uint featureTableBinaryByteLength;
        public uint batchTableJSONByteLength;
        public uint batchTableBinaryByteLength;
    }


    public class B3DM
    {

        public ArraySegment<byte> FeatureJsonData { get; private set; }
        public string FeatureJsonText { get; private set; }

        public ArraySegment<byte> BatchJsonData { get; private set; }
        public string BatchJsonText { get; private set; }

        public ArraySegment<byte> GltfData { get; private set; }

        public ArraySegment<byte> GltfJsonData { get; private set; }
        public string GltfJsonText { get; private set; }

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
            int b3dmHeaderSize = 28;

            int featureTableBasePos = b3dmHeaderSize;
            FeatureJsonData = new ArraySegment<byte>(data, featureTableBasePos, (int)header.featureTableJSONByteLength);
            if (header.featureTableJSONByteLength > 0)
            {
                FeatureJsonText = Encoding.UTF8.GetString(FeatureJsonData.ToArray());
            }

            int batchTableBasePos = b3dmHeaderSize + 
                (int) header.featureTableJSONByteLength + 
                (int) header.featureTableBinaryByteLength;
            BatchJsonData = new ArraySegment<byte>(data, batchTableBasePos, (int)header.batchTableJSONByteLength);
            if (header.batchTableJSONByteLength > 0)
            {
                BatchJsonText = Encoding.UTF8.GetString(BatchJsonData.ToArray());
            }

            int pos = 28
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

            ReadGLTF(GltfData);

            //onReceiveGltfData(GltfData.ToArray());

            return true;
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


                GltfJsonData = new ArraySegment<byte>(gltfData.ToArray(), 20, (int) chunkLength);
                if (GltfJsonData.Count > 0)
                {
                    GltfJsonText = Encoding.UTF8.GetString(GltfJsonData.ToArray());
                }
            }
            return true;
        }
    }

    [Serializable]
    public class GltfMetadata
    {
        [SerializeField]
        public GltfMetadataExtensions extensions;
    }

    [Serializable]
    public class GltfMetadataExtensions
    {
        [SerializeField]
        public GltfMetadataCesiumTrcExtension CESIUM_RTC;
    }

    [Serializable]
    public class GltfMetadataCesiumTrcExtension
    {
        [SerializeField]
        public double[] center;
    }

}
