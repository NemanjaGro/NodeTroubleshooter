using NodeTroubleshooter.Model;

using System.Text.Json;
using NodeTroubleshooter.Model;

namespace NodeTroubleshooter.Core;

public class TroubleshootSession
{
    public string NodeName { get; }
    public string Generation { get; }
    public string Platform { get; }
    public string CurrentSymptomCode { get; private set; }
    public string CurrentSymptomTitle { get; private set; }
    public int CurrentStage { get; private set; }
    public List<CheckItem> Checks { get; private set; }
    public List<EvidenceItem> Evidence { get; private set; }
    public List<string> RunbookSteps { get; private set; }
    public List<JournalEntry> Journal { get; private set; } = new();
    public List<string> SymptomHistory { get; private set; } = new();
    public List<ActionRecord> Actions { get; private set; } = new();
    public string LogFilePath { get; }
    public string StateFilePath { get; }
    public DateTime StartedAt { get; }
    public bool IsResumed { get; }

    private static readonly string ReportsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NodeTroubleshootingReports");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string GetLogFilePathForNode(string nodeName)
    {
        var safeName = string.Join("_", nodeName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(ReportsFolder, $"{safeName}.txt");
    }

    public static string GetStateFilePathForNode(string nodeName)
    {
        var safeName = string.Join("_", nodeName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(ReportsFolder, $"{safeName}.state.json");
    }

    public static bool HasSavedSession(string nodeName)
    {
        Directory.CreateDirectory(ReportsFolder);
        return File.Exists(GetStateFilePathForNode(nodeName));
    }

    public static TroubleshootSession? TryLoad(string nodeName)
    {
        Directory.CreateDirectory(ReportsFolder);
        var stateFile = GetStateFilePathForNode(nodeName);
        if (!File.Exists(stateFile))
            return null;

        try
        {
            var json = File.ReadAllText(stateFile);
            var state = JsonSerializer.Deserialize<SessionState>(json, JsonOptions);
            if (state == null) return null;
            return new TroubleshootSession(state);
        }
        catch
        {
            return null;
        }
    }

    private TroubleshootSession(SessionState state)
    {
        NodeName = state.NodeName;
        Generation = state.Generation;
        Platform = state.Platform;
        CurrentSymptomCode = state.CurrentSymptomCode;
        CurrentSymptomTitle = state.CurrentSymptomTitle;
        CurrentStage = state.CurrentStage;
        StartedAt = state.StartedAt;
        Checks = state.Checks.Select(c => new CheckItem(c.Description) { Done = c.Done, Notes = c.Notes }).ToList();
        Evidence = state.Evidence.Select(e => new EvidenceItem(e.Description, e.MinStage) { Collected = e.Collected, Notes = e.Notes }).ToList();
        RunbookSteps = state.RunbookSteps;
        Journal = state.Journal.Select(j => new JournalEntry(j.Text, j.Timestamp)).ToList();
        SymptomHistory = state.SymptomHistory.ToList();
        Actions = state.Actions.Select(a => new ActionRecord(a.Id, a.Description, a.Timestamp)).ToList();

        Directory.CreateDirectory(ReportsFolder);
        LogFilePath = GetLogFilePathForNode(NodeName);
        StateFilePath = GetStateFilePathForNode(NodeName);
        IsResumed = true;
    }

    public TroubleshootSession(
        string nodeName,
        string generation,
        string platform,
        string symptomCode,
        string symptomTitle,
        int stage,
        List<string> checks,
        List<EvidenceSpec> evidence,
        List<string> runbookSteps)
    {
        NodeName = nodeName;
        Generation = generation;
        Platform = platform;
        CurrentSymptomCode = symptomCode;
        CurrentSymptomTitle = symptomTitle;
        CurrentStage = stage;
        Checks = checks.Select(c => new CheckItem(c)).ToList();
        Evidence = evidence.Select(e => new EvidenceItem(e.Description, e.MinStage)).ToList();
        RunbookSteps = runbookSteps;
        StartedAt = DateTime.Now;
        SymptomHistory.Add($"{StartedAt:MM/dd} Started with {symptomCode}: {symptomTitle}");

        Directory.CreateDirectory(ReportsFolder);
        LogFilePath = GetLogFilePathForNode(nodeName);
        StateFilePath = GetStateFilePathForNode(nodeName);
        IsResumed = File.Exists(StateFilePath);
    }

    public void AddJournalEntry(string text)
    {
        Journal.Add(new JournalEntry(text));
    }

    public void RecordAction(ActionSpec action)
    {
        Actions.Add(new ActionRecord(action.Id, action.Description));
        AddJournalEntry($"ACTION: {action.Description}");

        if (action.ExpectedSideEffects.Count > 0)
        {
            AddJournalEntry($"  Expected side effects: {string.Join("; ", action.ExpectedSideEffects)}");
        }
    }

    public List<EvidenceItem> GetAvailableEvidence()
    {
        return Evidence.Where(e => e.MinStage <= CurrentStage).ToList();
    }

    public List<EvidenceItem> GetLockedEvidence()
    {
        return Evidence.Where(e => e.MinStage > CurrentStage).ToList();
    }

    public void PivotSymptom(
        string newCode,
        string newTitle,
        int newStage,
        List<string> newChecks,
        List<EvidenceSpec> newEvidence,
        List<string> newRunbookSteps)
    {
        SymptomHistory.Add($"{DateTime.Now:MM/dd} Pivoted from {CurrentSymptomCode} to {newCode}");
        AddJournalEntry($"--- Pivoted investigation: {CurrentSymptomCode} -> {newCode} ({newTitle}) ---");

        foreach (var check in newChecks)
        {
            if (!Checks.Any(c => c.Description.Equals(check, StringComparison.OrdinalIgnoreCase)))
            {
                Checks.Add(new CheckItem(check));
            }
        }

        foreach (var ev in newEvidence)
        {
            if (!Evidence.Any(e => e.Description.Equals(ev.Description, StringComparison.OrdinalIgnoreCase)))
            {
                Evidence.Add(new EvidenceItem(ev.Description, ev.MinStage));
            }
        }

        foreach (var step in newRunbookSteps)
        {
            if (!RunbookSteps.Contains(step))
            {
                RunbookSteps.Add(step);
            }
        }

        CurrentSymptomCode = newCode;
        CurrentSymptomTitle = newTitle;
        if (newStage > CurrentStage)
        {
            CurrentStage = newStage;
        }
    }

    public void SaveProgress()
    {
        bool fileExists = File.Exists(LogFilePath);
        using var writer = new StreamWriter(LogFilePath, append: fileExists);

        if (fileExists)
        {
            writer.WriteLine();
            writer.WriteLine("================================================================");
            writer.WriteLine($"  CONTINUED SESSION - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine("================================================================");
        }
        else
        {
            writer.WriteLine("================================================================");
            writer.WriteLine("  NodeTroubleshooter - Node Recovery Wizard (FPGA-STP Platform)");
            writer.WriteLine("================================================================");
        }
        writer.WriteLine($"  Node Name:   {NodeName}");
        writer.WriteLine($"  Generation:  {Generation} ({Platform})");
        writer.WriteLine($"  Symptom:     {CurrentSymptomCode} - {CurrentSymptomTitle}");
        writer.WriteLine($"  Stage:       {CurrentStage}");
        writer.WriteLine($"  Started:     {StartedAt:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine($"  Last saved:  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine("================================================================");
        writer.WriteLine();

        if (SymptomHistory.Count > 1)
        {
            writer.WriteLine("INVESTIGATION PATH:");
            foreach (var entry in SymptomHistory)
            {
                writer.WriteLine($"  {entry}");
            }
            writer.WriteLine();
        }

        var available = GetAvailableEvidence();
        int completedChecks = Checks.Count(c => c.Done);
        int completedEvidence = available.Count(e => e.Collected);
        writer.WriteLine($"  Progress: {completedChecks}/{Checks.Count} checks done, " +
                          $"{completedEvidence}/{available.Count} evidence collected (stage {CurrentStage})");
        writer.WriteLine();

        writer.WriteLine("CHECKS:");
        for (int i = 0; i < Checks.Count; i++)
        {
            var c = Checks[i];
            var mark = c.Done ? "X" : " ";
            writer.WriteLine($"  [{mark}] {i + 1}. {c.Description}");
            if (!string.IsNullOrWhiteSpace(c.Notes))
            {
                writer.WriteLine($"        Notes: {c.Notes}");
            }
        }
        writer.WriteLine();

        writer.WriteLine("EVIDENCE (available at current stage):");
        for (int i = 0; i < available.Count; i++)
        {
            var e = available[i];
            var mark = e.Collected ? "X" : " ";
            writer.WriteLine($"  [{mark}] {i + 1}. {e.Description}");
            if (!string.IsNullOrWhiteSpace(e.Notes))
            {
                writer.WriteLine($"        Notes: {e.Notes}");
            }
        }

        var locked = GetLockedEvidence();
        if (locked.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine("EVIDENCE (locked - requires later stage):");
            foreach (var e in locked)
            {
                writer.WriteLine($"  [LOCKED] {e.Description} (stage {e.MinStage}+)");
            }
        }

        if (Actions.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine("ACTIONS TAKEN:");
            foreach (var a in Actions)
            {
                writer.WriteLine($"  [{a.Timestamp:MM/dd HH:mm}] {a.Description}");
            }
        }

        if (RunbookSteps.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine("RUNBOOK REFERENCE:");
            for (int i = 0; i < RunbookSteps.Count; i++)
            {
                writer.WriteLine($"  {i + 1}. {RunbookSteps[i]}");
            }
        }

        if (Journal.Count > 0)
        {
            writer.WriteLine();
            writer.WriteLine("INVESTIGATION JOURNAL:");
            foreach (var entry in Journal)
            {
                writer.WriteLine($"  [{entry.Timestamp:MM/dd HH:mm}] {entry.Text}");
            }
        }

        writer.WriteLine();
        writer.WriteLine("================================================================");
        writer.WriteLine("  End of session log");
        writer.WriteLine("================================================================");

        SaveState();
    }

    private void SaveState()
    {
        var state = new SessionState
        {
            NodeName = NodeName,
            Generation = Generation,
            Platform = Platform,
            CurrentSymptomCode = CurrentSymptomCode,
            CurrentSymptomTitle = CurrentSymptomTitle,
            CurrentStage = CurrentStage,
            StartedAt = StartedAt,
            Checks = Checks.Select(c => new CheckState
            {
                Description = c.Description,
                Done = c.Done,
                Notes = c.Notes
            }).ToList(),
            Evidence = Evidence.Select(e => new EvidenceState
            {
                Description = e.Description,
                MinStage = e.MinStage,
                Collected = e.Collected,
                Notes = e.Notes
            }).ToList(),
            RunbookSteps = RunbookSteps.ToList(),
            Journal = Journal.Select(j => new JournalState
            {
                Timestamp = j.Timestamp,
                Text = j.Text
            }).ToList(),
            SymptomHistory = SymptomHistory.ToList(),
            Actions = Actions.Select(a => new ActionState
            {
                Id = a.Id,
                Description = a.Description,
                Timestamp = a.Timestamp
            }).ToList()
        };

        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(StateFilePath, json);
    }

    public void RemoveLastAction()
    {
        if (Actions.Count > 0)
        {
            var removed = Actions[^1];
            Actions.RemoveAt(Actions.Count - 1);
            AddJournalEntry($"UNDO ACTION: {removed.Description}");
        }
    }
}

public class CheckItem
{
    public string Description { get; }
    public bool Done { get; set; }
    public string Notes { get; set; } = string.Empty;

    public CheckItem(string description)
    {
        Description = description;
    }
}

public class EvidenceItem
{
    public string Description { get; }
    public int MinStage { get; }
    public bool Collected { get; set; }
    public string Notes { get; set; } = string.Empty;

    public EvidenceItem(string description, int minStage)
    {
        Description = description;
        MinStage = minStage;
    }
}

public class JournalEntry
{
    public DateTime Timestamp { get; }
    public string Text { get; }

    public JournalEntry(string text)
    {
        Timestamp = DateTime.Now;
        Text = text;
    }

    public JournalEntry(string text, DateTime timestamp)
    {
        Timestamp = timestamp;
        Text = text;
    }
}

public class ActionRecord
{
    public string Id { get; }
    public string Description { get; }
    public DateTime Timestamp { get; }

    public ActionRecord(string id, string description)
    {
        Id = id;
        Description = description;
        Timestamp = DateTime.Now;
    }

    public ActionRecord(string id, string description, DateTime timestamp)
    {
        Id = id;
        Description = description;
        Timestamp = timestamp;
    }
}
