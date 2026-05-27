using System.Collections.Generic;

namespace WordLens.Services.LocalApi;

public sealed record LocalApiHealthResponse(
    bool Ok,
    string App,
    string Version);

public sealed record LocalApiStatusResponse(
    bool Enabled,
    int Port,
    string BaseUrl);

public sealed record TranslateApiRequest(
    string Text,
    string TargetLanguage = "en",
    string SourceLanguage = "auto",
    List<string>? ProviderNames = null);

public sealed record TranslateApiResponse(
    List<TranslateApiResult> Results);

public sealed record TranslateApiResult(
    string Provider,
    bool IsSuccess,
    string? Text,
    string? ErrorMessage);

public sealed record OpenTranslationWindowApiRequest(string Text);

public sealed record ApiErrorResponse(string Error);
