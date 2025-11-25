# ADJUST SFML PATHS AS NEEDED
# IF SO, THIS MAKE FILE CAN BE USED TO COMPILE IN OTHER ENVIRONMENTS


# Compiler
CXX = g++

#ADJUST SFML INCLUDE PATH
CXXFLAGS = -std=c++17 -Wall -I/opt/homebrew/Cellar/sfml@2.6/2.6.0/include -I./include

# Linker flags
#ADJUST SFML LIBRARY PATH
LDFLAGS = -L/opt/homebrew/Cellar/sfml@2.6/2.6.0/lib \
          -lsfml-graphics -lsfml-window -lsfml-audio -lsfml-system

# Target executable name
TARGET = warehouse

# Source files
SRC = src/main.cpp src/Grid.cpp src/Robot.cpp src/Box.cpp src/SharedMemory.cpp src/Agent.cpp src/utils.cpp

# Object files
OBJ = $(SRC:.cpp=.o)

# Default target
all: $(TARGET)

# Linking
$(TARGET): $(OBJ)
	$(CXX) $(OBJ) -o $(TARGET) $(LDFLAGS)

# Compilation rule
%.o: %.cpp
	$(CXX) $(CXXFLAGS) -c $< -o $@

# Run the program
run: $(TARGET)
	./$(TARGET)

# Clean up
clean:
	rm -f $(OBJ) $(TARGET)