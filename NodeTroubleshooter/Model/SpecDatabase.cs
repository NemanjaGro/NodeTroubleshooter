namespace NodeTroubleshooter.Model;

public class SpecDatabase
{
    public List<NodeSpec> Nodes { get; set; } = new();
    public List<SymptomSpec> Symptoms { get; set; } = new();
    public List<RunbookSpec> Runbooks { get; set; } = new();
    public List<PivotRuleSpec> PivotRules { get; set; } = new();
    public List<ActionSpec> Actions { get; set; } = new();
}

public class NodeSpec
{
    public string Gen { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public List<string> Facts { get; set; } = new();
    public List<ComponentSpec> Components { get; set; } = new();
}

public class ComponentSpec
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class SymptomSpec
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Stage { get; set; }
    public string DomainConfidence { get; set; } = "known";
    public List<string> AppliesTo { get; set; } = new();
    public List<string> ComponentIds { get; set; } = new();
    public List<string> Checks { get; set; } = new();
    public List<EvidenceSpec> Evidence { get; set; } = new();
    public List<string> RunbookIds { get; set; } = new();
}

public class EvidenceSpec
{
    public string Description { get; set; } = string.Empty;
    public int MinStage { get; set; }
}

public class RunbookSpec
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = new();
}

public class PivotRuleSpec
{
    public string FromSymptom { get; set; } = string.Empty;
    public string Finding { get; set; } = string.Empty;
    public string ToSymptom { get; set; } = string.Empty;
}

public class ActionSpec
{
    public string Id { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> ExpectedSideEffects { get; set; } = new();
}
