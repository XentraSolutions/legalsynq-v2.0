namespace Intake.Application.Configuration;

public sealed class IntakeConfigurationException(
    int statusCode,
    string code,
    string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;

    public static IntakeConfigurationException BadRequest(string code, string message) =>
        new(400, code, message);

    public static IntakeConfigurationException Forbidden(string code, string message) =>
        new(403, code, message);

    public static IntakeConfigurationException NotFound(string code, string message) =>
        new(404, code, message);

    public static IntakeConfigurationException Conflict(string code, string message) =>
        new(409, code, message);
}