#pragma once
#include <mutex>
#include <chrono>
#include <vector>

#include "Box.hpp"
#include "Robot.hpp"

class SharedMemory {
public:
    static SharedMemory& get();

    bool pivotExists() const;
    void setPivot(Box* value);
    Box* getPivot() const;
    void clearPivot();
    int countBoxesGoingToPivot(const std::vector<Robot*>& robots);

    void startTimer();
    long long getElapsedTimeMs() const;

    void resetMovementCount();
    void addMovements(int count);
    int getMovementCount() const;

private:
    SharedMemory();

    SharedMemory(const SharedMemory&) = delete;
    SharedMemory& operator=(const SharedMemory&) = delete;

    Box* pivot;

    mutable std::mutex mtx;

    std::chrono::steady_clock::time_point startTime;
    int totalMovements;
};