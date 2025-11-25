#include "Box.hpp"
#include <string>

Box::Box(int x, int y, int stackSize)
: x(x), y(y), stackSize(stackSize), isPivot(false)
{
    if (this->stackSize < 1) this->stackSize = 1;
    if (this->stackSize > 5) this->stackSize = 5;
}

void Box::merge(Box& other) {
    stackSize += other.stackSize;
    if (stackSize > 5) stackSize = 5;
}

void Box::draw(sf::RenderWindow& window, int cellSize) const {
    sf::RectangleShape rect(sf::Vector2f(cellSize - 4, cellSize - 4));
    rect.setPosition(x * cellSize + 2, y * cellSize + 2);

    if (isPivot)
        rect.setFillColor(sf::Color(255, 180, 0)); // gold color for pivot
    else
        rect.setFillColor(sf::Color(160, 90, 40)); // wood-like color

    rect.setOutlineThickness(2);
    rect.setOutlineColor(sf::Color::Black);
    window.draw(rect);

    sf::Text text;
    static sf::Font font;

    static bool loaded = false;
    if (!loaded) {
        font.loadFromFile("/System/Library/Fonts/Supplemental/Arial.ttf");
        loaded = true;
    }

    text.setFont(font);
    text.setString(std::to_string(stackSize));
    text.setCharacterSize(cellSize * 0.6);
    text.setFillColor(sf::Color::White);

    text.setPosition(x * cellSize + cellSize * 0.35,
                     y * cellSize + cellSize * 0.1);

    window.draw(text);
}