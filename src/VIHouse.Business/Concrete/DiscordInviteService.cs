using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;

namespace VIHouse.Business.Concrete;

/// <summary>
/// POST /channels/{id}/invites on Discord's v10 API with max_uses = 1 and unique = true — one
/// link per person per channel per day. Every failure is logged and swallowed: the account page
/// must render with the static link when the bot is misconfigured, rate-limited or down.
/// </summary>
public class DiscordInviteService(HttpClient http, IMemoryCache cache, IOptions<DiscordOptions> options, ILogger<DiscordInviteService> logger) : IDiscordInviteService
{
    private readonly DiscordOptions opts = options.Value;

    public bool IsConfigured => opts.IsConfigured;

    public async Task<string?> CreateInviteAsync(Guid userId, string channelId, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(channelId)) return null;

        var cacheKey = $"discord-invite:{userId}:{channelId}";
        if (cache.TryGetValue(cacheKey, out string? cached) && cached is not null) return cached;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://discord.com/api/v10/channels/{channelId}/invites");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bot", opts.BotToken);
            request.Content = JsonContent.Create(new
            {
                max_age = opts.InviteMaxAgeSeconds,
                max_uses = 1,
                unique = true,
                temporary = false,
            });

            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Discord refused an invite for channel {ChannelId}: {Status} {Body}",
                    channelId, (int)response.StatusCode, await response.Content.ReadAsStringAsync(ct));
                return null;
            }

            var invite = await response.Content.ReadFromJsonAsync<InviteResponse>(cancellationToken: ct);
            if (invite?.Code is null) return null;

            var url = $"https://discord.gg/{invite.Code}";
            // A minute short of the invite's own life, so the cache never hands out a dead link.
            cache.Set(cacheKey, url, TimeSpan.FromSeconds(Math.Max(60, opts.InviteMaxAgeSeconds - 60)));
            return url;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Discord unreachable while creating an invite for channel {ChannelId}.", channelId);
            return null;
        }
    }

    private sealed record InviteResponse([property: JsonPropertyName("code")] string? Code);
}
