#include "Robot.hpp"
#include <SFML/Window/Keyboard.hpp>
#include "SharedMemory.hpp"
#include "utils.hpp"

#include <iostream>
#include <chrono>
#include <unordered_map>

Robot::Robot(const std::string& name, int startX, int startY)
    : Agent(name), x(startX), y(startY),
      carrying(false), carriedBox(nullptr),
      state(MOVING_TO_BOX) {}

bool Robot::inBounds(const Grid& grid, int nx, int ny) {
    return ny >= 0 && ny < grid.rows &&
           nx >= 0 && nx < grid.cols;
}

bool Robot::checkBoxAvailability(Box* box, const std::vector<Robot*>& robots) {
    if (!box) return false;

    std::string convId = generateUniqueConversationId();
    pendingRequests[convId] = false;

    int expectedResponses = 0;
    waitingForResponses[convId] = 0;

    std::string queryContent = "available? box(" + std::to_string(box->x) + "," + std::to_string(box->y) + ")";

    for (const auto& r : robots) {
        if (r == this) continue;
        sendRequest(r->name, queryContent, convId);
        expectedResponses++;
    }

    waitingForResponses[convId] = expectedResponses;

    auto start = std::chrono::steady_clock::now();
    while (waitingForResponses[convId] > 0) {
        auto now = std::chrono::steady_clock::now();
        auto diff = std::chrono::duration_cast<std::chrono::milliseconds>(now - start);
        if (diff.count() > 100) break;  // timeout
    }

    bool answered = pendingRequests[convId];
    pendingRequests.erase(convId);
    waitingForResponses.erase(convId);

    return answered;
}

bool Robot::isBoxTargetedByOthers(Box* box, const std::vector<Robot*>& allRobots) {
    for (auto r : allRobots) {
        if (r == this) continue;
        if (r->targetBox == box)
            return true;
    }
    return false;
}

Box* Robot::findNearestNonPivotBox(Grid& grid, const std::vector<Robot*>& allRobots) {
    struct Candidate {
        Box* box;
        int dist;
    };

    std::vector<Candidate> candidates;

    for (int row = 0; row < grid.rows; row++) {
        for (int col = 0; col < grid.cols; col++) {
            Box* box = grid.cells[row][col].box;
            if (!box) continue;
            if (box->stackSize >= 5) continue;
            if (box->isPivot) continue;

            int dist = abs(col - x) + abs(row - y);
            candidates.push_back({box, dist});
        }
    }

    std::sort(candidates.begin(), candidates.end(),
              [](const Candidate& a, const Candidate& b) {
                  return a.dist < b.dist;
              });

    for (const auto& c : candidates) {
        if (!isBoxTargetedByOthers(c.box, allRobots)) {
            bool available = checkBoxAvailability(c.box, allRobots);
            if (available) {
                std::cout << "Nearest available box confirmed free by ACL: " << c.box->x << "," << c.box->y << std::endl;
                return c.box;
            } else {
                std::cout << "Box " << c.box->x << "," << c.box->y << " is NOT available according to ACL responses." << std::endl;
            }
        } else {
            std::cout << "Box " << c.box->x << "," << c.box->y << " is already targeted by another robot." << std::endl;
        }
    }

    std::cout << "No available boxes found." << std::endl;
    return nullptr;
}

bool Robot::go_to(const Grid& grid, const sf::Vector2f& target, std::function<bool(Robot*)> onReached) {
    int tx = static_cast<int>(target.x);
    int ty = static_cast<int>(target.y);

    if (currentTarget != std::make_pair(tx, ty) || currentPath.empty()) {
        // Compute new path using Dijkstra and store in currentPath
        std::cout << "About to compute" << std::endl;
        currentPath = computeDijkstraPath(grid, x, y, tx, ty);
        std::cout << "Robot " << name << " computed new path to (" << currentPath.front().first << "," << currentPath.front().second << ")\n";
        currentTarget = {tx, ty};


        if (currentPath.empty()) {
            // No path found
            return false;
        }
    }

    // Check if robot is adjacent or on the target
    if ((x == tx + 1 && y == ty) ||
        (x == tx - 1 && y == ty) ||
        (x == tx && y == ty + 1) ||
        (x == tx && y == ty - 1) ||
        (x == tx && y == ty))
    {
        if (onReached)
            return onReached(this);
        return true;
    }

    int storedX = x;
    int storedY = y;

    // Move along the path
    auto nextStep = currentPath.front();
    currentPath.erase(currentPath.begin());

    x = nextStep.first;
    y = nextStep.second;

    if(x != storedX || y != storedY) {
        SharedMemory::get().addMovements(1);
    }

    return false;
}

bool Robot::tryPickup(Grid& grid) {
    std::cout << "Trying to pick up box..." << std::endl;
    if (carrying) return false;

    if (!SharedMemory::get().pivotExists()) {
        // No pivot yet - first box becomes pivot
        const int dirs[4][2] = {
            { 1, 0}, {-1, 0},
            { 0, 1}, { 0,-1}
        };

        for (auto& d : dirs) {
            int nx = x + d[0];
            int ny = y + d[1];

            if (!inBounds(grid, nx, ny)) continue;

            Box* box = grid.cells[ny][nx].box;
            if (!box) continue;

            // Cannot pick up stacked boxes
            if (box->stackSize > 1)
                return false;

            SharedMemory::get().addMovements(1);

            std::cout << "Box set as pivot" << std::endl;
            box->isPivot = true;
            targetBox = nullptr; 
            SharedMemory::get().setPivot(box);
            state = MOVING_TO_BOX;
            return true;
        }
        return false;
    }
    
    // There's already a pivot
    Box* pivot = SharedMemory::get().getPivot();
    if (!pivot) return false;

    int boxesHeadingToPivot = SharedMemory::get().countBoxesGoingToPivot(grid.getRobots());

    const int dirs[4][2] = {
        { 1, 0}, {-1, 0},
        { 0, 1}, { 0,-1}
    };

    for (auto& d : dirs) {
        int nx = x + d[0];
        int ny = y + d[1];

        if (!inBounds(grid, nx, ny)) continue;

        Box* box = grid.cells[ny][nx].box;
        if (!box) continue;

        if (box->stackSize > 1)
            return false;

        std::cout << "Boxes heding to pivot: " << boxesHeadingToPivot << std::endl;

        // If picking this box would exceed pivot limit, wait near box (do not pick)
        if (boxesHeadingToPivot + pivot->stackSize >= 5) {
            // Stay nearby, do not pick
            std::cout << "Pivot full or nearly full, waiting near box at (" << box->x << "," << box->y << ")" << std::endl;
            return false;
        }

        // Otherwise pick up and move to pivot
        carriedBox = box;
        carrying = true;
        grid.cells[ny][nx].box = nullptr;
        grid.cells[ny][nx].type = EMPTY;
        state = MOVING_TO_PIVOT;
        SharedMemory::get().addMovements(1);
        return true;
    }

    return false;
}

bool Robot::tryStack(Grid& grid) {
    if (!carrying) return false;

    const int dirs[4][2] = {
        { 1, 0}, {-1, 0},
        { 0, 1}, { 0,-1}
    };

    for (auto& d : dirs) {
        int nx = x + d[0];
        int ny = y + d[1];

        if (!inBounds(grid, nx, ny)) continue;

        Box* target = grid.cells[ny][nx].box;
        if (!target) continue;

        // Merge stacks
        target->merge(*carriedBox);
        SharedMemory::get().addMovements(1);

        // After merging, check if stack size reached limit
        if (target->stackSize >= 5) {
            std::cout << "Pivot box at " << target->x << "," << target->y << " reached max stack size. Unmarking pivot.\n";
            target->isPivot = false;

            // If this pivot is stored in SharedMemory, clear it
            if (SharedMemory::get().pivotExists() && SharedMemory::get().getPivot() == target) {
                SharedMemory::get().clearPivot();
            }
        }

        delete carriedBox;
        carriedBox = nullptr;
        carrying = false;

        state = MOVING_TO_BOX;
        targetBox = nullptr;

        return true;
    }

    return false;
}

void Robot::update(Grid& grid) {

    if (state == MOVING_TO_BOX) {
        if(targetBox){
            go_to(
                grid,
                sf::Vector2f(targetBox->x, targetBox->y),
                [&](Robot* r) {
                    return r->tryPickup(grid);
                }
            );
        } else{
            targetBox = findNearestNonPivotBox(grid, grid.getRobots());

            if (targetBox == nullptr){ 
                std::cout << "No non-pivot boxes left.\n";
                state = EXPLORING;
                return;
            }
        }
    } else if(state == MOVING_TO_PIVOT) {
        Box* pivotBox = SharedMemory::get().pivotExists() ? SharedMemory::get().getPivot() : nullptr;
        if (pivotBox) {
            go_to(
                grid,
                sf::Vector2f(pivotBox->x, pivotBox->y),
                [&](Robot* r) {
                    return r->tryStack(grid);
                }
            );
        } else {
            std::cout << "Pivot box no longer exists!\n";
            state = EXPLORING;
        }    
    }
}


///ACL STUFF

void Robot::receive(const acl::ACLMessage& msg) {
    std::cout << "Robot " << name << " received message from " << msg.sender
              << ": " << msg.content << std::endl;

    if (msg.performative == acl::Performative::REQUEST) {
        if (msg.content.find("available? box") != std::string::npos) {
            // Parse
            int bx, by;
            sscanf(msg.content.c_str(), "available? box(%d,%d)", &bx, &by);

            bool isAvailable = true;

            if (carrying && carriedBox && carriedBox->x == bx && carriedBox->y == by)
                isAvailable = false;

            if (targetBox && targetBox->x == bx && targetBox->y == by)
                isAvailable = false;

            std::string replyContent = isAvailable ? "YES" : "NO";

            acl::ACLMessage reply(
                acl::Performative::INFORM,
                name,
                msg.sender,
                replyContent,
                "SL",
                "warehouse-ontology",
                "fipa-contract-net",
                msg.conversationId
            );

            send(msg.sender, reply);
        }
        else {
            std::cout << "Unknown request content: " << msg.content << std::endl;
        }
    }
    else if (msg.performative == acl::Performative::INFORM) {
        handleResponse(msg);
    }
}

void Robot::handleResponse(const acl::ACLMessage& msg) {
    auto it = pendingRequests.find(msg.conversationId);
    if (it != pendingRequests.end()) {
        it->second = true;
        std::cout << "Response received for conversation: " << msg.conversationId << std::endl;

        if (waitingForResponses.find(msg.conversationId) != waitingForResponses.end()) {
            waitingForResponses[msg.conversationId]--;
        }
    }
}