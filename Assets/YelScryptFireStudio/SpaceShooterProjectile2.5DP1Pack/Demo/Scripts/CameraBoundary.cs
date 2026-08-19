using UnityEngine;

namespace SSP25DP1
{
    public static class CameraBoundary
    {
        public static Vector2 GetHalfDimensionsInWorldUnits()
        {
            float width, height, ratio;

            Camera cam = Camera.main;
            ratio = cam.pixelWidth / (float)cam.pixelHeight;
            height = cam.orthographicSize * 2;
            width = height * ratio;

            return new Vector2(width, height) / 2f;
        }
    }
}