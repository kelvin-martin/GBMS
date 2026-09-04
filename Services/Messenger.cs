using System;

namespace GBMS.Services;

/// <summary>
/// Represents a message sent within the application.
/// </summary>
/// <param name="Message">The message content.</param>
/// <param name="IsAlert">Indicates whether the message represents an error.</param>
public record AppMessage(string Message, bool IsAlert = false);

/// <summary>
/// A messenger class for sending application-wide messages.
/// </summary>
public class Messenger
{
    /// <summary>
    /// Occurs when a message is received.
    /// </summary>
    public event EventHandler<AppMessage>? MessageReceived;

    /// <summary>
    /// Sends a message to all subscribers.
    /// </summary>
    /// <param name="text">The message content.</param>
    /// <param name="isAlert">Indicates whether the message represents an error an alert.</param>
    public void Send(string text, bool isAlert = false)
    {
        MessageReceived?.Invoke(
            this, new AppMessage(text, isAlert));
    }

}
