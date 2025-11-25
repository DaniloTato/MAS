#include "utils.hpp"

#include <vector>
#include <optional>
#include <queue>
#include <iostream>
#include <limits>

using Pos = std::pair<int, int>;

std::vector<Pos> computeDijkstraPath(const Grid& grid, int startX, int startY, int targetX, int targetY) {
    const int INF = std::numeric_limits<int>::max();

    // Check if start and target are within bounds
    if (startX < 0 || startX >= grid.cols || startY < 0 || startY >= grid.rows ||
        targetX < 0 || targetX >= grid.cols || targetY < 0 || targetY >= grid.rows) {
        std::cout << "Invalid start or target coordinates.\n";
        return {};
    }

    // Directions (4-way)
    const int dirs[4][2] = {
        {1, 0}, {-1, 0}, {0, 1}, {0, -1}
    };

    std::vector<std::vector<int>> dist(grid.rows, std::vector<int>(grid.cols, INF));
    std::vector<std::vector<std::optional<Pos>>> parent(grid.rows, std::vector<std::optional<Pos>>(grid.cols, std::nullopt));

    auto cmp = [&](const std::pair<int, Pos>& a, const std::pair<int, Pos>& b) {
        return a.first > b.first;  // min-heap by distance
    };
    std::priority_queue<std::pair<int, Pos>, std::vector<std::pair<int, Pos>>, decltype(cmp)> pq(cmp);

    dist[startY][startX] = 0;
    pq.push({0, {startX, startY}});

    while (!pq.empty()) {
        auto [curDist, curPos] = pq.top();
        pq.pop();

        int cx = curPos.first;
        int cy = curPos.second;

        if (curDist > dist[cy][cx])
            continue;

        if (cx == targetX && cy == targetY)
            break;

        for (auto& d : dirs) {
            int nx = cx + d[0];
            int ny = cy + d[1];

            // Check bounds
            if (ny < 0 || ny >= grid.rows || nx < 0 || nx >= grid.cols)
                continue;

            // Allow target cell even if it has a box, but not walls or other boxes in path
            bool isTargetCell = (nx == targetX && ny == targetY);

            if (grid.cells[ny][nx].type == WALL)
                continue;

            if (grid.cells[ny][nx].box != nullptr && !isTargetCell)
                continue;

            int ndist = curDist + 1;
            if (ndist < dist[ny][nx]) {
                dist[ny][nx] = ndist;
                parent[ny][nx] = {cx, cy};
                pq.push({ndist, {nx, ny}});
            }
        }
    }

    if (dist[targetY][targetX] == INF) {
        std::cout << "Target unreachable\n";
        return {};
    }

    std::vector<Pos> path;
    Pos cur = {targetX, targetY};
    while (cur != Pos{startX, startY}) {
        path.push_back(cur);
        auto p = parent[cur.second][cur.first];
        if (!p.has_value()) {
            std::cout << "No parent found for (" << cur.first << "," << cur.second << "), path incomplete.\n";
            return {};
        }
        cur = p.value();
    }
    std::reverse(path.begin(), path.end());

    std::cout << "Dijkstra finished. Distance to target: " << dist[targetY][targetX] << std::endl;
    return path;
}