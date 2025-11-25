#ifndef GRID_HPP
#define GRID_HPP

#include <SFML/Graphics.hpp>
#include <vector>
#include "Box.hpp"

class Robot;

enum CellType {
    EMPTY,
    WALL,
};

struct Cell {
    CellType type = EMPTY;
    Box* box = nullptr;   // Pointer to a box stack
};

class Grid {
public:
    int rows, cols;
    std::vector<std::vector<Cell>> cells;

    Grid(int rows, int cols);

    // Box management
    void placeBox(int x, int y, int stackSize = 1);
    void removeBox(int x, int y);

    // Check if a box exists
    bool hasBox(int x, int y) const;

    // Drawing
    void draw(sf::RenderWindow& window, int cellSize);

    void addWallRange(int startX, int startY, int endX, int endY);

    void addRobot(Robot* robot);
    const std::vector<Robot*>& getRobots() const;
private:
    std::vector<Robot*> robots;
};

#endif