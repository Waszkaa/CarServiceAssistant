namespace CarServiceAssistant.Ai;

public record AiIntervalSource(string Title, string Url);

public record AiIntervalResult(
    string PlainLanguageSummary,
    IReadOnlyList<string> KeyIntervals,
    IReadOnlyList<AiIntervalSource> Sources,
    string SafetyNote
);
