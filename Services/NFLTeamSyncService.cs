using System.Threading.Tasks;
using Authenticate.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Text.Json;
using System.Collections.Generic;
using Authenticate.Controllers;
using Authenticate.Data.Entities;

public class NFLTeamSyncService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _apiKey;
    private readonly string? _baseUrl;

    public NFLTeamSyncService(ApplicationDbContext db, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _apiKey = configuration["Tank01_x-rapidapi-key"];
        _baseUrl = configuration["Tank01_baseUrl"];
    }

    public async Task<string> SyncNFLTeamsAsync()
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new System.Uri(_baseUrl);
        client.DefaultRequestHeaders.Add("x-rapidapi-key", _apiKey);

        var endpoint = "/getNFLTeams";
        var response = await client.GetAsync(endpoint);

        if (!response.IsSuccessStatusCode)
            return await response.Content.ReadAsStringAsync();

        var json = await response.Content.ReadAsStringAsync();
        var root = JsonDocument.Parse(json);
        var teamsNode = root.RootElement.TryGetProperty("body", out var body) ? body : root.RootElement;
        var teams = JsonSerializer.Deserialize<List<Tank01NFLTeam>>(teamsNode.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (teams == null)
            return "No teams data found in API response.";

        foreach (var team in teams)
        {
            var existing = await _db.NFLTeams.FirstOrDefaultAsync(t => t.TeamID == team.teamID);
            var currentStreakJson = JsonSerializer.Serialize(team.currentStreak);
            var byeWeeksJson = JsonSerializer.Serialize(team.byeWeeks);

            if (existing == null)
            {
                _db.NFLTeams.Add(new NFLTeam
                {
                    TeamID = team.teamID,
                    TeamAbv = team.teamAbv,
                    TeamCity = team.teamCity,
                    TeamName = team.teamName,
                    Division = team.division,
                    ConferenceAbv = team.conferenceAbv,
                    Conference = team.conference,
                    NflComLogo1 = team.nflComLogo1,
                    EspnLogo1 = team.espnLogo1,
                    Loss = team.loss,
                    Tie = team.tie,
                    Wins = team.wins,
                    Pa = team.pa,
                    Pf = team.pf,
                    CurrentStreakJson = currentStreakJson,
                    ByeWeeksJson = byeWeeksJson
                });
            }
            else
            {
                existing.TeamAbv = team.teamAbv;
                existing.TeamCity = team.teamCity;
                existing.TeamName = team.teamName;
                existing.Division = team.division;
                existing.ConferenceAbv = team.conferenceAbv;
                existing.Conference = team.conference;
                existing.NflComLogo1 = team.nflComLogo1;
                existing.EspnLogo1 = team.espnLogo1;
                existing.Loss = team.loss;
                existing.Tie = team.tie;
                existing.Wins = team.wins;
                existing.Pa = team.pa;
                existing.Pf = team.pf;
                existing.CurrentStreakJson = currentStreakJson;
                existing.ByeWeeksJson = byeWeeksJson;
            }
        }

        await _db.SaveChangesAsync();
        return "NFL teams synced successfully.";
    }
}