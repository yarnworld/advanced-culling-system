using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NGS.AdvancedCullingSystem.Dynamic
{
    // 相机参数快照结构体
    // 用于缓存相机的关键渲染参数，便于快速比较是否发生变化
    public struct DC_CameraSettings
    {
        // 相机渲染宽度（像素）
        public int width;

        // 相机渲染高度（像素）
        public int height;

        // 相机视野角（Field Of View）
        public float fov;

        // 相机远裁剪面距离
        public float farPlane;

        // 构造函数：从 Camera 组件中提取关键参数
        public DC_CameraSettings(Camera camera)
        {
            // 当前相机的像素宽度
            width = camera.pixelWidth;

            // 当前相机的像素高度
            height = camera.pixelHeight;

            // 当前相机的视野角
            fov = camera.fieldOfView;

            // 当前相机的远裁剪面距离
            farPlane = camera.farClipPlane;
        }
    }
}