using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GeoTileLoaderライブラリの設定用アセット
/// APIキーなどを分けて置くために利用
/// </summary>
[CreateAssetMenu(fileName = "GeoTileLoaderSettings", menuName = "GeoTileLoader/GeoTileLoaderSettings")]
public class GeoTileLoaderSettings : ScriptableObject
{
    [SerializeField]
    public string GoogleApiKeyFor3dMapTiles;
}
