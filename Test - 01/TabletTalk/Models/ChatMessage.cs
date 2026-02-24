using System;

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SenderId { get; set; }
    public string Content { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}