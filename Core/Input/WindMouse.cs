using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;

namespace OsrsMr.Core.Input
{
    /// <summary>
    /// Implements the physics-based WindMouse human curve simulation algorithm.
    /// Generates organic mouse trajectories with velocity, inertia, target gravity, and randomized wind fluctuations.
    /// </summary>
    public static class WindMouse
    {
        private static readonly Random Rnd = new();
        private static double Distance(double dx, double dy) => Math.Sqrt(dx * dx + dy * dy);

        public static List<Point> GenerateTrajectory(
            Point start,
            Point destination,
            double gravity = 9.0,
            double wind = 3.0,
            double minWait = 2.0,
            double maxWait = 8.0,
            double maxStep = 15.0,
            double targetArea = 8.0)
        {
            var points = new List<Point>();

            double currentX = start.X;
            double currentY = start.Y;
            double destX = destination.X;
            double destY = destination.Y;

            double windX = 0;
            double windY = 0;
            double velocityX = 0;
            double velocityY = 0;

            double sqrt3 = Math.Sqrt(3.0);
            double sqrt5 = Math.Sqrt(5.0);

            double dist = Distance(destX - currentX, destY - currentY);

            while (dist > 1.0)
            {
                double currentWind = Math.Min(wind, dist);

                if (dist >= targetArea)
                {
                    windX = windX / sqrt3 + (Rnd.NextDouble() * (currentWind * 2.0 + 1.0) - currentWind) / sqrt5;
                    windY = windY / sqrt3 + (Rnd.NextDouble() * (currentWind * 2.0 + 1.0) - currentWind) / sqrt5;
                }
                else
                {
                    windX /= sqrt3;
                    windY /= sqrt3;
                    if (maxStep < 3.0)
                    {
                        maxStep = Rnd.NextDouble() * 3.0 + 3.0;
                    }
                    else
                    {
                        maxStep /= sqrt5;
                    }
                }

                velocityX += windX + gravity * (destX - currentX) / dist;
                velocityY += windY + gravity * (destY - currentY) / dist;

                double velMag = Distance(velocityX, velocityY);
                if (velMag > maxStep)
                {
                    double randomDist = maxStep / 2.0 + Rnd.NextDouble() * (maxStep / 2.0);
                    velocityX = (velocityX / velMag) * randomDist;
                    velocityY = (velocityY / velMag) * randomDist;
                }

                currentX += velocityX;
                currentY += velocityY;

                points.Add(new Point((int)Math.Round(currentX), (int)Math.Round(currentY)));
                dist = Distance(destX - currentX, destY - currentY);
            }

            points.Add(destination);
            return points;
        }
    }
}
