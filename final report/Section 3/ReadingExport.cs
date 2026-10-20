using Octokit;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace CodeExamples;

// For every pull request on our reading list (reading_list.csv) this saves everything we need
// to read it: the description, the labels, who closed it, and all comments, reviews and
// comments on lines of code, in time order. The result is one text file (reading_notes.txt),
// so we can read the pull requests and copy exact quotes from them.
public class ReadingExport
{
    private readonly GitHubClient client;
    private readonly string repoOwner = "yt-dlp";
    private readonly string repoName = "yt-dlp";

    public ReadingExport()
    {
        client = new GitHubClient(new ProductHeaderValue("TUe-Architecture-Report"));
        client.Credentials = new Credentials(APIKey.GetAPIKey());
    }

    public void ExportPullRequests(string readingListPath, string outputPath)
    {
        // reading_list.csv has the columns: Number,Why,Author,Title,Link
        // We only need the first two (the titles can contain commas, so we do not use them here)
        var lines = File.ReadAllLines(readingListPath).Skip(1);

        using (var writer = new StreamWriter(outputPath))
        {
            foreach (var line in lines)
            {
                var parts = line.Split(',');
                int number = int.Parse(parts[0]);
                string why = parts[1];

                Console.WriteLine($"PR #{number} ({why})");

                // The pull request itself: title, description, labels, who closed it
                var pr = client.Issue.Get(repoOwner, repoName, number).Result;
                string labels = string.Join(", ", pr.Labels.Select(l => l.Name));
                string closed = pr.ClosedAt == null
                    ? "still open"
                    : $"{pr.ClosedAt:yyyy-MM-dd HH:mm} by {pr.ClosedBy?.Login ?? "unknown"}";

                writer.WriteLine($"==================== PR #{number} ({why}) ====================");
                writer.WriteLine($"Link:    https://github.com/{repoOwner}/{repoName}/pull/{number}");
                writer.WriteLine($"Author:  {pr.User?.Login ?? "ghost"}");
                writer.WriteLine($"Title:   {pr.Title}");
                writer.WriteLine($"Opened:  {pr.CreatedAt:yyyy-MM-dd HH:mm}");
                writer.WriteLine($"Closed:  {closed}");
                writer.WriteLine($"Labels:  {labels}");
                writer.WriteLine("--- Description ---");
                writer.WriteLine((pr.Body ?? "(empty)").Replace("\r", ""));
                writer.WriteLine("--- Comments, reviews and line comments (in time order) ---");

                // We collect every reaction as (time, who, type, text) and sort them by time
                var reactions = new List<(DateTimeOffset Time, string Who, string Type, string Text)>();

                // 1. Normal comments in the conversation
                var comments = client.Issue.Comment.GetAllForIssue(repoOwner, repoName, number).Result;
                foreach (var c in comments)
                {
                    reactions.Add((c.CreatedAt, c.User?.Login ?? "ghost", "comment", c.Body));
                }

                // 2 and 3. Reviews and comments on lines of code.
                // Like in FirstResponseExport.cs, GitHub answers "not found" here for some closed pull requests.
                try
                {
                    var reviews = client.PullRequest.Review.GetAll(repoOwner, repoName, number).Result;
                    foreach (var r in reviews)
                    {
                        // Reviews that were never submitted have no real date, so we skip them
                        if (r.SubmittedAt.Year > 2000)
                        {
                            reactions.Add((r.SubmittedAt, r.User?.Login ?? "ghost", $"review: {r.State.StringValue}", r.Body));
                        }
                    }

                    var lineComments = client.PullRequest.ReviewComment.GetAll(repoOwner, repoName, number).Result;
                    foreach (var c in lineComments)
                    {
                        reactions.Add((c.CreatedAt, c.User?.Login ?? "ghost", $"line comment on {c.Path}", c.Body));
                    }
                }
                catch (Exception)
                {
                    writer.WriteLine("(reviews not available for this pull request, only comments are shown)");
                }

                if (reactions.Count == 0)
                {
                    writer.WriteLine("(no comments, reviews or line comments)");
                }

                foreach (var x in reactions.OrderBy(x => x.Time))
                {
                    string text = string.IsNullOrWhiteSpace(x.Text) ? "(no text)" : x.Text.Replace("\r", "");
                    writer.WriteLine($"[{x.Time:yyyy-MM-dd HH:mm}] {x.Who} ({x.Type}):");
                    writer.WriteLine(text);
                    writer.WriteLine();
                }

                writer.WriteLine();
            }
        }

        Console.WriteLine($"Saved to {Path.GetFullPath(outputPath)}");
    }
}