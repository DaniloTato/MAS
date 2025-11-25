#pragma once
#include <string>
#include <iostream>

namespace acl {

enum class Performative {
    REQUEST,
    INFORM,
    QUERY_REF,
    // Agrega más según necesites
};

inline std::string performativeToString(Performative p) {
    switch(p) {
        case Performative::REQUEST: return "REQUEST";
        case Performative::INFORM: return "INFORM";
        case Performative::QUERY_REF: return "QUERY_REF";
        default: return "UNKNOWN";
    }
}

class ACLMessage {
public:
    Performative performative;
    std::string sender;
    std::string receiver;
    std::string content;
    std::string language;
    std::string ontology;
    std::string protocol;
    std::string conversationId;

    ACLMessage(
        Performative perf,
        const std::string& sndr,
        const std::string& rcvr,
        const std::string& cont,
        const std::string& lang,
        const std::string& onto,
        const std::string& proto,
        const std::string& convId
    ) : performative(perf),
        sender(sndr),
        receiver(rcvr),
        content(cont),
        language(lang),
        ontology(onto),
        protocol(proto),
        conversationId(convId)
    {}

    void print() const {
        std::cout << "ACLMessage:\n"
                  << "  Performative: " << performativeToString(performative) << "\n"
                  << "  From: " << sender << "\n"
                  << "  To: " << receiver << "\n"
                  << "  Content: " << content << "\n"
                  << "  Language: " << language << "\n"
                  << "  Ontology: " << ontology << "\n"
                  << "  Protocol: " << protocol << "\n"
                  << "  ConversationId: " << conversationId << "\n";
    }
};

} // namespace acl