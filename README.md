# About GeoTileLoader

Japanese README is available [here](README_JA.md).

GeoTileLoader is a library for Unity3D that loads and displays data in the [Cesium 3D Tiles](https://cesium.com/why-cesium/3d-tiles/) format. Unlike [Cesium for Unity](https://cesium.com/learn/unity/), it is built with C# and the glTFast package so it can also be used in WebGL builds.

It is mainly tested with data from [Google Photorealistic 3D Tiles](https://developers.google.com/maps/documentation/tile/3d-tiles) and [PLATEAU Streaming](https://github.com/Project-PLATEAU/plateau-streaming-tutorial).

*This library is in an early stage of development and may not provide full functionality.*

<img src="https://github.com/user-attachments/assets/c7fdb2ef-5fb7-4c0f-a3e7-7c93fa1eb396" width="500px">

# Requirements

* Unity 2022.3.x (Unity 2021 might also work)

When using Google Photorealistic 3D Tiles you also need:

* A Google Cloud account with billing enabled
* A Google API key issued with Map Tiles API enabled in the Google API Console

# How to install

* Install UniTask
  * On the Package Manager window click the `+` icon in the upper-left corner
  * Choose **Add package from git URL...**
  * Enter the URL below and click **Add**

  ```
  https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.5
  ```

* Install GeoTileLoader
  * In the same way as above, enter the following URL and click **Add**

  ```
  https://github.com/mhama/GeoTileLoader.git?path=Assets/GeoTileLoader
  ```

# How to use (Google Photorealistic 3D Tiles)

## Preparing an API key

See the following documentation to obtain a Google API key:
https://developers.google.com/maps/documentation/tile/cloud-setup

Set the API key in the `Google Api Key For 3D Map Tiles` field of `Assets/GeoTileLoader/GeoTileLoaderSettingsForGoogle.asset`.

## Use the sample scene

Open the `Assets/GeoTileLoader/Samples/Google3DMapTiles` scene. After pressing **Play**, click `Load Hierarchy` to load the tile hierarchy and then `Load 3D Models` to display the models.

If you see a `403 Forbidden` message when pressing `Load Hierarchy`, there may be a problem with your API key. Please check the following:
* Billing settings in Google Cloud
* Map Tiles API is enabled
* The API key has been issued correctly

# How to use (PLATEAU Streaming)

Open the `Assets/GeoTileLoader/Samples/PlateauStreamingSample` scene. After pressing **Play**, click `Load Hierarchy` to load the structure and then `Load 3D Models` to display the models.

## Changing the tileset

From the `Plateau Data Selector` component in the `Tile Set Manager` GameObject inspector you can select a dataset. Select a prefecture in the Region field and then choose a dataset. When selected, the `Tile Set Json Url` and `Tile Set Title` fields of `Tile Set Manager` will be updated.

After selecting a dataset, you need to adjust the viewing area.

Get the latitude and longitude of the center of the area you want to view from Google Maps or similar. Without adjustment, nothing may be shown. Set the latitude, longitude and display radius in the `Culling Info` section of `Tile Set Manager`. Because the current implementation also constrains the vertical range, a radius of 1000 meters or more is recommended.

After pressing **Play**, click `Load Hierarchy` to load the tile hierarchy and then `Load 3D Models` to display the models.

## Notes about PLATEAU datasets

* Currently datasets that include textures cannot be used.
  * PLATEAU Streaming uses `WebP encoding` for texture compression in its GLTF files, but the `glTFast` package used by this library does not support it.
* Datasets other than building models may not work correctly.
* A JSON list of datasets is bundled, but links may become invalid if the dataset is updated. If this happens, set the PLATEAU Streaming dataset URL directly in `Tile Set Json Url`.
