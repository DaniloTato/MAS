#include "Agent.hpp"
#include "AgentRegistry.hpp"

#include <random>
#include <sstream>
#include <chrono>

Agent::Agent(const std::string& name) : name(name) {
    AgentRegistry::registerAgent(name, this);
}

void Agent::send(const std::string& receiverName, const acl::ACLMessage& msg) {
    Agent* receiver = AgentRegistry::getAgent(receiverName);
    if (!receiver) {
        std::cerr << "ERROR: Agent '" << receiverName << "' not found!\n";
        return;
    }
    receiver->receive(msg);
}

std::string Agent::generateUniqueConversationId() {
    auto now = std::chrono::system_clock::now().time_since_epoch();
    auto millis = std::chrono::duration_cast<std::chrono::milliseconds>(now).count();

    static std::mt19937 rng(std::random_device{}());
    static std::uniform_int_distribution<int> dist(0, 9999);

    std::stringstream ss;
    ss << "conv-" << millis << "-" << dist(rng);
    return ss.str();
}

void Agent::sendRequest(const std::string& receiver, const std::string& content, const std::string& convId) {
    acl::ACLMessage req(
        acl::Performative::REQUEST,
        name,
        receiver,
        content,
        "SL",
        "warehouse-ontology",
        "fipa-contract-net",
        convId
    );

    send(receiver, req);
}

bool Agent::hasResponseArrived(const std::string& conversationId) {
    auto it = pendingRequests.find(conversationId);
    return it != pendingRequests.end() && it->second;
}