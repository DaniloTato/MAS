#include "SharedMemory.hpp"
#include "Robot.hpp"

SharedMemory::SharedMemory()
    : pivot(nullptr) 
{}

SharedMemory& SharedMemory::get() {
    static SharedMemory instance;
    return instance;
}

bool SharedMemory::pivotExists() const {
    std::lock_guard<std::mutex> lock(mtx);
    return pivot != nullptr;
}

void SharedMemory::setPivot(Box* value) {
    std::lock_guard<std::mutex> lock(mtx);
    pivot = value;
}

Box* SharedMemory::getPivot() const {
    std::lock_guard<std::mutex> lock(mtx);
    return pivot;
}

void SharedMemory::clearPivot(){
    pivot = nullptr;
}

int SharedMemory::countBoxesGoingToPivot(const std::vector<Robot*>& robots) {
    Box* pivot = getPivot();
    if (!pivot) return 0;

    int count = 0;

    for (auto r : robots) {
        if (r->state == MOVING_TO_PIVOT) {
            count++;
        }
    }

    return count;
}

void SharedMemory::startTimer() {
    std::lock_guard<std::mutex> lock(mtx);
    startTime = std::chrono::steady_clock::now();
    totalMovements = 0;
}

long long SharedMemory::getElapsedTimeMs() const {
    std::lock_guard<std::mutex> lock(mtx);
    auto now = std::chrono::steady_clock::now();
    return std::chrono::duration_cast<std::chrono::milliseconds>(now - startTime).count();
}

void SharedMemory::resetMovementCount() {
    std::lock_guard<std::mutex> lock(mtx);
    totalMovements = 0;
}

void SharedMemory::addMovements(int count) {
    std::lock_guard<std::mutex> lock(mtx);
    totalMovements += count;
}

int SharedMemory::getMovementCount() const {
    std::lock_guard<std::mutex> lock(mtx);
    return totalMovements;
}