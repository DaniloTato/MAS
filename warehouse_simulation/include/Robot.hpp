#pragma once
#include <SFML/Graphics.hpp>
#include "Grid.hpp"
#include "Agent.hpp"
#include <functional>

enum RobotState {
    EXPLORING,
    MOVING_TO_BOX,
    MOVING_TO_PIVOT,
    STACKING
};

enum class ACLPerformative {
    QUERY_REF,
    INFORM
};

struct ACLMessage {
    ACLPerformative performative;
    Box* box;
    bool free;
    Robot* sender;
};

class Robot: public Agent {
public:
    int x, y;
    bool carrying;
    Box* carriedBox;

    RobotState state;
    Robot(const std::string& name, int startX, int startY);

    Box* targetBox = nullptr;

    void update(Grid& grid);
    bool go_to(const Grid& grid, const sf::Vector2f& target, std::function<bool(Robot*)> onReached);
    bool tryPickup(Grid& grid);

    virtual void receive(const acl::ACLMessage& msg) override;
    virtual void handleResponse(const acl::ACLMessage& msg) override;

private:
    bool inBounds(const Grid& grid, int nx, int ny);
    bool tryStack(Grid& grid);
    bool isBoxTargetedByOthers(Box* box, const std::vector<Robot*>& allRobots);
    Box* findNearestNonPivotBox(Grid& grid, const std::vector<Robot*>& allRobots);
    bool checkBoxAvailability(Box* box, const std::vector<Robot*>& robots);
    std::vector<std::pair<int,int>> currentPath;
    std::pair<int,int> currentTarget = {-1, -1};
};