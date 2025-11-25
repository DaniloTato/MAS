#include "Grid.hpp"
#include "Robot.hpp"

Grid::Grid(int rows, int cols)
: rows(rows), cols(cols), cells(rows, std::vector<Cell>(cols)) {}

void Grid::addRobot(Robot* robot) {
    robots.push_back(robot);
}

void Grid::placeBox(int x, int y, int stackSize) {
    cells[y][x].box = new Box(x, y, stackSize);
}

void Grid::removeBox(int x, int y) {
    if (cells[y][x].box) {
        delete cells[y][x].box;
        cells[y][x].box = nullptr;
    }
    cells[y][x].type = EMPTY;
}

bool Grid::hasBox(int x, int y) const {
    return cells[y][x].box != nullptr;
}

void Grid::draw(sf::RenderWindow& window, int cellSize) {
    sf::RectangleShape cellRect(sf::Vector2f(cellSize - 1, cellSize - 1));

    for (int row = 0; row < rows; row++) {
        for (int col = 0; col < cols; col++) {

            cellRect.setPosition(col * cellSize, row * cellSize);

            switch (cells[row][col].type) {
                case EMPTY:
                    cellRect.setFillColor(sf::Color(40, 40, 40));
                    break;

                case WALL:
                    cellRect.setFillColor(sf::Color(100, 100, 100));
                    break;
            }

            window.draw(cellRect);

            // Draw box if present
            if (cells[row][col].box)
                cells[row][col].box->draw(window, cellSize);
        }
    }

    // Draw robot
    for (Robot* r : robots) {
        int x = r->x;   // horizontal
        int y = r->y;   // vertical

        sf::RectangleShape rect(sf::Vector2f(cellSize - 2, cellSize - 2));
        rect.setPosition(x * cellSize, y * cellSize);  // correct

        rect.setFillColor(r->carrying ? sf::Color::Cyan : sf::Color::Yellow);
        rect.setOutlineThickness(1);
        rect.setOutlineColor(sf::Color::Black);
        window.draw(rect);

        if (r->carrying) {
            sf::CircleShape symbol(5);
            symbol.setFillColor(sf::Color::White);
            symbol.setPosition(
                x * cellSize + cellSize * 0.35,
                y * cellSize + cellSize * 0.35
            );
            window.draw(symbol);
        }
    }
}

const std::vector<Robot*>& Grid::getRobots() const {
    return robots;
}

void Grid::addWallRange(int startX, int startY, int endX, int endY) {
    if (startX < 0) startX = 0;
    if (startY < 0) startY = 0;
    if (endX >= cols) endX = cols - 1;
    if (endY >= rows) endY = rows - 1;

    for (int y = startY; y <= endY; y++) {
        for (int x = startX; x <= endX; x++) {
            cells[y][x].type = WALL;

            if (cells[y][x].box) {
                delete cells[y][x].box;
                cells[y][x].box = nullptr;
            }
        }
    }
}