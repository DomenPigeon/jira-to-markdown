using JiraToMarkdown;

const string sourceDir = @"C:\Users\domen\Downloads\jira-backup";
var (issues, users) = JiraParser.Parse(sourceDir);

Console.WriteLine($"Issues: {issues.Count}");
Console.WriteLine($"Users: {users.Count}");

var allIssueStatuses = issues.Values
    .Select(i => i.Status.Name)
    .Distinct()
    .ToList();

Console.WriteLine($"All issue statuses: {string.Join(", ", allIssueStatuses)}");

const string markdownOutputDir = @"C:\Workspace\knowledge_hub\Issues\TwinGrid";
foreach (var (_, issue) in issues)
{
    if (issue.Description is null)
    {
        continue;
    }

    MarkdownGenerator.Generate(issue, users, markdownOutputDir, sourceDir);
}