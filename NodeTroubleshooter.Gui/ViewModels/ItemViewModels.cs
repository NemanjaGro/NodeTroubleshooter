namespace NodeTroubleshooter.Gui.ViewModels;

public class CheckItemViewModel : ViewModelBase
{
    private bool _done;
    private string _notes = string.Empty;

    public string Description { get; }

    public bool Done
    {
        get => _done;
        set => SetField(ref _done, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetField(ref _notes, value);
    }

    public CheckItemViewModel(string description, bool done = false, string notes = "")
    {
        Description = description;
        _done = done;
        _notes = notes;
    }
}

public class EvidenceItemViewModel : ViewModelBase
{
    private bool _collected;
    private string _notes = string.Empty;

    public string Description { get; }
    public int MinStage { get; }
    public bool IsLocked { get; }

    public bool Collected
    {
        get => _collected;
        set => SetField(ref _collected, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetField(ref _notes, value);
    }

    public EvidenceItemViewModel(string description, int minStage, int currentStage, bool collected = false, string notes = "")
    {
        Description = description;
        MinStage = minStage;
        IsLocked = minStage > currentStage;
        _collected = collected;
        _notes = notes;
    }
}

public class JournalEntryViewModel
{
    public DateTime Timestamp { get; }
    public string Text { get; }
    public string Display => $"[{Timestamp:MM/dd HH:mm}] {Text}";

    public JournalEntryViewModel(DateTime timestamp, string text)
    {
        Timestamp = timestamp;
        Text = text;
    }
}
