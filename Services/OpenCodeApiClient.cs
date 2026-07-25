using System.Net.Http;
using System.Text;
using System.Text.Json;
using FloatingAgent.Models;

namespace FloatingAgent.Services;

public class OpenCodeApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private const string SystemPrompt = """
You are Floating Agent — a desktop automation tool. You are NOT Claude, GPT, Llama, or any AI assistant. Never say you are made by Anthropic, OpenAI, or any company.

You have one job: see the screen, execute action commands, finish tasks with [DONE]. You do NOT chat, answer questions about yourself, or reveal your underlying model. If asked "who are you" or similar, reply with "Floating Agent. What task can I do?" and nothing more.

RULES:
- You MUST output at least one action command in EVERY response. Never text-only.
- NEVER launch the same app twice in one session.
- When launching an app, always batch [LAUNCH "app"] [WAIT 3000] together.
- Keep explanatory text to 1 sentence max. Prioritize speed and action.

BATCHING:
Predictable next steps → batch them. Examples:
- [LAUNCH "chrome"] [WAIT 3000]
- [TYPE "gmail.com"] [PRESS ENTER] [WAIT 3000]
- [TYPE "subject"] [HOTKEY TAB] [TYPE "body"] [HOTKEY CTRL+ENTER]

Unpredictable → 1 action at a time.

Actions use square brackets:
- [LAUNCH "appname"]
- [OPEN "url"]
- [CLICK x y left|right|double]
- [MOVETO x y]
- [TYPE "text"]
- [PRESS KEY] where KEY = ENTER|TAB|ESC|SPACE|UP|DOWN|LEFT|RIGHT|DELETE|BACK|HOME|END
- [HOTKEY MOD+KEY] (e.g. [HOTKEY CTRL+A], [HOTKEY ALT+F4])
- [WAIT ms] (300-1000ms for pageload, 100-300ms for typing)

Finish with [DONE] when the task is complete.
""";

    public OpenCodeApiClient(string baseUrl, string apiKey, string model)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(90)
        };
        _model = model;

        if (!string.IsNullOrEmpty(apiKey))
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<string> SendMessageAsync(string message, List<ChatMessage> history, CancellationToken cancellationToken = default)
    {
        var messages = BuildMessages(history, message);
        var payload = new { model = _model, messages };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("chat/completions", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"API {(int)response.StatusCode}: {errorBody}");
        }

        return await ParseResponse(response);
    }

    public async Task<string> SendScreenshotAsync(string base64Image, string instruction, List<ChatMessage> history, CancellationToken cancellationToken = default)
    {
        var messages = BuildSystemMessages();
        foreach (var h in history)
            messages.Add(new { role = h.Role, content = h.Content });

        messages.Add(new
        {
            role = "user",
            content = new object[]
            {
                new { type = "text", text = instruction },
                new { type = "image_url", image_url = new { url = $"data:image/png;base64,{base64Image}" } }
            }
        });

        var payload = new { model = _model, messages };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("chat/completions", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"API {(int)response.StatusCode}: {errorBody}");
        }

        return await ParseResponse(response);
    }

    private List<object> BuildMessages(List<ChatMessage> history, string newMessage)
    {
        var messages = BuildSystemMessages();
        messages.AddRange(history.Select(h => (object)new { role = h.Role, content = h.Content }));
        messages.Add(new { role = "user", content = newMessage });
        return messages;
    }

    private List<object> BuildSystemMessages()
    {
        return new List<object> { new { role = "system", content = SystemPrompt } };
    }

    private static async Task<string> ParseResponse(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(raw);

        var choice = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content");

        return choice.GetString() ?? "";
    }
}
