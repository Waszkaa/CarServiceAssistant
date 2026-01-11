using CarServiceAssistant.Domain;

namespace CarServiceAssistant.Services;

public record ServiceRecommendation(
    ServiceArea Area,
    ServiceStatus Status,
    string Title,
    string Description,
    string WhatToDoNow,
    string WhatToCheckSoon
);
