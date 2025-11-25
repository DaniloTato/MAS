# Warehouse Robots Simulation

This project simulates robots coordinating in a warehouse environment, moving boxes and stacking them using pathfinding and simple multi-agent coordination.

---

## Requirements

- C++17 compatible compiler (`g++` or `clang++`)
- [SFML 2.6](https://www.sfml-dev.org/download.php) installed on your system
- Make sure SFML libraries and headers are accessible (e.g., installed via Homebrew)

---

# The make file included inside this folder will be useful *only if* the INCLUDE and LIBRARY paths are modified to match those in your system.

## How to Compile Manually on macOS/Linux

### 1. Install SFML

If you haven't installed SFML, you can do it via Homebrew:

```bash
brew install sfml
```

### 2. Compile source files

Assuming your source files are in src/ and headers in include/ . Adjust SFML *include* path accordingly to the installation in your system:

```bash
g++ -std=c++17 -Wall -I/opt/homebrew/Cellar/sfml@2.6/2.6.0/include -I./include -c src/main.cpp -o main.o
g++ -std=c++17 -Wall -I/opt/homebrew/Cellar/sfml@2.6/2.6.0/include -I./include -c src/Grid.cpp -o Grid.o
g++ -std=c++17 -Wall -I/opt/homebrew/Cellar/sfml@2.6/2.6.0/include -I./include -c src/Robot.cpp -o Robot.o
g++ -std=c++17 -Wall -I/opt/homebrew/Cellar/sfml@2.6/2.6.0/include -I./include -c src/Box.cpp -o Box.o
g++ -std=c++17 -Wall -I/opt/homebrew/Cellar/sfml@2.6/2.6.0/include -I./include -c src/SharedMemory.cpp -o SharedMemory.o
g++ -std=c++17 -Wall -I/opt/homebrew/Cellar/sfml@2.6/2.6.0/include -I./include -c src/Agent.cpp -o Agent.o
g++ -std=c++17 -Wall -I/opt/homebrew/Cellar/sfml@2.6/2.6.0/include -I./include -c src/utils.cpp -o utils.o
```

### 3. Link object files and create executable

Again, adjust the SFML *include* path accordingly to the installation in your system:

```bash
g++ main.o Grid.o Robot.o Box.o SharedMemory.o Agent.o utils.o -o warehouse \
  -L/opt/homebrew/Cellar/sfml@2.6/2.6.0/lib \
  -lsfml-graphics -lsfml-window -lsfml-audio -lsfml-system
```

## 4. Run the program

```bash
./warehouse
```

- This manual compilation can be replaced by using a Makefile for convenience.