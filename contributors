// Section 7 - Contributors of yt-dlp/yt-dlp.
//
// Collects, for the same period and activity types as Section 6
// (Sep 1 2025 - Aug 31 2026; bots removed):
//   - commits to the default branch (per GitHub login)
//   - pull requests opened
//   - issues opened (excluding pull requests)
// and the total number of contributors.
//
// Set the environment variable GITHUB_TOKEN to your fine-grained token before running
// (in Rider: Run > Edit Configurations > Environment variables).
// Output: contributors_last_year.csv (in the working directory) and a top-10 table.

using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

const string Repo = "yt-dlp/yt-dlp";
const string Api = "https://api.github.com";
var start = new DateTime(2025, 9, 1, 0, 0, 0, DateTimeKind.Utc);
var end = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc); // exclusive

// Accounts run by automation rather than people (same rule as Section 6)
var botLogins = new HashSet<string> { "dlp-bot" };
bool IsBot(string login) => login.EndsWith("[bot]") || botLogins.Contains(login);

var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
if (string.IsNullOrEmpty(token))
    Console.WriteLine("Warning: GITHUB_TOKEN not set - only 60 requests per hour are allowed.");

using var http = new HttpClient();
http.DefaultRequestHeaders.UserAgent.ParseAdd("CapstoneTeam38");
http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
if (!string.IsNullOrEmpty(token))
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

// GET a URL; returns the parsed JSON and the URL of the next page (or null)
async Task<(JsonElement Json, string? Next)> Get(string url)
{
    using var resp = await http.GetAsync(url);
    resp.EnsureSuccessStatusCode();
    var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()).RootElement.Clone();
    string? next = null;
    if (resp.Headers.TryGetValues("Link", out var links))
    {
        var m = Regex.Match(string.Join(",", links), "<([^>]+)>;\\s*rel=\"next\"");
        if (m.Success) next = m.Groups[1].Value;
    }
    return (json, next);
}

// Number of items of a list endpoint, read from the "last" link at per_page=1
async Task<int> Count(string url)
{
    using var resp = await http.GetAsync(url);
    resp.EnsureSuccessStatusCode();
    if (!resp.Headers.TryGetValues("Link", out var links)) return 1;
    var m = Regex.Match(string.Join(",", links), "page=(\\d+)>;\\s*rel=\"last\"");
    return m.Success ? int.Parse(m.Groups[1].Value) : 1;
}

var stats = new Dictionary<string, (int Commits, int Prs, int Issues)>();
void Add(string login, int commits = 0, int prs = 0, int issues = 0)
{
    stats.TryGetValue(login, out var s);
    stats[login] = (s.Commits + commits, s.Prs + prs, s.Issues + issues);
}

// 1) Total number of contributors (all-time)
var linked = await Count($"{Api}/repos/{Repo}/contributors?per_page=1");
var withAnon = await Count($"{Api}/repos/{Repo}/contributors?per_page=1&anon=true");
Console.WriteLine($"Contributors (GitHub accounts): {linked}");
Console.WriteLine($"Contributors (incl. anonymous e-mails): {withAnon}");

// 2) Commits on the default branch in the period
string? url = $"{Api}/repos/{Repo}/commits?per_page=100" +
              $"&since={start:yyyy-MM-ddTHH:mm:ssZ}&until={end:yyyy-MM-ddTHH:mm:ssZ}";
while (url != null)
{
    var (page, next) = await Get(url);
    url = next;
    foreach (var c in page.EnumerateArray())
    {
        string login;
        if (c.TryGetProperty("author", out var author) && author.ValueKind == JsonValueKind.Object)
        {
            login = author.GetProperty("login").GetString()!;
            if (IsBot(login)) continue;
        }
        else // e-mail not linked to a GitHub account
        {
            login = "(unlinked) " + c.GetProperty("commit").GetProperty("author").GetProperty("name").GetString();
        }
        Add(login, commits: 1);
    }
}

// 3) Issues and pull requests opened in the period (newest first)
url = $"{Api}/repos/{Repo}/issues?state=all&sort=created&direction=desc&per_page=100";
while (url != null)
{
    var (page, next) = await Get(url);
    url = next;
    foreach (var item in page.EnumerateArray())
    {
        var created = DateTime.Parse(item.GetProperty("created_at").GetString()!,
            CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        if (created < start) { url = null; break; } // sorted newest first: the rest is older
        if (created >= end) continue;

        var user = item.GetProperty("user");
        var login = user.GetProperty("login").GetString()!;
        if (IsBot(login) || user.GetProperty("type").GetString() == "Bot") continue;

        if (item.TryGetProperty("pull_request", out _)) Add(login, prs: 1);
        else Add(login, issues: 1);
    }
}

// 4) Output, ranked by commits, then PRs, then issues
var rows = stats.OrderByDescending(kv => kv.Value.Commits)
                .ThenByDescending(kv => kv.Value.Prs)
                .ThenByDescending(kv => kv.Value.Issues)
                .ToList();

var csv = new StringBuilder("login,commits,prs_opened,issues_opened,total\n");
foreach (var (login, s) in rows)
    csv.AppendLine($"\"{login}\",{s.Commits},{s.Prs},{s.Issues},{s.Commits + s.Prs + s.Issues}");
File.WriteAllText("contributors_last_year.csv", csv.ToString());

Console.WriteLine($"People with at least one commit in the period: {rows.Count(r => r.Value.Commits > 0)}");
Console.WriteLine($"People with any activity in the period: {rows.Count}\n");
Console.WriteLine($"{"#",2}  {"login",-28}{"commits",8}{"PRs",6}{"issues",8}");
var rank = 1;
foreach (var (login, s) in rows.Take(10))
    Console.WriteLine($"{rank++,2}  {login,-28}{s.Commits,8}{s.Prs,6}{s.Issues,8}");
Console.WriteLine($"\nCSV written to {Path.GetFullPath("contributors_last_year.csv")}");

// 5) Public GitHub profile of each top-10 contributor: account age, bio,
//    own (non-fork) repositories and their main languages
Console.WriteLine("\n=== Profiles of the top-10 contributors ===");
foreach (var (login, _) in rows.Take(10))
{
    if (login.StartsWith("(unlinked)")) continue;
    var (user, _) = await Get($"{Api}/users/{login}");
    var bio = user.GetProperty("bio").ValueKind == JsonValueKind.String ? user.GetProperty("bio").GetString() : "-";
    Console.WriteLine($"\n{login}  (on GitHub since {user.GetProperty("created_at").GetString()![..10]}, " +
                      $"{user.GetProperty("public_repos").GetInt32()} public repos, " +
                      $"{user.GetProperty("followers").GetInt32()} followers)  bio: {bio}");

    var (repos, _) = await Get($"{Api}/users/{login}/repos?type=owner&sort=pushed&per_page=100");
    var own = repos.EnumerateArray().Where(r => !r.GetProperty("fork").GetBoolean()).ToList();
    var languages = own.Select(r => r.GetProperty("language"))
                       .Where(l => l.ValueKind == JsonValueKind.String)
                       .GroupBy(l => l.GetString()!)
                       .OrderByDescending(g => g.Count())
                       .Select(g => $"{g.Key} ({g.Count()})");
    Console.WriteLine($"  languages of own repos: {string.Join(", ", languages)}");
    foreach (var r in own.OrderByDescending(r => r.GetProperty("stargazers_count").GetInt32()).Take(5))
    {
        var desc = r.GetProperty("description").ValueKind == JsonValueKind.String ? r.GetProperty("description").GetString() : "";
        var lang = r.GetProperty("language").ValueKind == JsonValueKind.String ? r.GetProperty("language").GetString() : "-";
        Console.WriteLine($"  - {r.GetProperty("name").GetString()} [{lang}, " +
                          $"{r.GetProperty("stargazers_count").GetInt32()} stars] {desc}");
    }
}
