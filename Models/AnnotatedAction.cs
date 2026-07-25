namespace FloatingAgent.Models;

public enum ActionType
{
    Click,
    MoveTo,
    Type,
    Press,
    Wait,
    Launch,
    Open
}

public class AnnotatedAction
{
    public ActionType Type { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }
    public string? Text { get; set; }
    public string OriginalLine { get; set; } = "";
    public int Index { get; set; }

    public string GetDisplayText()
    {
        return Type switch
        {
            ActionType.Click => Text != null ? $"Click ({X}, {Y}) - {Text}" : $"Click ({X}, {Y})",
            ActionType.MoveTo => $"Move to ({X}, {Y})",
            ActionType.Type => $"Type \"{Text}\"",
            ActionType.Press => $"Press {Text}",
            ActionType.Wait => $"Wait {Text}ms",
            ActionType.Launch => $"Launch \"{Text}\"",
            ActionType.Open => $"Open \"{Text}\"",
            _ => OriginalLine
        };
    }
}
