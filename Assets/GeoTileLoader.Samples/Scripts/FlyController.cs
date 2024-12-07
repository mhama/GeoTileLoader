using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GeoTile.Samples
{
    /// <summary>
    /// 基本的なカメラ移動スクリプト
    /// WASDQEで前後左右上下
    /// SHIFT押しながらWASDQEで高速移動
    /// マウス左ボタン押しながら上下左右で回転
    /// </summary>
    public class FlyController : MonoBehaviour
    {
        private Vector2 prevMousePositionRatio;

        void Update()
        {
            if (Time.deltaTime > 0.2f)
            {
                return;
            }
            var basicSpeedMetersPerSec = 30;
            var fastSpeedMetersPerSec = 120;
            var speedMetersPerSec = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                ? fastSpeedMetersPerSec
                : basicSpeedMetersPerSec;
            if (Input.GetKey(KeyCode.W))
            {
                transform.position += transform.forward * speedMetersPerSec * Time.deltaTime;
            }
            if (Input.GetKey(KeyCode.A))
            {
                transform.position += -transform.right * speedMetersPerSec * Time.deltaTime;
            }
            if (Input.GetKey(KeyCode.S))
            {
                transform.position += -transform.forward * speedMetersPerSec * Time.deltaTime;
            }
            if (Input.GetKey(KeyCode.D))
            {
                transform.position += transform.right * speedMetersPerSec * Time.deltaTime;
            }
            if (Input.GetKey(KeyCode.E))
            {
                transform.position += transform.up * speedMetersPerSec * Time.deltaTime;
            }
            if (Input.GetKey(KeyCode.Q))
            {
                transform.position += -transform.up * speedMetersPerSec * Time.deltaTime;
            }

            float referenceScreenSize = Math.Min(Screen.width, Screen.height);

            // 画面サイズいっぱいにカーソルを動かしたら何度回転するかという比率
            float screenAngleRatio = 120.0f;
            var mousePositionRatio = new Vector2(Input.mousePosition.x, Input.mousePosition.y) / referenceScreenSize;

            // 初回極端な差分が出ないように同じ値にしておく
            if (prevMousePositionRatio == Vector2.zero)
            {
                prevMousePositionRatio = mousePositionRatio;
            }
            // クリックした最初のフレームも極端な差分が出がちのため同じ値にしておく
            if (Input.GetMouseButtonDown(0))
            {
                prevMousePositionRatio = mousePositionRatio;
            }

            if (Input.GetMouseButton(0))
            {
                var mousePositionDiff = mousePositionRatio - prevMousePositionRatio;
                var euler = transform.rotation.eulerAngles;
                euler.z = 0;
                euler.y += mousePositionDiff.x * screenAngleRatio;
                euler.x += -mousePositionDiff.y * screenAngleRatio;
                transform.rotation = Quaternion.Euler(euler);
            }

            prevMousePositionRatio = mousePositionRatio;
        }
    }
}