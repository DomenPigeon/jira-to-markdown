using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace JiraToMarkdown;

public static class JiraParser
{
    public static (Dictionary<string, JiraIssue> issues, Dictionary<string, JiraUser> users) Parse(string backupFolder)
    {
        const string r = "[\0-\b\v\f\x0E-\x1F\x26]";
        var s = File.ReadAllText(Path.Combine(backupFolder, "entities.xml"), System.Text.Encoding.UTF8);
        s = Regex.Replace(s, r, "", RegexOptions.Compiled);
        using var sr = new StringReader(s);
        var doc = XDocument.Load(sr);

        var statuses = doc.Descendants("Status").ToDictionary(i => i.Attribute("id")!.Value, i => new JiraStatus(i));
        var issues = doc.Descendants("Issue").ToDictionary(i => i.Attribute("id")!.Value, i => new JiraIssue(i, statuses));
        var users = doc
            .Descendants("User")
            .ToDictionary(i => i.Attribute("lowerUserName")!.Value, i => new JiraUser
            {
                Author = i
            });

        foreach (var a in doc.Descendants("Action"))
        {
            var issueId = a.Attribute("issue")?.Value ?? string.Empty;
            if (issues.TryGetValue(issueId, out var value))
            {
                value.Actions.Add(new JiraAction(a, users));
            }
        }

        foreach (var fa in doc.Descendants("FileAttachment"))
        {
            var issueId = fa.Attribute("issue")?.Value ?? string.Empty;
            if (issues.TryGetValue(issueId, out var value))
            {
                value.FileAttachments.Add(new JiraFileAttachment(fa, users));
            }
        }


        return (issues, users);
    }
}