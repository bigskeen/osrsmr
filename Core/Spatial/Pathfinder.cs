using System;
using System.Collections.Generic;

namespace OsrsMr.Core.Spatial
{
    /// <summary>
    /// Lightweight A* grid pathfinder for local scene navigation.
    /// </summary>
    public static class Pathfinder
    {
        private readonly struct Node : IComparable<Node>
        {
            public readonly int X;
            public readonly int Y;
            public readonly int G;
            public readonly int H;
            public int F => G + H;

            public Node(int x, int y, int g, int h)
            {
                X = x;
                Y = y;
                G = g;
                H = h;
            }

            public int CompareTo(Node other) => F.CompareTo(other.F);
        }

        private static readonly (int dx, int dy)[] Directions = new[]
        {
            (0, 1), (1, 0), (0, -1), (-1, 0), // Cardinal
            (1, 1), (1, -1), (-1, 1), (-1, -1) // Diagonal
        };

        /// <summary>
        /// Finds an optimal path of world coordinates from start to target.
        /// </summary>
        public static List<WorldPoint> FindPath(int startX, int startY, int destX, int destY, int plane = 0, Func<int, int, bool>? isBlocked = null, int maxSearchNodes = 2000)
        {
            var path = new List<WorldPoint>();
            if (startX == destX && startY == destY)
            {
                path.Add(new WorldPoint(startX, startY, plane));
                return path;
            }

            var openSet = new PriorityQueue<Node, int>();
            var cameFrom = new Dictionary<(int, int), (int, int)>();
            var gScore = new Dictionary<(int, int), int> { [(startX, startY)] = 0 };

            openSet.Enqueue(new Node(startX, startY, 0, Heuristic(startX, startY, destX, destY)), Heuristic(startX, startY, destX, destY));

            int count = 0;
            bool reached = false;

            while (openSet.Count > 0 && count++ < maxSearchNodes)
            {
                var current = openSet.Dequeue();

                if (current.X == destX && current.Y == destY)
                {
                    reached = true;
                    break;
                }

                foreach (var (dx, dy) in Directions)
                {
                    int neighborX = current.X + dx;
                    int neighborY = current.Y + dy;

                    if (isBlocked != null && isBlocked(neighborX, neighborY))
                    {
                        // Cannot pass through blocked tile unless it is the destination
                        if (neighborX != destX || neighborY != destY)
                            continue;
                    }

                    int tentativeG = current.G + (dx != 0 && dy != 0 ? 14 : 10); // diagonal cost ~1.4x

                    var neighborKey = (neighborX, neighborY);
                    if (!gScore.TryGetValue(neighborKey, out int currentG) || tentativeG < currentG)
                    {
                        cameFrom[neighborKey] = (current.X, current.Y);
                        gScore[neighborKey] = tentativeG;
                        int h = Heuristic(neighborX, neighborY, destX, destY);
                        openSet.Enqueue(new Node(neighborX, neighborY, tentativeG, h), tentativeG + h);
                    }
                }
            }

            if (!reached && !cameFrom.ContainsKey((destX, destY)))
            {
                // Find nearest reached node if dest was unreachable
                (int X, int Y) nearest = (startX, startY);
                int nearestDist = int.MaxValue;
                foreach (var kvp in gScore.Keys)
                {
                    int d = Heuristic(kvp.Item1, kvp.Item2, destX, destY);
                    if (d < nearestDist)
                    {
                        nearestDist = d;
                        nearest = kvp;
                    }
                }
                destX = nearest.X;
                destY = nearest.Y;
            }

            // Reconstruct path
            var curr = (destX, destY);
            while (cameFrom.TryGetValue(curr, out var prev))
            {
                path.Add(new WorldPoint(curr.Item1, curr.Item2, plane));
                curr = prev;
            }
            path.Add(new WorldPoint(startX, startY, plane));
            path.Reverse();

            return path;
        }

        private static int Heuristic(int x1, int y1, int x2, int y2)
        {
            int dx = Math.Abs(x1 - x2);
            int dy = Math.Abs(y1 - y2);
            return (dx + dy) * 10;
        }
    }
}
