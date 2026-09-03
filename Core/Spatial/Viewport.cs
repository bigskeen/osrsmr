using System;

namespace OsrsMr.Core.Spatial
{
    /// <summary>
    /// Computes 3D world/local coordinate projections into 2D client viewport canvas pixels.
    /// Replicates RuneLite and OSRS Perspective projection math.
    /// </summary>
    public static class Viewport
    {
        private static readonly int[] SINE = new int[2048];
        private static readonly int[] COSINE = new int[2048];

        static Viewport()
        {
            for (int i = 0; i < 2048; i++)
            {
                SINE[i] = (int)(65536.0 * Math.Sin(i * 0.0030679615));
                COSINE[i] = (int)(65536.0 * Math.Cos(i * 0.0030679615));
            }
        }

        /// <summary>
        /// Converts a world coordinate into screen coordinates using the player position as the local scene origin.
        /// </summary>
        public static ScreenPoint? WorldToCanvas(
            int worldX,
            int worldY,
            int planeHeight,
            int playerWorldX,
            int playerWorldY,
            CameraSnapshot? camera = null)
        {
            // Default fixed fallback camera if telemetry is not yet populated
            var cam = camera ?? new CameraSnapshot
            {
                ViewportWidth = 765,
                ViewportHeight = 503,
                Pitch = 380,
                Yaw = 0,
                Zoom = 512,
                X = 0,
                Y = 0,
                Z = -500
            };

            int localX = (worldX - playerWorldX) * LocalPoint.LocalTileSize;
            int localY = (worldY - playerWorldY) * LocalPoint.LocalTileSize;

            return LocalToCanvas(localX, localY, planeHeight, cam);
        }

        /// <summary>
        /// Projects local 3D coordinates (x, y, height) onto the 2D viewport canvas.
        /// </summary>
        public static ScreenPoint? LocalToCanvas(
            int localX,
            int localY,
            int planeHeight,
            CameraSnapshot camera)
        {
            if (camera == null || camera.ViewportWidth <= 0 || camera.ViewportHeight <= 0)
            {
                return null;
            }

            int camX = camera.X;
            int camY = camera.Y;
            int camZ = camera.Z;
            int camPitch = camera.Pitch & 2047;
            int camYaw = camera.Yaw & 2047;

            int dx = localX - camX;
            int dy = localY - camY;
            int dz = planeHeight - camZ;

            int sinYaw = SINE[camYaw];
            int cosYaw = COSINE[camYaw];
            int sinPitch = SINE[camPitch];
            int cosPitch = COSINE[camPitch];

            int rotX = (dx * cosYaw + dy * sinYaw) >> 16;
            int rotY = (dy * cosYaw - dx * sinYaw) >> 16;
            int rotZ = (dz * cosPitch - rotY * sinPitch) >> 16;
            int depth = (dz * sinPitch + rotY * cosPitch) >> 16;

            if (depth < 50) return null; // Behind camera plane

            int scale = camera.Scale > 0 ? camera.Scale : (camera.Zoom > 0 ? camera.Zoom : 512);
            int screenX = (camera.ViewportWidth / 2) + (rotX * scale / depth) + camera.ViewportOffsetX;
            int screenY = (camera.ViewportHeight / 2) + (rotZ * scale / depth) + camera.ViewportOffsetY;

            return new ScreenPoint(screenX, screenY);
        }

        /// <summary>
        /// Returns the 4-corner screen polygon of a tile at (localX, localY).
        /// </summary>
        public static Polygon2D? GetCanvasTilePoly(
            int localX,
            int localY,
            int planeHeight,
            CameraSnapshot camera)
        {
            int halfTile = LocalPoint.LocalTileSize / 2;
            var p1 = LocalToCanvas(localX - halfTile, localY - halfTile, planeHeight, camera);
            var p2 = LocalToCanvas(localX + halfTile, localY - halfTile, planeHeight, camera);
            var p3 = LocalToCanvas(localX + halfTile, localY + halfTile, planeHeight, camera);
            var p4 = LocalToCanvas(localX - halfTile, localY + halfTile, planeHeight, camera);

            if (!p1.HasValue || !p2.HasValue || !p3.HasValue || !p4.HasValue)
            {
                return null;
            }

            var poly = new Polygon2D();
            poly.Points.Add(p1.Value);
            poly.Points.Add(p2.Value);
            poly.Points.Add(p3.Value);
            poly.Points.Add(p4.Value);
            return poly;
        }
    }
}
