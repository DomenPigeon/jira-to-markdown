using System.Text;
using System.Text.RegularExpressions;

namespace JiraToMarkdown;

public static class MarkdownGenerator
{
    public static void Generate(JiraIssue issue, Dictionary<string, JiraUser> users, string outputDirectory, string sourceDir)
    {
        // Create directory structure if it doesn't exist
        Directory.CreateDirectory(outputDirectory);
        var attachmentsDir = Path.Combine(outputDirectory, "attachments");
        Directory.CreateDirectory(attachmentsDir);
        Directory.CreateDirectory(Path.Combine(outputDirectory, "open"));
        Directory.CreateDirectory(Path.Combine(outputDirectory, "closed"));

        // Create the Markdown file with issue name
        var fileName = $"{issue.IssueNr} {SanitizeFileName(issue.Summary)}.md";
        var markdownFilePath = Path.Combine(outputDirectory, issue.IsClosed ? "closed" : "open", fileName);

        // Build markdown content
        var sb = new StringBuilder();

        // Add obsidian header
        var issueStatusTag = issue.IsClosed ? "closed" : "open";
        sb.AppendLine("---");
        sb.AppendLine($"time: \"{issue.Created:yyyy-MM-dd HH:mm}\"");
        sb.AppendLine("type: note");
        sb.AppendLine("tags:");
        sb.AppendLine("- TwinGrid");
        sb.AppendLine("- issue");
        sb.AppendLine($"- {issueStatusTag}");
        sb.AppendLine("resource:");
        sb.AppendLine("---");

        // Add header
        sb.AppendLine($"# {issue.Summary}");
        sb.AppendLine();
        sb.AppendLine($"**Issue**: {issue.IssueNr}");
        sb.AppendLine($"**Created**: {issue.Created:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // Add description if available
        if (!string.IsNullOrEmpty(issue.Description))
        {
            sb.AppendLine("## Description");
            sb.AppendLine();

            // Process the description to handle attachments
            var processedDescription = ProcessDescription(sourceDir, issue.Description, issue, attachmentsDir, issue.ProjectKey, users);
            sb.AppendLine(processedDescription);
            sb.AppendLine();
        }

        // Add file attachments section if any
        if (issue.FileAttachments.Count > 0)
        {
            sb.AppendLine("## Attachments");
            sb.AppendLine();

            foreach (var attachment in issue.FileAttachments)
            {
                // Copy the attachment to the output directory
                var attachmentPath = CopyAttachment(sourceDir, attachment, attachmentsDir, issue.ProjectKey, issue.IssueNr);

                // Add a link to the attachment in the markdown
                sb.AppendLine(
                    $"- [{attachment.Filename}](./../attachments/{Path.GetFileName(attachmentPath)}) - *{attachment.Created:yyyy-MM-dd HH:mm}* by {attachment.Author}");
            }

            sb.AppendLine();
        }

        // Add actions/comments section if any
        if (issue.Actions.Count > 0)
        {
            sb.AppendLine("## Comments");
            sb.AppendLine();

            foreach (var action in issue.Actions.OrderBy(a => a.Created))
            {
                if (action.Type == "comment")
                {
                    sb.AppendLine($"### {action.Author} - {action.Created:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine();

                    // Process the comment body to handle attachments
                    var processedBody = ProcessDescription(sourceDir, action.Body, issue, attachmentsDir, issue.ProjectKey, users);
                    sb.AppendLine(processedBody);
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine($"### {action.Type} by {action.Author} - {action.Created:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine();

                    if (!string.IsNullOrEmpty(action.Body))
                    {
                        var processedBody = ProcessDescription(sourceDir, action.Body, issue, attachmentsDir, issue.ProjectKey, users);
                        sb.AppendLine(processedBody);
                        sb.AppendLine();
                    }
                }
            }
        }

        // Write markdown to file
        File.WriteAllText(markdownFilePath, sb.ToString());
    }

    private static string ProcessDescription(string sourceDir, string description, JiraIssue issue, string attachmentsDir, string projectKey, Dictionary<string, JiraUser> users)
    {
        if (string.IsNullOrEmpty(description))
        {
            return string.Empty;
        }

        description = description.Replace("#", "-");

        // Regular expression to find Jira attachment references
        // Typically looks like: !filename.png|thumbnail! or !filename.png!
        var attachmentRegex = new Regex(@"!([^|!]+)(?:\|[^!]+)?!");

        var result = attachmentRegex.Replace(description, match =>
        {
            var filename = match.Groups[1].Value.Trim();

            // Try to find the attachment in the issue
            var attachment = issue.FileAttachments.FirstOrDefault(a => a.Filename == filename);

            if (attachment != null)
            {
                // Copy the attachment and get the new path
                var newPath = CopyAttachment(sourceDir, attachment, attachmentsDir, projectKey, issue.IssueNr);

                // Return markdown image link
                return $"![{filename}](./../attachments/{Path.GetFileName(newPath)})";
            }

            // If not found, return the original text
            return match.Value;
        });

        // Process authors
        var regex = new Regex(@"\[~accountid:(.*)\]");
        var externalId = regex.Match(description).Groups[1].Value;
        var author = users.FirstOrDefault(u => u.Value.ExternalId == externalId).Value;

        if (author != null)
        {
            result = result.Replace($"[~accountid:{externalId}]", author.ToString());
        }

        return result;
    }

    private static string CopyAttachment(string sourceDir, JiraFileAttachment attachment, string attachmentsDir, string projectKey, string issueNr)
    {
        // Source file path in Jira backup
        var sourceFilePath = Path.Combine(
            sourceDir,
            "data",
            "attachments",
            projectKey,
            "10000", // Assuming this is always 10000 as per example
            issueNr,
            attachment.Id);

        // If source file doesn't exist, try to find it
        if (!File.Exists(sourceFilePath))
        {
            // Look through possible parent directories
            var parentDir = Path.GetDirectoryName(Path.GetDirectoryName(sourceFilePath));
            if (parentDir != null)
            {
                var possiblePaths = Directory.GetFiles(parentDir, attachment.Id, SearchOption.AllDirectories);
                if (possiblePaths.Length > 0)
                {
                    sourceFilePath = possiblePaths[0];
                }
            }
        }

        // Destination file path with proper filename
        var destFileName = SanitizeFileName(attachment.Filename);
        var destFilePath = Path.Combine(attachmentsDir, destFileName);

        // If destination file already exists, append a number to make it unique
        if (File.Exists(destFilePath))
        {
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(destFileName);
            var extension = Path.GetExtension(destFileName);
            var counter = 1;

            do
            {
                destFilePath = Path.Combine(attachmentsDir, $"{fileNameWithoutExt}_{counter}{extension}");
                counter++;
            } while (File.Exists(destFilePath));
        }

        // Copy the file if it exists
        if (File.Exists(sourceFilePath))
        {
            File.Copy(sourceFilePath, destFilePath, true);
        }

        return destFilePath;
    }

    private static string SanitizeFileName(string fileName)
    {
        // Replace invalid characters with underscore
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(fileName.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
    }
}