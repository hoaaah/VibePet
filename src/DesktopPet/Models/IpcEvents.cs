using System.Text.Json.Serialization;

namespace DesktopPet.Models;

public class PetEventMessage
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = "notify"; // "start", "success", "error", "needs_action", "notify", "clear"

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("actionLabel")]
    public string? ActionLabel { get; set; }

    [JsonPropertyName("actionCommand")]
    public string? ActionCommand { get; set; }

    [JsonPropertyName("timeoutSeconds")]
    public int? TimeoutSeconds { get; set; }
}

public class PetEventResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "ok";

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
