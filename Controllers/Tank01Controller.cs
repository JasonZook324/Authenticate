using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using Authenticate.Data;
using Authenticate.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Authenticate.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class Tank01Controller : ControllerBase
    {
        private readonly string? _apiKey;
        private readonly string? _baseUrl;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ApplicationDbContext _db;

        public Tank01Controller(IConfiguration configuration, IHttpClientFactory httpClientFactory, ApplicationDbContext db)
        {
            _apiKey = configuration["Tank01_x-rapidapi-key"];
            _baseUrl = configuration["Tank01_baseUrl"];
            _httpClientFactory = httpClientFactory;
            _db = db;
        }

        // GET: api/Tank01/nflteams
        [HttpGet("nflteams")]
        public async Task<IActionResult> GetNFLTeams()
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new System.Uri(_baseUrl);
            client.DefaultRequestHeaders.Add("x-rapidapi-key", _apiKey);

            var endpoint = "/getNFLTeams";
            var response = await client.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());

            var json = await response.Content.ReadAsStringAsync();
            var teams = JsonSerializer.Deserialize<object>(json);

            return Ok(teams);
        }

        // POST: api/Tank01/sync-nflteams
        [HttpPost("sync-nflteams")]
        public async Task<IActionResult> SyncNFLTeams()
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new System.Uri(_baseUrl);
            client.DefaultRequestHeaders.Add("x-rapidapi-key", _apiKey);

            var endpoint = "/getNFLTeams";
            var response = await client.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, await response.Content.ReadAsStringAsync());

            var json = await response.Content.ReadAsStringAsync();
            var root = JsonNode.Parse(json);
            var teamsNode = root?["body"];
            var teams = teamsNode?.Deserialize<List<Tank01NFLTeam>>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (teams == null)
                return BadRequest("No teams data found in API response.");

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
            return Ok("NFL teams synced successfully.");
        }
    }

    // Models for deserialization
    public class Tank01NFLTeam
    {
        public string? teamAbv { get; set; }
        public string? teamCity { get; set; }
        public CurrentStreak? currentStreak { get; set; }
        public string? loss { get; set; }
        public string? teamName { get; set; }
        public string? nflComLogo1 { get; set; }
        public string? teamID { get; set; }
        public string? tie { get; set; }
        public ByeWeeks? byeWeeks { get; set; }
        public string? division { get; set; }
        public string? conferenceAbv { get; set; }
        public string? pa { get; set; }
        public string? pf { get; set; }
        public string? espnLogo1 { get; set; }
        public string? wins { get; set; }
        public string? conference { get; set; }
    }

    public class CurrentStreak
    {
        public string? result { get; set; }
        public string? length { get; set; }
    }

    public class ByeWeeks : Dictionary<string, List<string>> { }
}