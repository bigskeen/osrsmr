using System;
using System.Collections.Generic;

namespace OsrsMr.Core.Spatial
{
    public readonly record struct ScreenPoint(int X, int Y);

    public readonly record struct WorldPoint(int X, int Y, int Plane)
    {
        public double DistanceTo(WorldPoint other)
        {
            int dx = X - other.X;
            int dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public int ChebyshevDistanceTo(WorldPoint other)
        {
            return Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));
        }
    }

    public readonly record struct LocalPoint(int X, int Y)
    {
        public const int SceneSize = 104;
        public const int LocalCoordBits = 7;
        public const int LocalTileSize = 1 << LocalCoordBits; // 128

        public static LocalPoint FromWorld(int worldX, int worldY, int baseX, int baseY)
        {
            return new LocalPoint((worldX - baseX) << LocalCoordBits, (worldY - baseY) << LocalCoordBits);
        }
    }

    public class Polygon2D
    {
        public List<ScreenPoint> Points { get; } = new();

        public Polygon2D() { }

        public Polygon2D(IEnumerable<ScreenPoint> points)
        {
            Points.AddRange(points);
        }

        public bool Contains(int x, int y)
        {
            if (Points.Count < 3) return false;

            bool inside = false;
            for (int i = 0, j = Points.Count - 1; i < Points.Count; j = i++)
            {
                if (((Points[i].Y > y) != (Points[j].Y > y)) &&
                    (x < (Points[j].X - Points[i].X) * (y - Points[i].Y) / (Points[j].Y - Points[i].Y + 0.0001) + Points[i].X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        public ScreenPoint GetCenter()
        {
            if (Points.Count == 0) return new ScreenPoint(0, 0);
            int sumX = 0, sumY = 0;
            foreach (var p in Points)
            {
                sumX += p.X;
                sumY += p.Y;
            }
            return new ScreenPoint(sumX / Points.Count, sumY / Points.Count);
        }
    }
}
