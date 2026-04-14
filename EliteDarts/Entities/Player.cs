using System;

namespace EliteDarts.Domain.Entities;

public class Player
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string? Club { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}