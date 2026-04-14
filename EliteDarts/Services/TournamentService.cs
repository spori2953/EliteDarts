using System.Security.Claims;
using EliteDarts.Data;
using EliteDarts.Domain.Entities;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace EliteDarts.Services;

public class TournamentService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly AuthenticationStateProvider _authStateProvider;

    public TournamentService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        AuthenticationStateProvider authStateProvider)
    {
        _dbFactory = dbFactory;
        _authStateProvider = authStateProvider;
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
            throw new Exception("Nincs bejelentkezett felhasználó.");

        return userId;
    }

    public async Task<int> CreateTournamentAsync(string name, DateTime startDate, string location)
    {
        var userId = await GetCurrentUserIdAsync();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var t = new Tournament
        {
            Name = name.Trim(),
            Location = location.Trim(),
            StartDate = startDate,
            Mode = GameMode.X501_DO,
            Status = TournamentStatus.Draft,
            OwnerUserId = userId
        };

        db.Tournaments.Add(t);
        await db.SaveChangesAsync();

        var boards = Enumerable.Range(1, 4).Select(n => new Board
        {
            TournamentId = t.Id,
            BoardNumber = n
        });

        db.Boards.AddRange(boards);
        await db.SaveChangesAsync();

        return t.Id;
    }

    public async Task<List<Tournament>> GetTournamentsAsync()
    {
        var userId = await GetCurrentUserIdAsync();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Tournaments
            .Where(x => x.OwnerUserId == userId)
            .OrderByDescending(x => x.StartDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
    }

    public async Task<Tournament?> GetTournamentByIdAsync(int tournamentId)
    {
        var userId = await GetCurrentUserIdAsync();

        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Tournaments
            .FirstOrDefaultAsync(x => x.Id == tournamentId && x.OwnerUserId == userId);
    }

    public async Task DeleteTournamentAsync(int tournamentId)
    {
        var userId = await GetCurrentUserIdAsync();

        await using var db = await _dbFactory.CreateDbContextAsync();

        var tournament = await db.Tournaments
            .FirstOrDefaultAsync(x => x.Id == tournamentId && x.OwnerUserId == userId);

        if (tournament is null)
            throw new Exception("A verseny nem található vagy nincs jogosultságod hozzá.");

        var matchIds = await db.Matches
            .Where(m => m.TournamentId == tournamentId)
            .Select(m => m.Id)
            .ToListAsync();

        var scores = await db.Scores
            .Where(s => matchIds.Contains(s.MatchId))
            .ToListAsync();

        var matches = await db.Matches
            .Where(m => m.TournamentId == tournamentId)
            .ToListAsync();

        var entries = await db.Entries
            .Where(e => e.TournamentId == tournamentId)
            .ToListAsync();

        var boards = await db.Boards
            .Where(b => b.TournamentId == tournamentId)
            .ToListAsync();

        db.Scores.RemoveRange(scores);
        db.Matches.RemoveRange(matches);
        db.Entries.RemoveRange(entries);
        db.Boards.RemoveRange(boards);
        db.Tournaments.Remove(tournament);

        await db.SaveChangesAsync();
    }
}