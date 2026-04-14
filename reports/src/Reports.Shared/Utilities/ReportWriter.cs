namespace Reports.Shared.Utilities;

public sealed class ReportWriter
{
    private readonly string _basePath;

    public ReportWriter(string basePath = "analysis")
    {
        _basePath = Path.GetFullPath(basePath);
        if (!Directory.Exists(_basePath))
            Directory.CreateDirectory(_basePath);
    }

    public string CreateReport(string fileName, string title, string storyId, string objective, string scope)
    {
        var path = Path.Combine(_basePath, fileName);

        var content = $"""
            # {title}

            ## Story ID
            {storyId}

            ## Objective
            {objective}

            ## Scope
            {scope}

            ## Execution Log
            _(created at {DateTimeOffset.UtcNow:u})_

            ## Files Created
            _(to be updated)_

            ## Files Modified
            _(to be updated)_

            ## Endpoints Added
            _(to be updated)_

            ## Build / Run / Validation Status
            _(to be updated)_

            ## Issues Encountered
            _(none yet)_

            ## Decisions Made
            _(to be updated)_

            ## Known Gaps / Not Yet Implemented
            _(to be updated)_

            ## Final Summary
            _(to be completed)_
            """;

        File.WriteAllText(path, content);
        return path;
    }

    public void AppendSection(string fileName, string sectionTitle, string content)
    {
        var path = Path.Combine(_basePath, fileName);
        var text = $"\n\n## {sectionTitle}\n{content}\n";
        File.AppendAllText(path, text);
    }

    public void AppendStep(string fileName, string stepName, string status, string notes)
    {
        var path = Path.Combine(_basePath, fileName);
        var text = $"\n### {stepName}\n- **Status**: {status}\n- **Notes**: {notes}\n- **Timestamp**: {DateTimeOffset.UtcNow:u}\n";
        File.AppendAllText(path, text);
    }

    public void WriteFinalSummary(string fileName, string summary)
    {
        AppendSection(fileName, "Final Summary (Appended)", summary);
    }
}
