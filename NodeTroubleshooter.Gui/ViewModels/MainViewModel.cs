using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using NodeTroubleshooter.Core;
using NodeTroubleshooter.Model;

namespace NodeTroubleshooter.Gui.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly SpecDatabase _db;
    private TroubleshootSession? _session;

    private string _nodeName = string.Empty;
    private NodeSpec? _selectedNode;
    private SymptomSpec? _selectedSymptom;
    private string _statusMessage = "Ready. Enter a node name, select generation and symptom.";
    private bool _hasActiveSession;
    private string _journalText = string.Empty;
    private ActionSpec? _selectedAction;
    private string _windowTitle = "NodeTroubleshooter – Node Recovery Wizard (FPGA-STP Platform)";

    public string WindowTitle
    {
        get => _windowTitle;
        private set => SetField(ref _windowTitle, value);
    }

    public string NodeName
    {
        get => _nodeName;
        set
        {
            if (SetField(ref _nodeName, value))
                OnPropertyChanged(nameof(CanStartSession));
        }
    }

    public ObservableCollection<NodeSpec> Nodes { get; } = new();
    public ObservableCollection<SymptomSpec> FilteredSymptoms { get; } = new();
    public ObservableCollection<CheckItemViewModel> Checks { get; } = new();
    public ObservableCollection<EvidenceItemViewModel> Evidence { get; } = new();
    public ObservableCollection<EvidenceItemViewModel> LockedEvidence { get; } = new();
    public ObservableCollection<string> RunbookSteps { get; } = new();
    public ObservableCollection<JournalEntryViewModel> Journal { get; } = new();
    public ObservableCollection<string> SymptomHistory { get; } = new();
    public ObservableCollection<ActionSpec> AvailableActions { get; } = new();
    public ObservableCollection<string> RecentSessions { get; } = new();
    public ObservableCollection<string> Components { get; } = new();

    public NodeSpec? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (SetField(ref _selectedNode, value))
            {
                RefreshSymptoms();
                OnPropertyChanged(nameof(CanStartSession));
            }
        }
    }

    public SymptomSpec? SelectedSymptom
    {
        get => _selectedSymptom;
        set
        {
            if (SetField(ref _selectedSymptom, value))
                OnPropertyChanged(nameof(CanStartSession));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public bool HasActiveSession
    {
        get => _hasActiveSession;
        private set
        {
            if (SetField(ref _hasActiveSession, value))
            {
                OnPropertyChanged(nameof(ShowSetupPanel));
                OnPropertyChanged(nameof(SessionInfo));
            }
        }
    }

    public bool ShowSetupPanel => !_hasActiveSession;

    public string SessionInfo
    {
        get
        {
            if (_session == null) return string.Empty;
            int doneChecks = Checks.Count(c => c.Done);
            int doneEvidence = Evidence.Count(e => e.Collected);
            return $"{_session.NodeName}  |  {_session.CurrentSymptomCode}  |  Stage {_session.CurrentStage}  |  " +
                   $"Checks {doneChecks}/{Checks.Count}  |  Evidence {doneEvidence}/{Evidence.Count}";
        }
    }

    public string JournalText
    {
        get => _journalText;
        set => SetField(ref _journalText, value);
    }

    public ActionSpec? SelectedAction
    {
        get => _selectedAction;
        set => SetField(ref _selectedAction, value);
    }

    public bool CanStartSession =>
        !string.IsNullOrWhiteSpace(NodeName) && SelectedNode != null && SelectedSymptom != null;

    // Commands
    public ICommand StartSessionCommand { get; }
    public ICommand SaveSessionCommand { get; }
    public ICommand ExportLogCommand { get; }
    public ICommand NewSessionCommand { get; }
    public ICommand GoBackCommand { get; }
    public ICommand AddJournalEntryCommand { get; }
    public ICommand RecordActionCommand { get; }
    public ICommand LoadRecentSessionCommand { get; }
    public ICommand DeleteRecentSessionCommand { get; }

    public MainViewModel()
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "nodespecs.json");
        _db = SpecLoader.Load(dataPath);

        foreach (var node in _db.Nodes)
            Nodes.Add(node);

        LoadRecentSessionsList();

        StartSessionCommand = new RelayCommand(StartSession, () => CanStartSession);
        SaveSessionCommand = new RelayCommand(SaveSession, () => HasActiveSession);
        ExportLogCommand = new RelayCommand(ExportLog, () => HasActiveSession);
        NewSessionCommand = new RelayCommand(CloseSession);
        GoBackCommand = new RelayCommand(GoBackToSetup, () => HasActiveSession);
        AddJournalEntryCommand = new RelayCommand(AddJournalEntry, () => HasActiveSession && !string.IsNullOrWhiteSpace(JournalText));
        RecordActionCommand = new RelayCommand(RecordAction, () => HasActiveSession && SelectedAction != null);
        LoadRecentSessionCommand = new RelayCommand(LoadRecentSession);
        DeleteRecentSessionCommand = new RelayCommand(DeleteRecentSession);
    }

    private void RefreshSymptoms()
    {
        FilteredSymptoms.Clear();
        SelectedSymptom = null;
        if (_selectedNode == null) return;

        foreach (var s in _db.Symptoms.Where(s =>
            s.AppliesTo.Contains(_selectedNode.Gen, StringComparer.OrdinalIgnoreCase)))
        {
            FilteredSymptoms.Add(s);
        }
    }

    private void StartSession()
    {
        if (SelectedNode == null || SelectedSymptom == null || string.IsNullOrWhiteSpace(NodeName))
            return;

        // If a saved session already exists for this node, resume it instead of creating a duplicate
        if (TroubleshootSession.HasSavedSession(NodeName))
        {
            var existing = TroubleshootSession.TryLoad(NodeName);
            if (existing != null)
            {
                _session = existing;
                var node = _db.Nodes.FirstOrDefault(n => n.Gen.Equals(existing.Generation, StringComparison.OrdinalIgnoreCase));
                if (node != null) SelectedNode = node;
                LoadSessionIntoView();
                StatusMessage = $"Resumed existing session for {NodeName} — {existing.CurrentSymptomCode}";
                return;
            }
        }

        var allRunbookSteps = new List<string>();
        foreach (var rbId in SelectedSymptom.RunbookIds)
        {
            var rb = _db.Runbooks.FirstOrDefault(r =>
                r.Id.Equals(rbId, StringComparison.OrdinalIgnoreCase));
            if (rb != null)
            {
                allRunbookSteps.Add($"--- {rb.Title} ---");
                allRunbookSteps.AddRange(rb.Steps);
            }
        }

        _session = new TroubleshootSession(
            NodeName,
            SelectedNode.Gen,
            SelectedNode.Platform,
            SelectedSymptom.Code,
            SelectedSymptom.Title,
            SelectedSymptom.Stage,
            SelectedSymptom.Checks,
            SelectedSymptom.Evidence,
            allRunbookSteps);

        _session.SaveProgress();
        LoadSessionIntoView();
        StatusMessage = $"Session started for {NodeName} — {SelectedSymptom.Code}";
    }

    private void LoadSessionIntoView()
    {
        if (_session == null) return;

        Checks.Clear();
        foreach (var c in _session.Checks)
            Checks.Add(new CheckItemViewModel(c.Description, c.Done, c.Notes));

        Evidence.Clear();
        LockedEvidence.Clear();
        foreach (var e in _session.GetAvailableEvidence())
            Evidence.Add(new EvidenceItemViewModel(e.Description, e.MinStage, _session.CurrentStage, e.Collected, e.Notes));
        foreach (var e in _session.GetLockedEvidence())
            LockedEvidence.Add(new EvidenceItemViewModel(e.Description, e.MinStage, _session.CurrentStage));

        RunbookSteps.Clear();
        foreach (var step in _session.RunbookSteps)
            RunbookSteps.Add(step);

        Journal.Clear();
        foreach (var j in _session.Journal)
            Journal.Add(new JournalEntryViewModel(j.Timestamp, j.Text));

        SymptomHistory.Clear();
        foreach (var h in _session.SymptomHistory)
            SymptomHistory.Add(h);

        Components.Clear();
        var node = _db.Nodes.FirstOrDefault(n => n.Gen.Equals(_session.Generation, StringComparison.OrdinalIgnoreCase));
        if (node != null)
        {
            var symptom = _db.Symptoms.FirstOrDefault(s => s.Code.Equals(_session.CurrentSymptomCode, StringComparison.OrdinalIgnoreCase));
            if (symptom != null)
            {
                foreach (var compId in symptom.ComponentIds)
                {
                    var comp = node.Components.FirstOrDefault(c => c.Id.Equals(compId, StringComparison.OrdinalIgnoreCase));
                    Components.Add(comp != null ? $"{comp.Name} [{comp.Category}]" : $"{compId} (not mapped)");
                }
            }
        }

        RefreshAvailableActions();
        HasActiveSession = true;
        UpdateWindowTitle();
        OnPropertyChanged(nameof(SessionInfo));
    }

    private void UpdateWindowTitle()
    {
        WindowTitle = _session != null
            ? $"{_session.NodeName} — {_session.CurrentSymptomCode} | NodeTroubleshooter"
            : "NodeTroubleshooter – Node Recovery Wizard (FPGA-STP Platform)";
    }

    private void RefreshAvailableActions()
    {
        AvailableActions.Clear();
        if (_session == null) return;
        var takenIds = _session.Actions.Select(a => a.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var a in _db.Actions.Where(a => !takenIds.Contains(a.Id)))
            AvailableActions.Add(a);
    }

    private void SyncViewToSession()
    {
        if (_session == null) return;

        for (int i = 0; i < _session.Checks.Count && i < Checks.Count; i++)
        {
            _session.Checks[i].Done = Checks[i].Done;
            _session.Checks[i].Notes = Checks[i].Notes;
        }

        var available = _session.GetAvailableEvidence();
        for (int i = 0; i < available.Count && i < Evidence.Count; i++)
        {
            available[i].Collected = Evidence[i].Collected;
            available[i].Notes = Evidence[i].Notes;
        }
    }

    private void SaveSession()
    {
        if (_session == null) return;
        SyncViewToSession();
        _session.SaveProgress();
        OnPropertyChanged(nameof(SessionInfo));
        LoadRecentSessionsList();
        StatusMessage = $"Session saved — {_session.LogFilePath}";
    }

    private void ExportLog()
    {
        if (_session == null) return;
        SyncViewToSession();
        _session.SaveProgress();

        var dialog = new SaveFileDialog
        {
            FileName = $"{_session.NodeName}_session.txt",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".txt"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                File.Copy(_session.LogFilePath, dialog.FileName, overwrite: true);
                StatusMessage = $"Log exported to {dialog.FileName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
            }
        }
    }

    private void CloseSession()
    {
        if (_session != null)
        {
            SyncViewToSession();
            _session.SaveProgress();
        }

        _session = null;
        HasActiveSession = false;
        Checks.Clear();
        Evidence.Clear();
        LockedEvidence.Clear();
        RunbookSteps.Clear();
        Journal.Clear();
        SymptomHistory.Clear();
        Components.Clear();
        AvailableActions.Clear();
        NodeName = string.Empty;
        SelectedNode = null;
        SelectedSymptom = null;
        JournalText = string.Empty;
        UpdateWindowTitle();
        StatusMessage = "Ready. Enter a node name, select generation and symptom.";
        LoadRecentSessionsList();
    }

    private void GoBackToSetup()
    {
        if (_session != null)
        {
            SyncViewToSession();
            _session.SaveProgress();
        }

        _session = null;
        HasActiveSession = false;
        Checks.Clear();
        Evidence.Clear();
        LockedEvidence.Clear();
        RunbookSteps.Clear();
        Journal.Clear();
        SymptomHistory.Clear();
        Components.Clear();
        AvailableActions.Clear();
        JournalText = string.Empty;
        UpdateWindowTitle();
        StatusMessage = "Session saved. Change your selections and start a new session.";
        LoadRecentSessionsList();
    }

    private void AddJournalEntry()
    {
        if (_session == null || string.IsNullOrWhiteSpace(JournalText)) return;
        SyncViewToSession();
        _session.AddJournalEntry(JournalText);
        Journal.Add(new JournalEntryViewModel(DateTime.Now, JournalText));
        JournalText = string.Empty;
        _session.SaveProgress();
        OnPropertyChanged(nameof(SessionInfo));
        StatusMessage = "Journal entry added.";
    }

    private void RecordAction()
    {
        if (_session == null || SelectedAction == null) return;
        SyncViewToSession();
        _session.RecordAction(SelectedAction);

        Journal.Add(new JournalEntryViewModel(DateTime.Now, $"ACTION: {SelectedAction.Description}"));
        if (SelectedAction.ExpectedSideEffects.Count > 0)
        {
            var effectsText = $"  Expected side effects: {string.Join("; ", SelectedAction.ExpectedSideEffects)}";
            Journal.Add(new JournalEntryViewModel(DateTime.Now, effectsText));
        }

        RefreshAvailableActions();
        SelectedAction = null;
        _session.SaveProgress();
        OnPropertyChanged(nameof(SessionInfo));
        StatusMessage = "Action recorded.";
    }

    private void LoadRecentSession(object? parameter)
    {
        if (parameter is not string sessionNodeName) return;

        var loaded = TroubleshootSession.TryLoad(sessionNodeName);
        if (loaded == null)
        {
            StatusMessage = $"Could not load session for {sessionNodeName}";
            return;
        }

        _session = loaded;
        NodeName = loaded.NodeName;

        var node = _db.Nodes.FirstOrDefault(n => n.Gen.Equals(loaded.Generation, StringComparison.OrdinalIgnoreCase));
        if (node != null) SelectedNode = node;

        LoadSessionIntoView();
        StatusMessage = $"Resumed session for {loaded.NodeName}";
    }

    private void DeleteRecentSession(object? parameter)
    {
        if (parameter is not string sessionNodeName) return;

        try
        {
            var stateFile = TroubleshootSession.GetStateFilePathForNode(sessionNodeName);
            var logFile = TroubleshootSession.GetLogFilePathForNode(sessionNodeName);
            if (File.Exists(stateFile)) File.Delete(stateFile);
            if (File.Exists(logFile)) File.Delete(logFile);
        }
        catch
        {
            // Ignore file-deletion errors
        }

        RecentSessions.Remove(sessionNodeName);
        StatusMessage = $"Session '{sessionNodeName}' removed.";
    }

    private void LoadRecentSessionsList()
    {
        RecentSessions.Clear();
        var reportsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NodeTroubleshootingReports");

        if (!Directory.Exists(reportsFolder)) return;

        foreach (var stateFile in Directory.GetFiles(reportsFolder, "*.state.json")
            .OrderByDescending(File.GetLastWriteTime)
            .Take(10))
        {
            var name = Path.GetFileNameWithoutExtension(stateFile).Replace(".state", "");
            RecentSessions.Add(name);
        }
    }
}
