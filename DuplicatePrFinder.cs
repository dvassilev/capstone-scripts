using System.Text.RegularExpressions;
using Octokit;

namespace CodeExamples;

// Finds issues that got pull requests from two different people within a few days,
// where neither pull request mentions the other one.
// These are candidates for "active in the same place, but not aware of each other".
public static class DuplicatePrFinder
{
    // Core maintainers from Maintainers.md. They review almost every pull request,
    // so if one of them is involved they are very likely aware of the other PR.
    private static readonly HashSet<string> CoreMaintainers = new() { "bashonly", "Grub4K", "coletdjnz" };

    public static async Task RunAsync(GitHubClient client, DateTimeOffset start, DateTimeOffset end, int maxDaysApart = 7)
    {
        var prs = await GetPullRequestsAsync(client, start, end);
        var prNumbers = prs.Select(p => p.Number).ToHashSet();
        Console.WriteLine($"Checked {prs.Count} pull requests.");

        // 1. Group pull requests by the issue numbers they mention in their title or description
        var prsPerIssue = new Dictionary<int, List<PullRequest>>();
        foreach (var pr in prs)
        {
            foreach (int number in MentionedNumbers(pr))
            {
                if (prNumbers.Contains(number))
                    continue; // this number is a pull request, not an issue

                if (!prsPerIssue.ContainsKey(number))
                    prsPerIssue[number] = new List<PullRequest>();
                prsPerIssue[number].Add(pr);
            }
        }

        // 2. For every issue, look at each pair of its pull requests
        foreach (var (issue, list) in prsPerIssue.OrderBy(kv => kv.Key))
        {
            var sorted = list.OrderBy(p => p.CreatedAt).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                for (int j = i + 1; j < sorted.Count; j++)
                {
                    var a = sorted[i];
                    var b = sorted[j];

                    if (a.User.Login == b.User.Login)
                        continue; // same person
                    if (CoreMaintainers.Contains(a.User.Login) || CoreMaintainers.Contains(b.User.Login))
                        continue; // core maintainer involved
                    var daysApart = (b.CreatedAt.UtcDateTime.Date - a.CreatedAt.UtcDateTime.Date).TotalDays;
                    if (daysApart > maxDaysApart)
                        continue; // not around the same time
                    if (Mentions(a, b.Number) || Mentions(b, a.Number))
                        continue; // they know about each other

                    Console.WriteLine($"\nIssue #{issue} ({daysApart} days apart)");
                    Print(a);
                    Print(b);
                }
            }
        }
    }

    // Title + description, without the hidden <!-- ... --> parts of the PR template
    private static string Text(PullRequest pr) =>
        pr.Title + " " + Regex.Replace(pr.Body ?? "", "<!--.*?-->", "", RegexOptions.Singleline);

    private static IEnumerable<int> MentionedNumbers(PullRequest pr) =>
        Regex.Matches(Text(pr), @"#(\d+)").Select(m => int.Parse(m.Groups[1].Value)).Distinct();

    private static bool Mentions(PullRequest pr, int number) =>
        Regex.IsMatch(Text(pr), $@"(#|/pull/){number}\b");

    private static void Print(PullRequest pr)
    {
        var status = pr.MergedAt != null ? "merged" : pr.State.StringValue;
        Console.WriteLine($"  #{pr.Number}  {pr.CreatedAt:yyyy-MM-dd}  {pr.User.Login}  ({status})  {pr.Title}");
        Console.WriteLine($"    {pr.HtmlUrl}");
    }

    // Same as in PrCollector: newest first, stop once we pass the start date
    private static async Task<List<PullRequest>> GetPullRequestsAsync(GitHubClient client, DateTimeOffset start, DateTimeOffset end)
    {
        var request = new PullRequestRequest
        {
            State = ItemStateFilter.All,
            SortProperty = PullRequestSort.Created,
            SortDirection = SortDirection.Descending
        };

        var prs = new List<PullRequest>();
        int page = 1;
        while (true)
        {
            var options = new ApiOptions { PageSize = 100, PageCount = 1, StartPage = page };
            var batch = await client.PullRequest.GetAllForRepository("yt-dlp", "yt-dlp", request, options);
            if (batch.Count == 0)
                break;

            foreach (var pr in batch)
            {
                if (pr.CreatedAt < start)
                    return prs;
                if (pr.CreatedAt < end)
                    prs.Add(pr);
            }
            page++;
        }
        return prs;
    }
}