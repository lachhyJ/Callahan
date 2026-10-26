namespace Callahan.Api.DTOs;

public record QuoteDto(int Id, string Text, DateTime CreatedAt);

// Several lines can arrive at once (pasting a whole list), each one its own quote.
public record AddQuotesRequest(List<string>? Texts);

// `Quotes` are the rows now live because of this request, whether newly created or
// brought back from the recovery window. `Skipped` names what was left out and why
// ("blank", "too long", "duplicate") so the page can say so instead of dropping lines
// silently.
public record AddQuotesResultDto(List<QuoteDto> Quotes, List<SkippedQuoteDto> Skipped);

public record SkippedQuoteDto(string Text, string Reason);
