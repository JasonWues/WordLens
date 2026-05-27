using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WordLens.Services;

namespace WordLens.Services.LocalApi;

public sealed class LocalApiBridge
{
    private const int MaxTextLength = 50_000;

    private readonly TranslationService _translationService;
    private readonly IWindowManagerService _windowManager;

    public LocalApiBridge(
        TranslationService translationService,
        IWindowManagerService windowManager)
    {
        _translationService = translationService;
        _windowManager = windowManager;
    }

    public async Task<TranslateApiResponse> TranslateAsync(
        TranslateApiRequest request,
        CancellationToken cancellationToken)
    {
        ValidateText(request.Text);

        var targetLanguage = string.IsNullOrWhiteSpace(request.TargetLanguage)
            ? "en"
            : request.TargetLanguage.Trim();
        var sourceLanguage = string.IsNullOrWhiteSpace(request.SourceLanguage)
            ? "auto"
            : request.SourceLanguage.Trim();
        var providerNames = request.ProviderNames?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var results = providerNames is { Length: > 0 }
            ? await TranslateWithProvidersAsync(providerNames, request.Text, targetLanguage, sourceLanguage, cancellationToken)
            : await _translationService.TranslateNonStreamingAsync(
                request.Text,
                targetLanguage,
                sourceLanguage,
                cancellationToken);

        return new TranslateApiResponse(results
            .Select(result => new TranslateApiResult(
                result.ProviderName,
                result.IsSuccess,
                result.Result,
                result.ErrorMessage))
            .ToList());
    }

    public async Task OpenTranslationWindowAsync(
        OpenTranslationWindowApiRequest request,
        CancellationToken cancellationToken)
    {
        ValidateText(request.Text);
        cancellationToken.ThrowIfCancellationRequested();
        await _windowManager.ShowTranslationWindowAsync(request.Text);
    }

    private async Task<List<Models.TranslationResult>> TranslateWithProvidersAsync(
        IEnumerable<string> providerNames,
        string text,
        string targetLanguage,
        string sourceLanguage,
        CancellationToken cancellationToken)
    {
        var tasks = providerNames.Select(providerName => _translationService.TranslateWithProviderNonStreamingAsync(
            providerName,
            text,
            targetLanguage,
            sourceLanguage,
            cancellationToken));

        return (await Task.WhenAll(tasks)).ToList();
    }

    private static void ValidateText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty.", nameof(text));

        if (text.Length > MaxTextLength)
            throw new ArgumentOutOfRangeException(nameof(text), $"Text cannot exceed {MaxTextLength} characters.");
    }
}
