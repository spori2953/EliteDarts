using System;

namespace EliteDarts.Domain.Entities;

public enum MatchStatus { Pending, Running, Finished }

public class Match
{
    public int Id { get; set; }

    public int TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;

    public int RoundNo { get; set; }

    public int? Player1Id { get; set; }
    public Player? Player1 { get; set; }

    public int? Player2Id { get; set; }
    public Player? Player2 { get; set; }

    public int? BoardId { get; set; }
    public Board? Board { get; set; }

    public int BestOfLegs { get; set; } = 5;
    public MatchStatus Status { get; set; } = MatchStatus.Pending;

    public int? LegsPlayer1 { get; set; }
    public int? LegsPlayer2 { get; set; }

    public int? WinnerPlayerId { get; set; }
    public Player? WinnerPlayer { get; set; }
    public int? NextMatchId { get; set; }
    public Match? NextMatch { get; set; }
    public bool? IsWinnerGoesToPlayer1 { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CurrentLegNo { get; set; } = 1;
    public int P1Remaining { get; set; } = 501;
    public int P2Remaining { get; set; } = 501;
    public int? CurrentPlayerId { get; set; }
    public int CurrentDartInVisit { get; set; } = 0;
    public int VisitStartRemaining { get; set; } = 501;
    public int? LegStarterPlayerId { get; set; }
    public int? PendingCheckoutDartNo { get; set; }
    public bool PendingCheckoutConfirm { get; set; } = false;
    public int? PendingCheckoutWinnerPlayerId { get; set; }
    public string? PendingCheckoutRing { get; set; }
    public int? PendingCheckoutSector { get; set; }
    public MatchInputMode InputMode { get; set; } = MatchInputMode.Darts;
}