using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Telegram.Bot.Types;

namespace TelegramBot.Services;

public sealed class TelegramUpdateParser
{
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ILogger<TelegramUpdateParser> _logger;

    public TelegramUpdateParser(ILogger<TelegramUpdateParser> logger)
    {
        _logger = logger;
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = new SnakeCaseNamingPolicy()
        };
    }

    public async Task<Update?> ParseAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.Body is null)
        {
            return null;
        }

        try
        {
            return await JsonSerializer.DeserializeAsync<Update>(request.Body, _serializerOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize Telegram update payload.");
            return null;
        }
    }
}
