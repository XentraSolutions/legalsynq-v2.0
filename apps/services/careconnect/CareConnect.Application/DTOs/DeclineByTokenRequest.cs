namespace CareConnect.Application.DTOs;

public class DeclineByTokenRequest
{
    public string Token { get; set; } = string.Empty;
    public string? DeclineNotes { get; set; }
}
