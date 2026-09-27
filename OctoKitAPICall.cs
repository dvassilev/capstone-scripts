using Octokit;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace CodeExamples;

public class OctoKitAPICall
{
    private readonly GitHubClient client;
    private readonly string repoOwner = "yt-dlp";
    private readonly string repoName = "yt-dlp";

    public OctoKitAPICall()
    {
        client = new GitHubClient(new ProductHeaderValue("TUe-Architecture-Report"));
        client.Credentials = new Credentials(APIKey.GetAPIKey());
    }

    public void ExportActivitiesToCsv(string outputPath)
    {
        // Start on the 1st of the month, 12 months ago (e.g. run in Sept 2026 -> 1 Sept 2025),
        // so the plot has 12 full months instead of starting with a partial one
        var now = DateTimeOffset.UtcNow;
        var oneYearAgo = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-12);

        // All commits in the last year
        var allCommits = client.Repository.Commit.GetAll(repoOwner, repoName,
            new CommitRequest { Since = oneYearAgo }).Result;

        // Commits touching the extractors folder
        var extractorCommits = client.Repository.Commit.GetAll(repoOwner, repoName,
            new CommitRequest { Since = oneYearAgo, Path = "yt_dlp/extractor" }).Result;
        var extractorShas = new HashSet<string>(extractorCommits.Select(c => c.Sha));

        // Issues and pull requests (GitHub returns both from the same endpoint)
        var issueRequest = new RepositoryIssueRequest { State = ItemStateFilter.All, Since = oneYearAgo };
        var issues = client.Issue.GetAllForRepository(repoOwner, repoName, issueRequest).Result;

        // Releases
        var releases = client.Repository.Release.GetAll(repoOwner, repoName).Result;

        using (var writer = new StreamWriter(outputPath))
        {
            writer.WriteLine("Date,Type,Author,Ref");

            foreach (var c in allCommits)
            {
                string commitType = extractorShas.Contains(c.Sha) ? "Extractor Commit" : "Core Commit";

                // Author is saved so we can remove automated (bot) commits later in Python
                string author = c.Author?.Login ?? "unknown";
                writer.WriteLine($"{c.Commit.Committer.Date:yyyy-MM-dd HH:mm:ss},{commitType},{author},{c.Sha.Substring(0, 7)}");
            }

            foreach (var item in issues)
            {
                if (item.CreatedAt >= oneYearAgo)
                {
                    string type = item.PullRequest == null ? "Issue" : "Pull Request";
                    writer.WriteLine($"{item.CreatedAt:yyyy-MM-dd HH:mm:ss},{type},{item.User.Login},#{item.Number}");
                }
            }

            foreach (var r in releases)
            {
                if (r.PublishedAt >= oneYearAgo)
                {
                    writer.WriteLine($"{r.PublishedAt:yyyy-MM-dd HH:mm:ss},Release,{r.Author.Login},{r.TagName}");
                }
            }
        }

        Console.WriteLine($"Data exported successfully to {outputPath}");
    }
}