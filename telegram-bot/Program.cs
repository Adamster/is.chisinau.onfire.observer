using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types;
using TelegramBot.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<TelegramBotOptions>()
    .Bind(builder.Configuration.GetSection(TelegramBotOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services
    .AddOptions<SupabaseOptions>()
    .Bind(builder.Configuration.GetSection(SupabaseOptions.SectionName))
    .Configure(options =>
    {
        options.ConnectionString ??= builder.Configuration.GetConnectionString(SupabaseOptions.SectionName);
    })
    .ValidateDataAnnotations();

builder.Services
    .AddOptions<RssOptions>()
    .Bind(builder.Configuration.GetSection(RssOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddHttpClient<IRssFetcher, RssFetcher>();
builder.Services.AddHttpClient<IArticleDetailsFetcher, ArticleDetailsFetcher>();
builder.Services.AddSingleton<ITelegramNotifier, TelegramNotifier>();
builder.Services.AddSingleton<IncidentCandidateStore>();
builder.Services.AddSingleton<TelegramUpdateParser>();
builder.Services.AddSingleton<TelegramWebhookHandler>();
builder.Services.AddSingleton<IIncidentRepository, SupabaseIncidentRepository>();
builder.Services.AddHostedService<RssPollingService>();
builder.Services.AddHostedService<ApprovalProcessingService>();
builder.Services.AddHostedService<TelegramWebhookSetupService>();

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapGet("/config", (IOptions<TelegramBotOptions> telegram, IOptions<SupabaseOptions> supabase, IOptions<RssOptions> rss) =>
    Results.Ok(new
    {
        telegram = new { telegram.Value.Enabled, telegram.Value.ChatId },
        supabase = new
        {
            hasConnectionString = !string.IsNullOrWhiteSpace(supabase.Value.ConnectionString),
            hasUrl = supabase.Value.Url is not null,
            hasServiceRoleKey = supabase.Value.ServiceRoleKey is not null
        },
        rss = new
        {
            rss.Value.FeedUrl,
            rss.Value.PollIntervalSeconds,
            keywordCount = rss.Value.Keywords.Count
        }
    }));

app.MapPost("/telegram/update", async (
    HttpRequest request,
    TelegramUpdateParser parser,
    TelegramWebhookHandler handler,
    IOptions<TelegramBotOptions> options,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    if (!options.Value.Enabled)
    {
        return Results.Ok(new { status = "disabled" });
    }

    var update = await parser.ParseAsync(request, cancellationToken);
    if (update is null)
    {
        logger.LogWarning("Unable to parse Telegram update payload.");
        return Results.Ok(new { status = "invalid" });
    }

    var handled = await handler.HandleUpdateAsync(update, cancellationToken);
    return Results.Ok(new { status = handled ? "ok" : "ignored" });
});

app.Run();

public sealed class TelegramBotOptions
{
    public const string SectionName = "Telegram";

    public bool Enabled { get; init; } = true;

    public string? BotToken { get; init; }

    public string? ChatId { get; init; }

    [Url]
    public string? WebhookUrl { get; init; }
}

public sealed class SupabaseOptions
{
    public const string SectionName = "Supabase";

    public string? ConnectionString { get; set; }

    public string? Url { get; set; }

    public string? ServiceRoleKey { get; set; }

    [Range(30, 86_400)]
    public int PollIntervalSeconds { get; init; } = 60;
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
