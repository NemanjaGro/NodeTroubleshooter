
namespace NodeTroubleshooter.Model;

public class SessionState
{
    public string NodeName { get; set; } = string.Empty;
    public string Generation { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string CurrentSymptomCode { get; set; } = string.Empty;
    public string CurrentSymptomTitle { get; set; } = string.Empty;
    public int CurrentStage { get; set; }
    public DateTime StartedAt { get; set; }
    public List<CheckState> Checks { get; set; } = new();
    public List<EvidenceState> Evidence { get; set; } = new();
    public List<string> RunbookSteps { get; set; } = new();
    public List<JournalState> Journal { get; set; } = new();
    public List<string> SymptomHistory { get; set; } = new();
    public List<ActionState> Actions { get; set; } = new();
}

public class CheckState
{
    public string Description { get; set; } = string.Empty;
    public bool Done { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class EvidenceState
{
    public string Description { get; set; } = string.Empty;
    public int MinStage { get; set; }
    public bool Collected { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class JournalState
{
    public DateTime Timestamp { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class ActionState
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
