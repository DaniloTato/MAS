#ifndef UTILS_HPP
#define UTILS_HPP
#include "Grid.hpp"


using Pos = std::pair<int,int>;

std::vector<Pos> computeDijkstraPath(const Grid& grid, int startX, int startY, int targetX, int targetY);

#endif