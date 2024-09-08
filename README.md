# About GeoTileLoader

Unity3D向けの、[Cesium 3D Tiles](https://cesium.com/why-cesium/3d-tiles/) 形式のデータをロード・表示するライブラリです。[Cesium for Unity](https://cesium.com/learn/unity/) と異なり、C#とglTFastパッケージを利用して作成されており、WebGLビルドでも利用できます。

主に [Google Photorealistic 3D Tiles](https://developers.google.com/maps/documentation/tile/3d-tiles) と [PLATEAU Streaming](https://github.com/Project-PLATEAU/plateau-streaming-tutorial) のデータでテストされています。


※ このライブラリは開発の初期段階のため、十分な利便性を提供しない可能性があります。

<img src="https://github.com/user-attachments/assets/c7fdb2ef-5fb7-4c0f-a3e7-7c93fa1eb396" width="500px">

# Requirements

Unity 2022.3.x
(Maybe Unity 2021 is also ok)

Google Photorealistic 3D Tilesを利用する場合

* 課金が有効なGoogle Cloud アカウント
* Google API Consoleにて、Map Tiles APIが有効な状態で発行された Google APIのキー

# How to use (case of Google Photorealistic 3D Tiles)

## Preparing API key

以下のあたりのドキュメントを参照して、Google APIのAPIキーを用意してください。
https://developers.google.com/maps/documentation/tile/cloud-setup

Assets/GeoTileLoader/GeoTileLoaderSettingsForGoogle.asset のインスペクタで、`Google Api Key For 3D Map Tiles` 欄にGoogle APIキーを設定してください。

## Use sample scene

Assets/GeoTileLoader/Samples/Google3DMapTiles シーンを開いてください。
PLAY後、`Load Hierarchy` ボタンを押すとデータ構造が読み込まれ、`Load 3D Models` ボタンを押すとモデルが表示されます。

`Load Hierarchy` ボタンを押したときに `403 Forbidden` のような表示が出る場合は、APIキーに問題がある可能性があります。以下を確認してください。
* Google Cloudの課金設定
* Map Tiles APIの有効化
* APIキーの発行

# How to use (case of PLATEAU Streming)

Assets/GeoTileLoader/Samples/PlateauStreamingSample シーンを開いてください。
PLAY後、`Load Hierarchy` ボタンを押すとデータ構造が読み込まれ、`Load 3D Models` ボタンを押すとモデルが表示されます。

## タイルセットを変更する場合

`Tile Set Manager` GameObjectのインスペクターの `Plateau Data Selector` コンポーネントのUIからデータセットを選択することができます。Regionで都道府県を選択し、その後データセットを選択できます。データセットを選択すると、`Tile Set Manager` の `Tile Set Json Url` および `Tile Set Title` のフィールドに反映されます。

データセットを選択した後、表示範囲を調整する必要があります。

表示したい範囲の中心の緯度・経度を、ブラウザ上のGoogle Mapを右クリックするなどして取得してください。デフォルトのままでは何も表示されない可能性があります。
緯度、経度および、表示半径を `Tile Set Manager` の `Culling Info` にセットしてください。
表示半径は、現状の実装では高さ方向も制約するため、表示半径は1000（メートル）やそれ以上を推奨します。

PLAY後、`Load Hierarchy` ボタンを押すとデータ構造が読み込まれ、`Load 3D Models` ボタンを押すとモデルが表示されます。

## PLATEAUデータセットの注意点

* 現時点では、テクスチャありのデータセットは利用できません。
  * PLATEAU StreammingのGLTFファイルは、テクスチャの圧縮に `WebPエンコーディング` を利用していますが、本ライブラリで利用している `glTFast` パッケージがこれに対応していないためです。
* 建築物以外のデータはうまく動作するかわかりません。
* データセット一覧のjsonを同梱していますが、データセットに更新があった場合、リンクが切れる可能性があります。この場合は、PLATEAU StreamingのデータセットのURLを直接 `Tile Set Json Url` にセットするなどしてください。

