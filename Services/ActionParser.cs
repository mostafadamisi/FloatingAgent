using System.Text.Json;
using System.Text.RegularExpressions;
using FloatingAgent.Models;

namespace FloatingAgent.Services;

public static class ActionParser
{
    private static readonly Regex BracketAction = new(
        @"\[(?:LAUNCH|OPEN|CLICK|MOVETO|TYPE|PRESS|HOTKEY|WAIT|DONE)[^\]]*\]",
        RegexOptions.IgnoreCase);

    private static readonly Regex ToolCallXml = new(
        @"<tool_call>\s*(?:\{.*?\})\s*</tool_call>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex ToolResultXml = new(
        @"<tool_result>[^<]*</tool_result>",
        RegexOptions.IgnoreCase);

    public static string StripActions(string text)
    {
        var cleaned = ToolCallXml.Replace(text, "");
        cleaned = ToolResultXml.Replace(cleaned, "");
        cleaned = BracketAction.Replace(cleaned, "").Trim();
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n");
        return cleaned.Trim();
    }

    public static List<AnnotatedAction> Collect(string response)
    {
        var actions = new List<AnnotatedAction>();

        foreach (Match m in BracketAction.Matches(response))
        {
            var t = m.Value;
            var idx = actions.Count + 1;

            if (t.StartsWith("[DONE", StringComparison.OrdinalIgnoreCase))
                continue;

            if (t.StartsWith("[CLICK", StringComparison.OrdinalIgnoreCase))
            {
                var p = t.Trim('[', ']').Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 3 && int.TryParse(p[1], out var x) && int.TryParse(p[2], out var y))
                    actions.Add(new AnnotatedAction { Type = ActionType.Click, X = x, Y = y, Text = p.Length > 3 ? p[3].ToLower() : null, OriginalLine = t, Index = idx });
            }
            else if (t.StartsWith("[HOTKEY", StringComparison.OrdinalIgnoreCase))
            {
                var s = t.IndexOf(' ');
                if (s > 0) actions.Add(new AnnotatedAction { Type = ActionType.Press, Text = t.Substring(s).Trim().Trim('[', ']', '"').ToUpper(), OriginalLine = t, Index = idx });
            }
            else if (t.StartsWith("[TYPE", StringComparison.OrdinalIgnoreCase))
            {
                var s = t.IndexOf('"'); var e = t.LastIndexOf('"');
                if (s >= 0 && e > s) actions.Add(new AnnotatedAction { Type = ActionType.Type, Text = t.Substring(s + 1, e - s - 1), OriginalLine = t, Index = idx });
            }
            else if (t.StartsWith("[MOVETO", StringComparison.OrdinalIgnoreCase))
            {
                var p = t.Trim('[', ']').Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 3 && int.TryParse(p[1], out var x) && int.TryParse(p[2], out var y))
                    actions.Add(new AnnotatedAction { Type = ActionType.MoveTo, X = x, Y = y, OriginalLine = t, Index = idx });
            }
            else if (t.StartsWith("[PRESS", StringComparison.OrdinalIgnoreCase))
            {
                var p = t.Trim('[', ']').Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 2) actions.Add(new AnnotatedAction { Type = ActionType.Press, Text = p[1].ToUpper(), OriginalLine = t, Index = idx });
            }
            else if (t.StartsWith("[WAIT", StringComparison.OrdinalIgnoreCase))
            {
                var p = t.Trim('[', ']').Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 2 && int.TryParse(p[1], out var ms)) actions.Add(new AnnotatedAction { Type = ActionType.Wait, Text = p[1], OriginalLine = t, Index = idx });
            }
            else if (t.StartsWith("[LAUNCH", StringComparison.OrdinalIgnoreCase))
            {
                var s = t.IndexOf('"'); var e = t.LastIndexOf('"');
                if (s >= 0 && e > s) actions.Add(new AnnotatedAction { Type = ActionType.Launch, Text = t.Substring(s + 1, e - s - 1), OriginalLine = t, Index = idx });
            }
            else if (t.StartsWith("[OPEN", StringComparison.OrdinalIgnoreCase))
            {
                var s = t.IndexOf('"'); var e = t.LastIndexOf('"');
                if (s >= 0 && e > s) actions.Add(new AnnotatedAction { Type = ActionType.Open, Text = t.Substring(s + 1, e - s - 1), OriginalLine = t, Index = idx });
            }
        }

        foreach (Match m in ToolCallXml.Matches(response))
        {
            try
            {
                var json = m.Value;
                var start = json.IndexOf('{');
                var end = json.LastIndexOf('}');
                if (start < 0 || end <= start) continue;
                json = json.Substring(start, end - start + 1);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var name = root.GetProperty("name").GetString()?.ToLower();
                if (name == "done" || name == null) continue;

                var args = root.GetProperty("arguments");
                var idx = actions.Count + 1;
                var original = m.Value;

                switch (name)
                {
                    case "launch":
                        actions.Add(new AnnotatedAction { Type = ActionType.Launch, Text = GetArg(args, "application") ?? GetArg(args, "app"), OriginalLine = original, Index = idx });
                        break;
                    case "open":
                        actions.Add(new AnnotatedAction { Type = ActionType.Open, Text = GetArg(args, "url"), OriginalLine = original, Index = idx });
                        break;
                    case "click":
                        if (TryGetInt(args, "x", out var cx) && TryGetInt(args, "y", out var cy))
                            actions.Add(new AnnotatedAction { Type = ActionType.Click, X = cx, Y = cy, Text = GetArg(args, "button") ?? "left", OriginalLine = original, Index = idx });
                        break;
                    case "moveto":
                    case "move_to":
                    case "move":
                        if (TryGetInt(args, "x", out var mx) && TryGetInt(args, "y", out var my))
                            actions.Add(new AnnotatedAction { Type = ActionType.MoveTo, X = mx, Y = my, OriginalLine = original, Index = idx });
                        break;
                    case "type":
                        actions.Add(new AnnotatedAction { Type = ActionType.Type, Text = GetArg(args, "text"), OriginalLine = original, Index = idx });
                        break;
                    case "press":
                        actions.Add(new AnnotatedAction { Type = ActionType.Press, Text = (GetArg(args, "key") ?? GetArg(args, "keys") ?? "").ToUpper(), OriginalLine = original, Index = idx });
                        break;
                    case "hotkey":
                    case "hot_key":
                        actions.Add(new AnnotatedAction { Type = ActionType.Press, Text = (GetArg(args, "keys") ?? GetArg(args, "key") ?? "").ToUpper(), OriginalLine = original, Index = idx });
                        break;
                    case "wait":
                        var ms = GetArg(args, "ms") ?? GetArg(args, "duration") ?? GetArg(args, "time");
                        if (ms != null)
                            actions.Add(new AnnotatedAction { Type = ActionType.Wait, Text = ms, OriginalLine = original, Index = idx });
                        break;
                }
            }
            catch { }
        }

        return actions;
    }

    private static string? GetArg(JsonElement args, string key)
    {
        if (args.ValueKind != JsonValueKind.Object) return null;
        if (args.TryGetProperty(key, out var prop))
            return prop.GetString() ?? prop.GetRawText().Trim('"');
        return null;
    }

    private static bool TryGetInt(JsonElement args, string key, out int val)
    {
        val = 0;
        if (args.ValueKind != JsonValueKind.Object) return false;
        if (args.TryGetProperty(key, out var prop))
        {
            if (prop.TryGetInt32(out val)) return true;
            if (prop.ValueKind == JsonValueKind.Number) { val = (int)prop.GetDouble(); return true; }
        }
        return false;
    }

    public static void Execute(ScreenAutomationService svc, AnnotatedAction a)
    {
        switch (a.Type)
        {
            case ActionType.Click:
                if (a.Text == "right") svc.RightClick(a.X!.Value, a.Y!.Value);
                else if (a.Text == "double") svc.DoubleClick(a.X!.Value, a.Y!.Value);
                else svc.LeftClick(a.X!.Value, a.Y!.Value);
                break;
            case ActionType.MoveTo: svc.MoveMouse(a.X!.Value, a.Y!.Value); break;
            case ActionType.Type: svc.TypeText(a.Text!); break;
            case ActionType.Press: HandlePress(svc, a); break;
            case ActionType.Wait: if (int.TryParse(a.Text, out var ms)) Thread.Sleep(ms); break;
            case ActionType.Launch: svc.LaunchApp(a.Text!); break;
            case ActionType.Open: svc.OpenUrl(a.Text!); break;
        }
    }

    private static void HandlePress(ScreenAutomationService svc, AnnotatedAction a)
    {
        var key = a.Text?.ToUpper();
        if (string.IsNullOrEmpty(key)) return;

        if (key.Contains('+')) { svc.PressHotKey(key); return; }

        var line = a.OriginalLine.ToUpper();
        if (line.Contains(" CTRL") || line.Contains(" CONTROL") || line.Contains(" ALT") || line.Contains(" SHIFT") || line.Contains(" WIN"))
        {
            var mod = line.Contains(" CTRL") || line.Contains(" CONTROL") ? "CTRL" : line.Contains(" ALT") ? "ALT" : line.Contains(" SHIFT") ? "SHIFT" : "WIN";
            svc.PressHotKey($"{mod}+{key}"); return;
        }

        var named = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            ["ENTER"] = 0x0D, ["RETURN"] = 0x0D, ["TAB"] = 0x09, ["ESC"] = 0x1B, ["ESCAPE"] = 0x1B,
            ["SPACE"] = 0x20, ["UP"] = 0x26, ["DOWN"] = 0x28, ["LEFT"] = 0x25, ["RIGHT"] = 0x27,
            ["DELETE"] = 0x2E, ["DEL"] = 0x2E, ["BACK"] = 0x08, ["BACKSPACE"] = 0x08,
            ["HOME"] = 0x24, ["END"] = 0x23, ["PAGEUP"] = 0x21, ["PAGEDOWN"] = 0x22, ["CAPSLOCK"] = 0x14
        };
        if (named.TryGetValue(key, out var vk)) svc.PressKey(vk);
        else if (key.Length == 1) svc.TypeText(key);
    }
}
