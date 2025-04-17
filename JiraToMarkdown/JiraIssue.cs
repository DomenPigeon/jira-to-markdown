using System.Xml.Linq;

namespace JiraToMarkdown;

public class JiraIssue
{
    public JiraIssue(XElement issue, Dictionary<string, JiraStatus> statuses)
    {
        Issue = issue;
        Status = statuses[issue.Attribute("status")!.Value];
    }

    public string ProjectKey => Issue.Attribute("projectKey")!.Value;
    public string Number => Issue.Attribute("number")!.Value;
    public string IssueNr => $"{ProjectKey}-{Number}";
    public string Summary => Issue.Attribute("summary")!.Value;
    public string? Description => Issue.Descendants("description").FirstOrDefault()?.Value;

    public string EpicName => Issue.Attribute("epicName")?.Value ?? string.Empty;

    public DateTime Created => (DateTime)Issue.Attribute("created")!;

    public XElement Issue { get; set; }
    public JiraStatus Status { get; }

    public List<JiraAction> Actions { get; set; } = [];

    public List<JiraFileAttachment> FileAttachments { get; set; } = [];

    public bool IsMatch(string keyword)
    {
        return Description?.Contains(keyword, StringComparison.InvariantCultureIgnoreCase) ?? Summary.Contains(keyword, StringComparison.InvariantCultureIgnoreCase)
            || Actions.Any(a => a.Body?.Contains(keyword, StringComparison.InvariantCultureIgnoreCase) ?? false);
    }

    public bool IsClosed => Status.Name is "Closed" or "Resolved" or "Obsolete" or "Done";
}

public class JiraStatus
{
    public JiraStatus(XElement status)
    {
        Status = status;
    }

    public string Id => Status.Attribute("id")!.Value;
    public string Name => Status.Attribute("name")!.Value;

    public XElement Status { get; set; }
}

public class JiraAction
{
    public JiraAction(XElement action, Dictionary<string, JiraUser> users)
    {
        Action = action;
        Author = users[action.Attribute("author")!.Value];
    }

    public string Type => Action.Attribute("type")!.Value;
    public DateTime Created => (DateTime)Action.Attribute("created")!;

    public string Body => Action.Descendants("body").FirstOrDefault()?.Value ?? Action.Attribute("body")?.Value!;

    public JiraUser Author { get; }

    public XElement Action { get; }
}

public class JiraFileAttachment
{
    public JiraFileAttachment(XElement fileAttachment, Dictionary<string, JiraUser> users)
    {
        FileAttachment = fileAttachment;
        Author = users[fileAttachment.Attribute("author")!.Value];
    }

    public string Id => FileAttachment.Attribute("id")!.Value;
    public DateTime Created => (DateTime)FileAttachment.Attribute("created")!;
    public string Mimetype => FileAttachment.Attribute("mimetype")!.Value;
    public string Thumbnailable => FileAttachment.Attribute("thumbnailable")!.Value;
    public string Filename => FileAttachment.Attribute("filename")!.Value;

    public JiraUser Author { get; }
    public XElement FileAttachment { get; }
}

public class JiraUser
{
    public string UserName => Author.Attribute("userName")!.Value;
    public string DisplayName => Author.Attribute("displayName")!.Value;
    public string EmailAddress => Author.Attribute("emailAddress")!.Value;
    public string ExternalId => Author.Attribute("externalId")!.Value;

    public required XElement Author { get; set; }

    public override string ToString() => $"[[@{DisplayName}]]";
}