#ifndef AGENTREGISTRY_HPP
#define AGENTREGISTRY_HPP

#include <unordered_map>
#include "Agent.hpp"

class AgentRegistry {
public:
    static void registerAgent(const std::string& name, Agent* agent) {
        agents()[name] = agent;
    }

    static Agent* getAgent(const std::string& name) {
        auto it = agents().find(name);
        return (it != agents().end()) ? it->second : nullptr;
    }

private:
    static std::unordered_map<std::string, Agent*>& agents() {
        static std::unordered_map<std::string, Agent*> instance;
        return instance;
    }
};

#endif