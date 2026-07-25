using System.Collections.ObjectModel;
using FloatingAgent.Models;

namespace FloatingAgent.Services;

public class BlazorBridge
{
    private OpenCodeApiClient? _apiClient;
    private ScreenAutomationService? _screenService;
    private CancellationTokenSource? _cts;

    public ObservableCollection<ChatMessageModel> Messages { get; } = new();
    public bool IsExecuting => _cts != null;
    public bool CameraFlash { get; set; }

    public event Action? OnStateChanged;
    public event Action<string>? RequestEvent;
    public event Action? ScreenshotCaptured;

    public bool ShowSettings { get; set; }
    public bool AutoMinimize { get; set; } = true;
    public string CurrentProvider { get; set; } = "";
    public string CurrentModel { get; set; } = "";
    public bool ShowMinimizePrompt { get; set; }
    private TaskCompletionSource<bool>? _minimizeTcs;

    public void Initialize(OpenCodeApiClient apiClient, ScreenAutomationService screenService)
    {
        _apiClient = apiClient;
        _screenService = screenService;
        LoadModelInfo();
        AddMessage("assistant", "Floating agent ready. I can see and control your screen.");
    }

    private void LoadModelInfo()
    {
        var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        if (System.IO.File.Exists(path))
        {
            try
            {
                var json = System.IO.File.ReadAllText(path);
                var cfg = System.Text.Json.JsonSerializer.Deserialize<Models.AppConfig>(json);
                if (cfg != null)
                {
                    CurrentProvider = cfg.Provider;
                    CurrentModel = cfg.Model;
                }
            }
            catch { }
        }
    }

    private List<ChatMessage> BuildHistory(int maxMessages = 20)
    {
        var msgs = Messages
            .Where(m => m.Role != "action")
            .Select(m => new ChatMessage { Role = m.Role, Content = m.Content })
            .ToList();
        if (msgs.Count > 0 && msgs.Last().Role == "user")
            msgs.RemoveAt(msgs.Count - 1);
        return msgs.TakeLast(maxMessages).ToList();
    }

    public async Task HandleUserMessage(string text)
    {
        if (_apiClient == null || _screenService == null) return;
        if (_cts is not null) { _cts.Cancel(); _cts.Dispose(); }
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        NotifyState();
        var needsScreen = NeedsScreenshot(text);
        if (AutoMinimize && needsScreen)
        {
            _minimizeTcs = new TaskCompletionSource<bool>();
            ShowMinimizePrompt = true;
            NotifyState();

            var shouldMinimize = await _minimizeTcs.Task;
            ShowMinimizePrompt = false;

            if (shouldMinimize)
                RequestEvent?.Invoke("minimize");
        }

        AddMessage("user", text);

        try
        {
            AddAction("Thinking", "🤔");
            var history = BuildHistory();

            string response;
            if (needsScreen)
            {
                if (token.IsCancellationRequested) return;
                var b64 = await CaptureWithFlash();
                if (b64 != null) AddScreenshot(b64);
                response = await _apiClient.SendScreenshotAsync(b64!, text, history, token);
            }
            else
            {
                response = await _apiClient.SendMessageAsync(text, history, token);
            }

            if (token.IsCancellationRequested) return;
            AddMessage("assistant", ActionParser.StripActions(response));
            await RunActions(response, text, token);
        }
        catch (OperationCanceledException) { AddAction("Agent stopped", "⏹"); }
        catch (Exception ex) { AddMessage("assistant", $"Error: {ex.Message}"); }
        finally { _cts?.Dispose(); _cts = null; NotifyState(); }
    }

    public async Task HandleScreenshot()
    {
        if (_screenService == null || _apiClient == null) return;
        if (_cts is not null) { _cts.Cancel(); _cts.Dispose(); }
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        NotifyState();
        if (AutoMinimize) RequestEvent?.Invoke("minimize");

        AddMessage("user", "[Analyzing screen...]");

        try
        {
            AddAction("Thinking", "🤔");
            if (token.IsCancellationRequested) return;

            var b64 = await CaptureWithFlash();
            if (b64 != null) AddScreenshot(b64);
            var history = BuildHistory(10);
            var response = await _apiClient.SendScreenshotAsync(b64!, "What is on my screen?", history, token);

            if (token.IsCancellationRequested) return;
            AddMessage("assistant", ActionParser.StripActions(response));
            await RunActions(response, "Analyze screen", token);
        }
        catch (OperationCanceledException) { AddAction("Agent stopped", "⏹"); }
        catch (Exception ex) { AddMessage("assistant", $"Error: {ex.Message}"); }
        finally { _cts?.Dispose(); _cts = null; NotifyState(); }
    }

    private async Task<string?> CaptureWithFlash()
    {
        CameraFlash = true;
        NotifyState();
        await Task.Delay(200);
        var b64 = _screenService?.CaptureScreenAsBase64();
        CameraFlash = false;
        NotifyState();
        return b64;
    }

    private static bool HasDone(string s) => s.Contains("[DONE]", StringComparison.OrdinalIgnoreCase);

    private async Task RunActions(string response, string task, CancellationToken token)
    {
        var actions = ActionParser.Collect(response);
        if (actions.Count == 0)
        {
            if (HasDone(response)) AddAction("Task complete", "✅");
            return;
        }

        if (HasDone(response)) { AddAction("Task complete", "✅"); return; }

        NotifyState();

        try
        {
            var queue = new Queue<AnnotatedAction>(actions);
            var completed = false;

            for (int step = 0; step < 30 && !token.IsCancellationRequested; step++)
            {
                if (queue.Count == 0) break;
                var batch = queue.ToList();
                queue.Clear();

                if (step > 0) AddAction("Continuing", "⏩");

                _screenService!.FadeOut();
                await Task.Delay(160, token);
                _screenService!.HideFromScreen();
                try
                {
                    foreach (var a in batch)
                    {
                        var icon = a.Type switch
                        {
                            ActionType.Click => "🖱",
                            ActionType.Type => "⌨",
                            ActionType.Press => "🔘",
                            ActionType.Launch => "🚀",
                            ActionType.Open => "🌐",
                            ActionType.MoveTo => "➡",
                            ActionType.Wait => "⏳",
                            _ => "⚡"
                        };
                        AddAction(a.GetDisplayText(), icon);
                        ActionParser.Execute(_screenService, a);
                    }
                }
                finally
                {
                    _screenService.ShowOnScreen();
                    _screenService.FadeIn();
                }

                if (queue.Count == 0)
                {
                    await Task.Delay(100, token);
                    if (token.IsCancellationRequested) return;
                    var screen = _screenService.CaptureScreenAsBase64();
                    AddScreenshot(screen);
                    var ctx = BuildHistory(20);
                    var prompt = $"Task: {task}\nLast actions executed. Updated screen attached. Continue working toward the task. What next? Reply with an action command or [DONE] if finished.";
                    var resp = await _apiClient!.SendScreenshotAsync(screen, prompt, ctx, token);

                    if (token.IsCancellationRequested) return;
                    AddMessage("assistant", ActionParser.StripActions(resp));

                    if (HasDone(resp)) { AddAction("Task complete", "✅"); completed = true; break; }

                    var next = ActionParser.Collect(resp);
                    if (next.Count == 0)
                    {
                        var r2 = await _apiClient.SendScreenshotAsync(screen, $"Task: {task}\nYour response had no action commands. You MUST output an action command now — look at the screen and choose the next action. Reply with EXACTLY one line like [CLICK x y left] or [DONE] and nothing else.", ctx, token);
                        if (token.IsCancellationRequested) return;
                        AddMessage("assistant", ActionParser.StripActions(r2));
                        if (HasDone(r2)) { completed = true; break; }
                        next = ActionParser.Collect(r2);
                    }
                    if (next.Count == 0)
                    {
                        AddAction("No further actions", "⏹");
                        break;
                    }
                    foreach (var n in next) queue.Enqueue(n);
                }
            }
            if (!completed && !token.IsCancellationRequested) AddAction("Finished", "✅");
        }
        catch (OperationCanceledException) { AddAction("Agent stopped", "⏹"); }
        catch (Exception ex) { AddMessage("assistant", $"[Error: {ex.Message}]"); }
    }

    public void AddMessage(string role, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        App.Current.Dispatcher.Invoke(() =>
        {
            Messages.Add(new ChatMessageModel { Role = role, Content = content });
            if (Messages.Count > 100) Messages.RemoveAt(0);
            NotifyState();
        });
    }

    public void AddAction(string text, string icon = "")
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        App.Current.Dispatcher.Invoke(() =>
        {
            Messages.Add(new ChatMessageModel { Role = "action", Content = text, ActionIcon = icon });
            if (Messages.Count > 100) Messages.RemoveAt(0);
            NotifyState();
        });
    }

    public void AddScreenshot(string base64, string label = "")
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            Messages.Add(new ChatMessageModel { Role = "action", Content = label, IsImage = true, ImageBase64 = base64 });
            if (Messages.Count > 100) Messages.RemoveAt(0);
            NotifyState();
        });
        ScreenshotCaptured?.Invoke();
    }

    public void ApplyConfig(AppConfig config)
    {
        AutoMinimize = config.AutoMinimize;
        CurrentProvider = config.Provider;
        CurrentModel = config.Model;

        if (!string.IsNullOrEmpty(config.OpenCodeApiUrl) && !string.IsNullOrEmpty(config.Model))
        {
            _apiClient = new OpenCodeApiClient(config.OpenCodeApiUrl, config.ApiKey, config.Model);
            App.ApiClient = _apiClient;

            AddAction($"Switched to {CurrentProvider} · {CurrentModel}", "🔄");
        }

        RequestEvent?.Invoke("config");
    }

    public void ShowChat()
    {
        ShowSettings = false;
        NotifyState();
    }

    public void ConfirmMinimize() => _minimizeTcs?.TrySetResult(true);
    public void CancelMinimize() => _minimizeTcs?.TrySetResult(false);

    public void StopAgent() { _cts?.Cancel(); AddAction("Agent stopped", "⏹"); }
    public void Minimize() => RequestEvent?.Invoke("minimize");
    public void Close() => RequestEvent?.Invoke("close");

    public void CopyChat()
    {
        var text = string.Join("\n", Messages.Select(m => $"{(m.Role == "user" ? "You" : "Agent")}: {m.Content}"));
        try { System.Windows.Clipboard.SetText(text); AddMessage("action", "Conversation copied."); } catch { }
    }

    private static readonly string[] ActionTriggers = ["click", "double click", "right click", "open", "launch", "go to", "type", "press", "scroll", "select", "minimize", "maximize", "close", "move", "find", "search"];
    private static readonly string[] ViewTriggers = ["screen", "desktop", "see", "look", "show"];
    private bool NeedsScreenshot(string msg)
    {
        var lower = msg.ToLower();
        if (ActionTriggers.Any(t => lower.Contains(t))) return true;
        if (ViewTriggers.Any(t => lower.Contains(t))) return true;
        return false;
    }
    public void NotifyState() => App.Current.Dispatcher.Invoke(() => OnStateChanged?.Invoke());
}
