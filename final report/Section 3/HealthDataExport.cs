using Octokit;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace CodeExamples;

//   issues_prs.csv - every issue and pull request of yt-dlp
//   commits.csv    - every commit on the default branch

public class HealthDataExport
{
    private readonly GitHubClient client;
    private readonly string repoOwner = "yt-dlp";
    private readonly string repoName = "yt-dlp";

    public HealthDataExport()
    {
        client = new GitHubClient(new ProductHeaderValue("Capstone"));
        client.Credentials = new Credentials(APIKey.GetAPIKey());
    }

    public void ExportIssuesAndPullRequests(string outputPath)
    {
        Console.WriteLine("Downloading issues and pull requests");
        
        var issueRequest = new RepositoryIssueRequest { State = ItemStateFilter.All };
        var issues = client.Issue.GetAllForRepository(repoOwner, repoName, issueRequest,
            new ApiOptions { PageSize = 100 }).Result;

        using (var writer = new StreamWriter(outputPath))
        {
            writer.WriteLine("Number,Type,Author,AuthorType,CreatedAt,ClosedAt,MergedAt,State,Comments,Title");

            foreach (var item in issues)
            {
                // GitHub returns issues and pull requests from the same endpoint
                string type = item.PullRequest == null ? "Issue" : "PR";

                // Deleted accounts have no user anymore, GitHub shows them as "ghost"
                string author = item.User?.Login ?? "ghost";

                // GitHub's own label for the account: "User" or "Bot" (needed for bot filtering)
                string authorType = item.User?.Type?.ToString() ?? "unknown";

                // Not every item is closed or merged, then we leave the field empty
                string closedAt = item.ClosedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
                string mergedAt = item.PullRequest?.MergedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";

                writer.WriteLine($"{item.Number},{type},{author},{authorType}," +
                                 $"{item.CreatedAt:yyyy-MM-dd HH:mm:ss},{closedAt},{mergedAt}," +
                                 $"{item.State.StringValue},{item.Comments},{Clean(item.Title)}");
            }
        }

        Console.WriteLine($"Saved {issues.Count} issues and pull requests to {Path.GetFullPath(outputPath)}");
    }

    public void ExportCommits(string outputPath)
    {
        Console.WriteLine("Downloading commits");

        var commits = client.Repository.Commit.GetAll(repoOwner, repoName,
            new ApiOptions { PageSize = 100 }).Result;

        using (var writer = new StreamWriter(outputPath))
        {
            writer.WriteLine("Sha,AuthorDate,AuthorName,AuthorEmail,AuthorLogin,CommitterName,AuthoredBy,Title");

            foreach (var c in commits)
            {
                string message = c.Commit.Message ?? "";

                // The title of a commit is the first line of its message
                string title = message.Split('\n')[0];

                // yt-dlp writes the real author at the end of the message, e.g. "Authored by: bashonly".
                // We take the text after "Authored by:" until the end of that line.
                string authoredBy = "";
                int index = message.IndexOf("Authored by:");
                if (index >= 0)
                {
                    authoredBy = message.Substring(index + "Authored by:".Length).Split('\n')[0].Trim();
                }

                // Name and email come from git itself, the login is the GitHub account
                // that GitHub linked to that email (empty if the email has no account).
                // Comparing them helps us find people who use more than one name or email.
                string login = c.Author?.Login ?? "";

                writer.WriteLine($"{c.Sha.Substring(0, 7)},{c.Commit.Author.Date:yyyy-MM-dd HH:mm:ss}," +
                                 $"{Clean(c.Commit.Author.Name)},{Clean(c.Commit.Author.Email)},{login}," +
                                 $"{Clean(c.Commit.Committer.Name)},{Clean(authoredBy)},{Clean(title)}");
            }
        }

        Console.WriteLine($"Saved {commits.Count} commits to {Path.GetFullPath(outputPath)}");
    }

    // Titles and names can contain commas or quotes, which would break the CSV columns.
    // So we put the text between quotes and double any quotes inside it (normal CSV rule).
    private static string Clean(string text)
    {
        if (text == null)
        {
            return "\"\"";
        }
        text = text.Replace("\r", " ").Replace("\n", " ").Replace("\"", "\"\"");
        return "\"" + text + "\"";
    }
}