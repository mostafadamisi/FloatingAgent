namespace FloatingAgent.Models;

public class AppConfig
{
    public string Provider { get; set; } = "OpenCode Zen";
    public string OpenCodeApiUrl { get; set; } = "https://opencode.ai/zen/v1";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "mimo-v2.5-free";
    public bool Topmost { get; set; } = true;
    public bool AutoMinimize { get; set; } = true;
    public int WindowWidth { get; set; } = 440;
    public int WindowHeight { get; set; } = 640;
}

public class ProviderInfo
{
    public string Name { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string DefaultModel { get; set; } = "";
    public string Note { get; set; } = "";
    public bool RequiresKey { get; set; } = true;

    public static List<ProviderInfo> All = new()
    {
        new() { Name = "OpenCode Zen", BaseUrl = "https://opencode.ai/zen/v1", DefaultModel = "mimo-v2.5-free", Note = "Free vision model", RequiresKey = true },
        new() { Name = "OpenAI", BaseUrl = "https://api.openai.com/v1", DefaultModel = "gpt-4o", Note = "Fast, high quality" },
        new() { Name = "OpenRouter", BaseUrl = "https://openrouter.ai/api/v1", DefaultModel = "openai/gpt-4o", Note = "Access many models" },
        new() { Name = "Groq", BaseUrl = "https://api.groq.com/openai/v1", DefaultModel = "llama-3.2-90b-vision-preview", Note = "Very fast inference" },
        new() { Name = "Together AI", BaseUrl = "https://api.together.xyz/v1", DefaultModel = "meta-llama/Llama-3.2-11B-Vision-Instruct-Turbo", Note = "Open-source models" },
        new() { Name = "DeepSeek", BaseUrl = "https://api.deepseek.com/v1", DefaultModel = "deepseek-chat", Note = "Vision capable" },
        new() { Name = "GitHub Models", BaseUrl = "https://models.inference.ai.azure.com", DefaultModel = "gpt-4o", Note = "Free tier available" },
        new() { Name = "Ollama (Local)", BaseUrl = "http://localhost:11434/v1", DefaultModel = "llava", Note = "Run locally, no key", RequiresKey = false },
    };
}
