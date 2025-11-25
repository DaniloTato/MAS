#include <SFML/Graphics.hpp>
#include "Grid.hpp"
#include "Robot.hpp"
#include "SharedMemory.hpp"

#include <iostream>
#include <random>
#include <set>

bool onReached(){
    std::cout << "Reached target!" << std::endl;
    return true;
}

// Helper to get a random empty cell on the grid
std::pair<int,int> getRandomEmptyCell(const Grid& grid, const std::set<std::pair<int,int>>& occupied, std::mt19937& rng) {
    std::uniform_int_distribution<int> distRow(0, grid.rows - 1);
    std::uniform_int_distribution<int> distCol(0, grid.cols - 1);

    while (true) {
        int r = distRow(rng);
        int c = distCol(rng);

        // Check cell not wall, no box, and not occupied
        if (grid.cells[r][c].type != WALL && grid.cells[r][c].box == nullptr && occupied.count({c,r}) == 0)
            return {c, r};
    }
}

int main() {

    const int rows = 30, cols = 30, cellSize = 20;

    sf::RenderWindow window(
        sf::VideoMode(cols * cellSize, rows * cellSize),
        "Warehouse Robots"
    );

    Grid grid(rows, cols);

    // Add walls first
    grid.addWallRange(10, 10, 15, 15);

    std::random_device rd;
    std::mt19937 rng(rd());

    std::set<std::pair<int,int>> occupiedCells;

    // Spawn 17 boxes at random empty cells
    for (int i = 0; i < 17; i++) {
        auto [bx, by] = getRandomEmptyCell(grid, occupiedCells, rng);
        grid.placeBox(bx, by);
        occupiedCells.insert({bx, by});
    }

    // Spawn 3 robots at random empty cells (no overlap with boxes or walls or other robots)
    Robot robots[] = {
        Robot("Robot1", 0, 0),
        Robot("Robot2", 0, 0),
        Robot("Robot3", 0, 0),
        Robot("Robot4", 0, 0),
        Robot("Robot5", 0, 0),
    };

    for (auto& r : robots) {
        auto [rx, ry] = getRandomEmptyCell(grid, occupiedCells, rng);
        r.x = rx;
        r.y = ry;
        occupiedCells.insert({rx, ry});
        grid.addRobot(&r);
    }

    window.setFramerateLimit(20);

    SharedMemory::get().startTimer();

    while (window.isOpen()) {
        sf::Event e;
        while (window.pollEvent(e)) {
            if (e.type == sf::Event::Closed)
                window.close();
        }

        // Update all robots
        for (auto& r : robots)
            r.update(grid);

        bool allExploringNoTargets = true;
        for (const auto& r : robots) {
            if (r.state != EXPLORING || r.targetBox != nullptr) {
                allExploringNoTargets = false;
                break;
            }
        }

        if (allExploringNoTargets) {
            auto elapsedMs = SharedMemory::get().getElapsedTimeMs();
            std::cout << "All robots are exploring with no target boxes.\n";
            std::cout << "Simulation ended after " << elapsedMs << " milliseconds.\n";
            std::cout << "Total number of movements " << SharedMemory::get().getMovementCount() << ".\n";
            window.close();
            break;
        }


        // Render
        window.clear();
        grid.draw(window, cellSize);

        window.display();
    }

    return 0;
}