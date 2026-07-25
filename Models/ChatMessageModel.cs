namespace FloatingAgent.Models;

public class ChatMessageModel
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public bool IsImage { get; set; }
    public string ImageBase64 { get; set; } = "";
    public string ActionIcon { get; set; } = "";
    public bool ImageExpanded { get; set; }

    public string CssClass => Role switch
    {
        "user" => "user",
        "assistant" => "assistant",
        "action" => "action",
        _ => ""
    };
}
