using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using TelegramBot.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<TelegramBotOptions>()
    .Bind(builder.Configuration.GetSection(TelegramBotOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services
    .AddOptions<SupabaseOptions>()
    .Bind(builder.Configuration.GetSection(SupabaseOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services
    .AddOptions<RssOptions>()
    .Bind(builder.Configuration.GetSection(RssOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddHttpClient<RssFetcher>();
builder.Services.AddSingleton<IncidentCandidateStore>();
builder.Services.AddHostedService<RssPollingService>();

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapGet("/config", (IOptions<TelegramBotOptions> telegram, IOptions<SupabaseOptions> supabase, IOptions<RssOptions> rss) =>
    Results.Ok(new
    {
        telegram = new { telegram.Value.Enabled, telegram.Value.ChatId },
        supabase = new { supabase.Value.ConnectionString is not null },
        rss = new
        {
            rss.Value.FeedUrl,
            rss.Value.PollIntervalSeconds,
            keywordCount = rss.Value.Keywords.Count
        }
    }));

app.Run();

public sealed class TelegramBotOptions
{
    public const string SectionName = "Telegram";

    public bool Enabled { get; init; } = true;

    public string? BotToken { get; init; }

    public string? ChatId { get; init; }
}

public sealed class SupabaseOptions
{
    public const string SectionName = "Supabase";

    public string? ConnectionString { get; init; }
}

public sealed class RssOptions
{
    public const string SectionName = "Rss";

    public string? FeedUrl { get; init; }

    [Range(30, 86_400)]
    public int PollIntervalSeconds { get; init; } = 300;

    public IReadOnlyList<string> Keywords { get; init; } =
        new List<string> { "incendiu", "incendii", "fire", "пожар", "chișinău", "chisinau" };
}
