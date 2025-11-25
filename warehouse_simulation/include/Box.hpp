#ifndef BOX_HPP
#define BOX_HPP

#include <SFML/Graphics.hpp>

class Box {
public:
    int x, y;              
    int stackSize;       
    bool isPivot;

    Box(int x, int y, int stackSize = 1);

    // Merge stacks: add sizes, cap at 5
    void merge(Box& other);

    // Render on SFML window
    void draw(sf::RenderWindow& window, int cellSize) const;
};

#endif