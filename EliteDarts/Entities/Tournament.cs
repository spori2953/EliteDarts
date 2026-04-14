using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using EliteDarts.Data;

namespace EliteDarts.Domain.Entities;

public enum TournamentStatus { Draft, Live, Finished }
public enum GameMode { X501_DO }

public class Tournament
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public DateTime StartDate { get; set; }

    public GameMode Mode { get; set; } = GameMode.X501_DO;
    public TournamentStatus Status { get; set; } = TournamentStatus.Draft;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? OwnerUserId { get; set; }

    [ForeignKey(nameof(OwnerUserId))]
    public ApplicationUser? OwnerUser { get; set; }

    public List<Board> Boards { get; set; } = new();
    public List<Entry> Entries { get; set; } = new();
    public List<Match> Matches { get; set; } = new();
}