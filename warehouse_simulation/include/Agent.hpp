#ifndef AGENT_HPP
#define AGENT_HPP

#include <string>

#include "ACLMessage.hpp"

class Agent {
public:
    Agent(const std::string& name);
    virtual void receive(const acl::ACLMessage& msg) = 0;
    virtual void handleResponse(const acl::ACLMessage& msg) = 0;

    std::string generateUniqueConversationId();
    void sendRequest(const std::string& receiver, const std::string& content, const std::string& convId);
    bool hasResponseArrived(const std::string& conversationId);

    std::string getName() const { return name; }
    void send(const std::string& receiverName, const acl::ACLMessage& msg);

protected:
    std::string name;
    std::unordered_map<std::string, bool> pendingRequests;
    std::unordered_map<std::string, int> waitingForResponses;
};

#endif