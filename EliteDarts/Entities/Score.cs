using System;

namespace EliteDarts.Domain.Entities;

public enum ThrowSource { Manual, CV }

public enum MatchInputMode
{
    Darts = 0,
    VisitTotal = 1,
    Camera = 2
}

public class Score
{
    public int Id { get; set; }

    public int MatchId { get; set; }
    public Match Match { get; set; } = null!;

    public int SetNo { get; set; } = 1;
    public int LegNo { get; set; } = 1;
    public int ThrowIndex { get; set; }

    public string? Ring { get; set; }
    public int? Sector { get; set; }
    public int Points { get; set; }

    public int PlayerId { get; set; }
    public Player Player { get; set; } = null!;

    public double? Confidence { get; set; }
    public ThrowSource Source { get; set; } = ThrowSource.Manual;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}