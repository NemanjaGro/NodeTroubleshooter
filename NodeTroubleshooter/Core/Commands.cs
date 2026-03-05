using NodeTroubleshooter.Model;

using NodeTroubleshooter.Model;

namespace NodeTroubleshooter.Core;

public class Commands
{
    private readonly SpecDatabase _db;

    public Commands(string dataPath)
    {
        _db = SpecLoader.Load(dataPath);
    }

    public void ShowGeneration(string gen)
    {
        var node = _db.Nodes.FirstOrDefault(n => n.Gen.Equals(gen, StringComparison.OrdinalIgnoreCase));
        if (node == null)
        {
            Console.WriteLine($"Error: Generation '{gen}' not found.");
            Console.WriteLine($"Available generations: {string.Join(", ", _db.Nodes.Select(n => n.Gen))}");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("===============================================================");
        Console.WriteLine($"  {node.Label}");
        Console.WriteLine($"  Platform: {node.Platform} | Generation: {node.Gen}");
        Console.WriteLine("===============================================================");

        if (node.Facts.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("KEY FACTS:");
            foreach (var fact in node.Facts)
            {
                Console.WriteLine($"  * {fact}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("COMPONENT INVENTORY:");
        var grouped = node.Components.GroupBy(c => c.Category).OrderBy(g => g.Key);
        foreach (var group in grouped)
        {
            Console.WriteLine($"  [{group.Key}]");
            foreach (var comp in group)
            {
                Console.WriteLine($"    - {comp.Id}: {comp.Name}");
            }
        }
        Console.WriteLine();
    }

    public void RunWizard()
    {
        Console.WriteLine();
        Console.WriteLine("+---------------------------------------------------------------+");
        Console.WriteLine("|   NodeTroubleshooter - Node Recovery Wizard (FPGA-STP Platform) |");
        Console.WriteLine("|   Tracks and guides node-level hardware investigations          |");
        Console.WriteLine("+---------------------------------------------------------------+");
        Console.WriteLine();

        Console.Write("Enter node name: ");
        var nodeName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            Console.WriteLine("  Node name is required.");
            return;
        }
        Console.WriteLine();

        // Check for existing saved session
        var existingSession = TroubleshootSession.TryLoad(nodeName);
        if (existingSession != null)
        {
            int completedChecks = existingSession.Checks.Count(c => c.Done);
            var availableEv = existingSession.GetAvailableEvidence();
            int completedEvidence = availableEv.Count(e => e.Collected);

            Console.WriteLine($"  Existing session found for {nodeName}");
            Console.WriteLine();
            Console.WriteLine($"    Generation:  {existingSession.Generation} ({existingSession.Platform})");
            Console.WriteLine($"    Symptom:     {existingSession.CurrentSymptomTitle} ({existingSession.CurrentSymptomCode})");
            Console.WriteLine($"    Stage:       {existingSession.CurrentStage}");
            Console.WriteLine($"    Progress:    {completedChecks}/{existingSession.Checks.Count} checks, " +
                              $"{completedEvidence}/{availableEv.Count} evidence");
            Console.WriteLine($"    Started:     {existingSession.StartedAt:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
            Console.WriteLine("  [1] Resume this session");
            Console.WriteLine("  [2] Start a new session (overwrites previous)");
            Console.WriteLine("  [0] Exit");
            Console.WriteLine();

            int resumeChoice = ReadChoice(0, 2);
            if (resumeChoice == 0) return;

            if (resumeChoice == 1)
            {
                Console.WriteLine();
                Console.WriteLine($"  Resuming session for {nodeName}...");
                RunInteractiveSession(existingSession);
                return;
            }
            // resumeChoice == 2: fall through to create new session
        }

        Console.WriteLine("Select node generation:");
        for (int i = 0; i < _db.Nodes.Count; i++)
        {
            Console.WriteLine($"  [{i + 1}] {_db.Nodes[i].Gen} - {_db.Nodes[i].Platform}");
        }
        Console.WriteLine("  [0] Exit");
        Console.WriteLine();

        int genChoice = ReadChoice(0, _db.Nodes.Count);
        if (genChoice == 0) return;

        var selectedNode = _db.Nodes[genChoice - 1];
        Console.WriteLine();
        Console.WriteLine($"Selected: {selectedNode.Label}");
        Console.WriteLine();

        var nodeSymptoms = _db.Symptoms
            .Where(s => s.AppliesTo.Contains(selectedNode.Gen, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (nodeSymptoms.Count == 0)
        {
            Console.WriteLine("No symptoms defined for this generation.");
            return;
        }

        Console.WriteLine("Select symptom:");
        Console.WriteLine();
        for (int i = 0; i < nodeSymptoms.Count; i++)
        {
            var s = nodeSymptoms[i];
            var domainTag = s.DomainConfidence.Equals("unknown", StringComparison.OrdinalIgnoreCase)
                ? " [Stage 0 - Domain Unknown]" : "";
            Console.WriteLine($"  [{i + 1}] {s.Title} ({s.Code}){domainTag}");
        }
        Console.WriteLine();
        Console.WriteLine("  [0] Back");
        Console.WriteLine();

        int symptomChoice = ReadChoice(0, nodeSymptoms.Count);
        if (symptomChoice == 0)
        {
            RunWizard();
            return;
        }

        var selectedSymptom = nodeSymptoms[symptomChoice - 1];

        var allRunbookSteps = new List<string>();
        foreach (var rbId in selectedSymptom.RunbookIds)
        {
            var rb = _db.Runbooks.FirstOrDefault(r =>
                r.Id.Equals(rbId, StringComparison.OrdinalIgnoreCase));
            if (rb != null)
            {
                allRunbookSteps.Add($"--- {rb.Title} ---");
                allRunbookSteps.AddRange(rb.Steps);
            }
        }

        var session = new TroubleshootSession(
            nodeName,
            selectedNode.Gen,
            selectedNode.Platform,
            selectedSymptom.Code,
            selectedSymptom.Title,
            selectedSymptom.Stage,
            selectedSymptom.Checks,
            selectedSymptom.Evidence,
            allRunbookSteps);

        session.SaveProgress();

        Console.WriteLine();
        Console.WriteLine($"Session log created: {session.LogFilePath}");

        if (selectedSymptom.ComponentIds.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("MOST-LIKELY COMPONENTS:");
            foreach (var compId in selectedSymptom.ComponentIds)
            {
                var comp = selectedNode.Components.FirstOrDefault(c =>
                    c.Id.Equals(compId, StringComparison.OrdinalIgnoreCase));
                if (comp != null)
                    Console.WriteLine($"  - {comp.Name} [{comp.Category}]");
                else
                    Console.WriteLine($"  - {compId} (not mapped for this generation)");
            }
        }

        if (selectedSymptom.Stage == 0)
        {
            Console.WriteLine();
            Console.WriteLine("  *** STAGE 0: Failure Domain Identification ***");
            Console.WriteLine("  Complete the Stage 0 checks to identify the failure domain.");
            Console.WriteLine("  Do NOT collect photos, run diag, swap parts, or do min boot config yet.");
        }

        RunInteractiveSession(session);
    }

    public void ShowSymptom(string code)
    {
        var symptom = _db.Symptoms.FirstOrDefault(s =>
            s.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

        if (symptom == null)
        {
            Console.WriteLine($"Error: Symptom code '{code}' not found.");
            Console.WriteLine();
            Console.WriteLine("Available symptom codes:");
            foreach (var s in _db.Symptoms)
            {
                Console.WriteLine($"  - {s.Code}: {s.Title}");
            }
            return;
        }

        foreach (var gen in symptom.AppliesTo)
        {
            var node = _db.Nodes.FirstOrDefault(n =>
                n.Gen.Equals(gen, StringComparison.OrdinalIgnoreCase));
            if (node != null)
            {
                PrintSymptomGuidance(symptom, node);
            }
        }
    }

    public void RunDiag(string type)
    {
        var runbook = _db.Runbooks.FirstOrDefault(r =>
            r.Id.Equals(type, StringComparison.OrdinalIgnoreCase) ||
            r.Id.Equals($"diag_{type}", StringComparison.OrdinalIgnoreCase));

        if (runbook == null)
        {
            Console.WriteLine($"Error: Diagnostic '{type}' not found.");
            Console.WriteLine();
            Console.WriteLine("Available diagnostics:");
            foreach (var rb in _db.Runbooks)
            {
                Console.WriteLine($"  - {rb.Id}: {rb.Title}");
            }
            return;
        }

        Console.WriteLine();
        Console.WriteLine("===============================================================");
        Console.WriteLine($"  RUNBOOK: {runbook.Title}");
        Console.WriteLine("===============================================================");
        Console.WriteLine();

        for (int i = 0; i < runbook.Steps.Count; i++)
        {
            Console.WriteLine($"  {i + 1}. {runbook.Steps[i]}");
        }
        Console.WriteLine();
    }

    private void RunInteractiveSession(TroubleshootSession session)
    {
        while (true)
        {
            Console.WriteLine();
            int completedChecks = session.Checks.Count(c => c.Done);
            var available = session.GetAvailableEvidence();
            int completedEvidence = available.Count(e => e.Collected);

            Console.WriteLine("===============================================================");
            Console.WriteLine($"  {session.NodeName} | {session.CurrentSymptomCode} | Stage {session.CurrentStage}");
            Console.WriteLine($"  Checks: {completedChecks}/{session.Checks.Count}  |  Evidence: {completedEvidence}/{available.Count}");
            Console.WriteLine("===============================================================");

            if (session.CurrentStage == 0)
            {
                Console.WriteLine();
                Console.WriteLine("  ** Stage 0 active: complete checks to identify the failure domain, then pivot **");
            }

            Console.WriteLine();
            Console.WriteLine("  [1] Work on Checks");
            Console.WriteLine("  [2] Work on Evidence Collection");
            Console.WriteLine("  [3] View Runbook Steps");
            Console.WriteLine("  [4] View Progress Summary");
            Console.WriteLine("  [5] Journal");
            Console.WriteLine("  [6] Pivot Symptom (investigation changed direction)");
            Console.WriteLine("  [7] Record Action Taken (part swap, reseat, etc.)");
            Console.WriteLine();
            Console.WriteLine("  [0] Save and Exit");
            Console.WriteLine();

            int mainChoice = ReadChoice(0, 7);

            switch (mainChoice)
            {
                case 0:
                    session.SaveProgress();
                    Console.WriteLine();
                    Console.WriteLine($"  Progress saved to: {session.LogFilePath}");
                    Console.WriteLine("  Session ended.");
                    return;
                case 1:
                    WorkOnChecks(session);
                    break;
                case 2:
                    WorkOnEvidence(session);
                    break;
                case 3:
                    ShowRunbookSteps(session);
                    break;
                case 4:
                    ShowProgressSummary(session);
                    break;
                case 5:
                    JournalMenu(session);
                    break;
                case 6:
                    PivotSymptom(session);
                    break;
                case 7:
                    RecordAction(session);
                    break;
            }

            // After completing Stage 0 checks, prompt auto-pivot
            if (session.CurrentStage == 0 &&
                session.Checks.All(c => c.Done))
            {
                PromptAutoPivot(session);
            }
        }
    }

    private static void WorkOnChecks(TroubleshootSession session)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("  CHECKS");
            Console.WriteLine("  -------");
            for (int i = 0; i < session.Checks.Count; i++)
            {
                var c = session.Checks[i];
                var mark = c.Done ? "X" : " ";
                Console.WriteLine($"  [{mark}] {i + 1}. {c.Description}");
                if (!string.IsNullOrWhiteSpace(c.Notes))
                    Console.WriteLine($"        Notes: {c.Notes}");
            }
            Console.WriteLine();
            Console.WriteLine("  Enter a number to update a check, or [0] to go back.");
            Console.WriteLine();

            int choice = ReadChoice(0, session.Checks.Count);
            if (choice == 0)
            {
                session.SaveProgress();
                return;
            }

            var selected = session.Checks[choice - 1];

            Console.WriteLine();
            Console.WriteLine($"  > {selected.Description}");
            Console.WriteLine($"    Status: {(selected.Done ? "DONE" : "PENDING")}");
            if (!string.IsNullOrWhiteSpace(selected.Notes))
                Console.WriteLine($"    Notes:  {selected.Notes}");
            Console.WriteLine();
            Console.WriteLine($"  [1] Mark as {(selected.Done ? "pending" : "done")}");
            Console.WriteLine("  [2] Add / edit notes");
            Console.WriteLine("  [0] Cancel");
            Console.WriteLine();

            int action = ReadChoice(0, 2);
            switch (action)
            {
                case 1:
                    selected.Done = !selected.Done;
                    Console.WriteLine(selected.Done
                        ? $"  -> Marked DONE"
                        : $"  -> Marked PENDING");
                    session.SaveProgress();
                    break;
                case 2:
                    Console.Write("  Enter notes: ");
                    var notes = Console.ReadLine()?.Trim() ?? string.Empty;
                    selected.Notes = notes;
                    Console.WriteLine("  -> Notes saved.");
                    session.SaveProgress();
                    break;
            }
        }
    }

    private static void WorkOnEvidence(TroubleshootSession session)
    {
        while (true)
        {
            var available = session.GetAvailableEvidence();
            var locked = session.GetLockedEvidence();

            Console.WriteLine();
            Console.WriteLine("  EVIDENCE COLLECTION");
            Console.WriteLine("  -------------------");
            for (int i = 0; i < available.Count; i++)
            {
                var e = available[i];
                var mark = e.Collected ? "X" : " ";
                Console.WriteLine($"  [{mark}] {i + 1}. {e.Description}");
                if (!string.IsNullOrWhiteSpace(e.Notes))
                    Console.WriteLine($"        Notes: {e.Notes}");
            }

            if (locked.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Locked (pivot to a specific symptom to unlock):");
                foreach (var e in locked)
                    Console.WriteLine($"    [--] {e.Description} (stage {e.MinStage}+)");
            }

            Console.WriteLine();
            Console.WriteLine("  Enter a number to update an evidence item, or [0] to go back.");
            Console.WriteLine();

            int choice = ReadChoice(0, available.Count);
            if (choice == 0)
            {
                session.SaveProgress();
                return;
            }

            var selected = available[choice - 1];

            Console.WriteLine();
            Console.WriteLine($"  > {selected.Description}");
            Console.WriteLine($"    Status: {(selected.Collected ? "COLLECTED" : "PENDING")}");
            if (!string.IsNullOrWhiteSpace(selected.Notes))
                Console.WriteLine($"    Notes:  {selected.Notes}");
            Console.WriteLine();
            Console.WriteLine($"  [1] Mark as {(selected.Collected ? "pending" : "collected")}");
            Console.WriteLine("  [2] Add / edit notes");
            Console.WriteLine("  [0] Cancel");
            Console.WriteLine();

            int action = ReadChoice(0, 2);
            switch (action)
            {
                case 1:
                    selected.Collected = !selected.Collected;
                    Console.WriteLine(selected.Collected
                        ? $"  -> Marked COLLECTED"
                        : $"  -> Marked PENDING");
                    session.SaveProgress();
                    break;
                case 2:
                    Console.Write("  Enter notes: ");
                    var notes = Console.ReadLine()?.Trim() ?? string.Empty;
                    selected.Notes = notes;
                    Console.WriteLine("  -> Notes saved.");
                    session.SaveProgress();
                    break;
            }
        }
    }

    private static void ShowRunbookSteps(TroubleshootSession session)
    {
        Console.WriteLine();
        if (session.RunbookSteps.Count == 0)
        {
            Console.WriteLine("  No runbook steps associated with this symptom.");
            if (session.CurrentStage == 0)
            {
                Console.WriteLine("  (Runbooks unlock after pivoting from Stage 0)");
            }
            return;
        }

        Console.WriteLine("RUNBOOK STEPS:");
        foreach (var step in session.RunbookSteps)
        {
            Console.WriteLine($"  {step}");
        }
    }

    private static void ShowProgressSummary(TroubleshootSession session)
    {
        var available = session.GetAvailableEvidence();
        int completedChecks = session.Checks.Count(c => c.Done);
        int completedEvidence = available.Count(e => e.Collected);

        Console.WriteLine();
        Console.WriteLine("===============================================================");
        Console.WriteLine("  PROGRESS SUMMARY");
        Console.WriteLine("===============================================================");
        Console.WriteLine($"  Node:     {session.NodeName}");
        Console.WriteLine($"  Symptom:  {session.CurrentSymptomCode} - {session.CurrentSymptomTitle}");
        Console.WriteLine($"  Stage:    {session.CurrentStage}");
        Console.WriteLine($"  Started:  {session.StartedAt:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"  Log file: {session.LogFilePath}");

        if (session.SymptomHistory.Count > 1)
        {
            Console.WriteLine();
            Console.WriteLine("  INVESTIGATION PATH:");
            foreach (var entry in session.SymptomHistory)
            {
                Console.WriteLine($"    {entry}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  Checks:   {completedChecks}/{session.Checks.Count}");
        for (int i = 0; i < session.Checks.Count; i++)
        {
            var c = session.Checks[i];
            var mark = c.Done ? "X" : " ";
            Console.WriteLine($"    [{mark}] {c.Description}");
        }
        Console.WriteLine();
        Console.WriteLine($"  Evidence: {completedEvidence}/{available.Count} (stage {session.CurrentStage})");
        for (int i = 0; i < available.Count; i++)
        {
            var e = available[i];
            var mark = e.Collected ? "X" : " ";
            Console.WriteLine($"    [{mark}] {e.Description}");
        }

        if (session.Actions.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"  Actions taken: {session.Actions.Count}");
            foreach (var a in session.Actions)
            {
                Console.WriteLine($"    [{a.Timestamp:MM/dd}] {a.Description}");
            }
        }

        if (session.Journal.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"  Journal entries: {session.Journal.Count}");
        }

        if (completedChecks == session.Checks.Count && completedEvidence == available.Count)
        {
            Console.WriteLine();
            Console.WriteLine("  ** All checks and available evidence complete! **");
        }
    }

    private static void JournalMenu(TroubleshootSession session)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("  JOURNAL");
            Console.WriteLine("  -------");
            Console.WriteLine($"  {session.Journal.Count} entries so far.");
            Console.WriteLine();
            Console.WriteLine("  [1] Add a journal entry");
            Console.WriteLine("  [2] View journal entries");
            Console.WriteLine();
            Console.WriteLine("  [0] Back");
            Console.WriteLine();

            int choice = ReadChoice(0, 2);
            if (choice == 0) return;

            switch (choice)
            {
                case 1:
                    Console.WriteLine();
                    Console.WriteLine("  What did you do / find / schedule?");
                    Console.Write("  > ");
                    var text = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        session.AddJournalEntry(text);
                        Console.WriteLine("  -> Journal entry added.");
                        session.SaveProgress();
                    }
                    else
                    {
                        Console.WriteLine("  Empty entry, skipped.");
                    }
                    break;
                case 2:
                    Console.WriteLine();
                    if (session.Journal.Count == 0)
                    {
                        Console.WriteLine("  No journal entries yet.");
                    }
                    else
                    {
                        Console.WriteLine("  INVESTIGATION JOURNAL:");
                        Console.WriteLine();
                        foreach (var entry in session.Journal)
                        {
                            Console.WriteLine($"    [{entry.Timestamp:MM/dd HH:mm}] {entry.Text}");
                        }
                    }
                    break;
            }
        }
    }

    private void PromptAutoPivot(TroubleshootSession session)
    {
        var rules = _db.PivotRules
            .Where(r => r.FromSymptom.Equals(session.CurrentSymptomCode, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (rules.Count == 0) return;

        Console.WriteLine();
        Console.WriteLine("===============================================================");
        Console.WriteLine("  STAGE 0 CHECKS COMPLETE - Identify the failure domain:");
        Console.WriteLine("===============================================================");
        Console.WriteLine();
        Console.WriteLine("  Based on your findings, what did you observe?");
        Console.WriteLine();
        for (int i = 0; i < rules.Count; i++)
        {
            Console.WriteLine($"  [{i + 1}] {rules[i].Finding}");
        }
        Console.WriteLine();
        Console.WriteLine("  [0] Stay in NODE_D_STATE (need more investigation)");
        Console.WriteLine();

        int choice = ReadChoice(0, rules.Count);
        if (choice == 0) return;

        var selectedRule = rules[choice - 1];
        ExecutePivot(session, selectedRule.ToSymptom);
    }

    private void PivotSymptom(TroubleshootSession session)
    {
        var selectedNode = _db.Nodes.FirstOrDefault(n =>
            n.Gen.Equals(session.Generation, StringComparison.OrdinalIgnoreCase));
        if (selectedNode == null) return;

        var nodeSymptoms = _db.Symptoms
            .Where(s => s.AppliesTo.Contains(selectedNode.Gen, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Console.WriteLine();
        Console.WriteLine("  PIVOT SYMPTOM");
        Console.WriteLine("  -------------");
        Console.WriteLine($"  Current: {session.CurrentSymptomTitle} ({session.CurrentSymptomCode}, Stage {session.CurrentStage})");
        Console.WriteLine("  Note: existing checks, evidence, journal, and actions are preserved.");

        // Show auto-pivot suggestions if in NODE_D_STATE
        var rules = _db.PivotRules
            .Where(r => r.FromSymptom.Equals(session.CurrentSymptomCode, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (rules.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  Suggested pivots based on findings:");
            foreach (var rule in rules)
                Console.WriteLine($"    {rule.Finding} -> {rule.ToSymptom}");
        }

        Console.WriteLine();
        Console.WriteLine("  Select symptom to pivot to:");
        Console.WriteLine();
        for (int i = 0; i < nodeSymptoms.Count; i++)
        {
            var s = nodeSymptoms[i];
            var marker = s.Code.Equals(session.CurrentSymptomCode, StringComparison.OrdinalIgnoreCase)
                ? " (current)" : "";
            Console.WriteLine($"  [{i + 1}] {s.Title} ({s.Code}){marker}");
        }
        Console.WriteLine();
        Console.WriteLine("  [0] Cancel");
        Console.WriteLine();

        int choice = ReadChoice(0, nodeSymptoms.Count);
        if (choice == 0) return;

        ExecutePivot(session, nodeSymptoms[choice - 1].Code);
    }

    private void ExecutePivot(TroubleshootSession session, string targetCode)
    {
        var selectedNode = _db.Nodes.FirstOrDefault(n =>
            n.Gen.Equals(session.Generation, StringComparison.OrdinalIgnoreCase));
        if (selectedNode == null) return;

        var newSymptom = _db.Symptoms.FirstOrDefault(s =>
            s.Code.Equals(targetCode, StringComparison.OrdinalIgnoreCase) &&
            s.AppliesTo.Contains(session.Generation, StringComparer.OrdinalIgnoreCase));
        if (newSymptom == null)
        {
            Console.WriteLine($"  Error: Symptom '{targetCode}' not found for {session.Generation}.");
            return;
        }

        var newRunbookSteps = new List<string>();
        foreach (var rbId in newSymptom.RunbookIds)
        {
            var rb = _db.Runbooks.FirstOrDefault(r =>
                r.Id.Equals(rbId, StringComparison.OrdinalIgnoreCase));
            if (rb != null)
            {
                newRunbookSteps.Add($"--- {rb.Title} ---");
                newRunbookSteps.AddRange(rb.Steps);
            }
        }

        // Check for expected side effects from recent actions
        CheckExpectedSideEffects(session, newSymptom.Code);

        session.PivotSymptom(
            newSymptom.Code,
            newSymptom.Title,
            newSymptom.Stage,
            newSymptom.Checks,
            newSymptom.Evidence,
            newRunbookSteps);

        Console.WriteLine();
        Console.WriteLine($"  Pivoted to: {newSymptom.Code} - {newSymptom.Title} (Stage {newSymptom.Stage})");

        if (newSymptom.ComponentIds.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  COMPONENTS TO INVESTIGATE:");
            foreach (var compId in newSymptom.ComponentIds)
            {
                var comp = selectedNode.Components.FirstOrDefault(c =>
                    c.Id.Equals(compId, StringComparison.OrdinalIgnoreCase));
                if (comp != null)
                    Console.WriteLine($"    - {comp.Name} [{comp.Category}]");
                else
                    Console.WriteLine($"    - {compId} (not mapped for this generation)");
            }
        }

        session.SaveProgress();
        Console.WriteLine();
        Console.WriteLine("  New checks and evidence merged. Evidence stage gate updated.");
    }

    private void CheckExpectedSideEffects(TroubleshootSession session, string newSymptomCode)
    {
        if (session.Actions.Count == 0) return;

        var relevantEffects = new List<string>();

        foreach (var actionRecord in session.Actions)
        {
            var actionSpec = _db.Actions.FirstOrDefault(a =>
                a.Id.Equals(actionRecord.Id, StringComparison.OrdinalIgnoreCase));
            if (actionSpec == null) continue;

            foreach (var effect in actionSpec.ExpectedSideEffects)
            {
                bool relevant = newSymptomCode switch
                {
                    "PXE_TIMEOUT" => effect.Contains("PXE", StringComparison.OrdinalIgnoreCase) ||
                                     effect.Contains("boot", StringComparison.OrdinalIgnoreCase) ||
                                     effect.Contains("MAC", StringComparison.OrdinalIgnoreCase) ||
                                     effect.Contains("DHCP", StringComparison.OrdinalIgnoreCase),
                    "NVME_MISSING" => effect.Contains("NVMe", StringComparison.OrdinalIgnoreCase) ||
                                     effect.Contains("boot cycle", StringComparison.OrdinalIgnoreCase),
                    "FPGA_MISSING_MULTIPLE" => effect.Contains("FPGA", StringComparison.OrdinalIgnoreCase) ||
                                               effect.Contains("enumerate", StringComparison.OrdinalIgnoreCase) ||
                                               effect.Contains("riser", StringComparison.OrdinalIgnoreCase) ||
                                               effect.Contains("PLX", StringComparison.OrdinalIgnoreCase),
                    "NIC_DOWN" or "DAC_CABLE" => effect.Contains("NIC", StringComparison.OrdinalIgnoreCase) ||
                                  effect.Contains("link", StringComparison.OrdinalIgnoreCase) ||
                                  effect.Contains("MAC", StringComparison.OrdinalIgnoreCase) ||
                                  effect.Contains("DAC", StringComparison.OrdinalIgnoreCase),
                    "POWER_P1" => effect.Contains("power", StringComparison.OrdinalIgnoreCase) ||
                                  effect.Contains("PSU", StringComparison.OrdinalIgnoreCase) ||
                                  effect.Contains("PMBus", StringComparison.OrdinalIgnoreCase),
                    "MOTHERBOARD" => effect.Contains("board", StringComparison.OrdinalIgnoreCase) ||
                                     effect.Contains("BMC", StringComparison.OrdinalIgnoreCase) ||
                                     effect.Contains("BIOS", StringComparison.OrdinalIgnoreCase) ||
                                     effect.Contains("TPM", StringComparison.OrdinalIgnoreCase),
                    "BIOS_BMC" => effect.Contains("BIOS", StringComparison.OrdinalIgnoreCase) ||
                                  effect.Contains("BMC", StringComparison.OrdinalIgnoreCase) ||
                                  effect.Contains("firmware", StringComparison.OrdinalIgnoreCase),
                    "CPU" => effect.Contains("CPU", StringComparison.OrdinalIgnoreCase) ||
                             effect.Contains("re-training", StringComparison.OrdinalIgnoreCase),
                    "DIMM" => effect.Contains("memory", StringComparison.OrdinalIgnoreCase) ||
                              effect.Contains("DIMM", StringComparison.OrdinalIgnoreCase) ||
                              effect.Contains("re-training", StringComparison.OrdinalIgnoreCase),
                    "RISER_CARD" => effect.Contains("riser", StringComparison.OrdinalIgnoreCase) ||
                                    effect.Contains("PLX", StringComparison.OrdinalIgnoreCase) ||
                                    effect.Contains("enumerate", StringComparison.OrdinalIgnoreCase),
                    "OCULINK" => effect.Contains("Oculink", StringComparison.OrdinalIgnoreCase) ||
                                 effect.Contains("NIC", StringComparison.OrdinalIgnoreCase) ||
                                 effect.Contains("link", StringComparison.OrdinalIgnoreCase),
                    _ => false
                };

                if (relevant)
                {
                    relevantEffects.Add($"After {actionRecord.Description}: {effect}");
                }
            }
        }

        if (relevantEffects.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  *** EXPECTED SIDE EFFECTS from recent actions: ***");
            foreach (var effect in relevantEffects)
            {
                Console.WriteLine($"    ! {effect}");
            }
            Console.WriteLine("  (These symptoms may be expected, not a new failure)");
        }
    }

    private void RecordAction(TroubleshootSession session)
    {
        while (true)
        {
            var takenIds = session.Actions.Select(a => a.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var availableActions = _db.Actions.Where(a => !takenIds.Contains(a.Id)).ToList();

            Console.WriteLine();
            Console.WriteLine("  RECORD AN ACTION");
            Console.WriteLine("  -----------------");

            if (session.Actions.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Actions already recorded:");
                foreach (var a in session.Actions)
                    Console.WriteLine($"    [{a.Timestamp:MM/dd HH:mm}] {a.Description}");
            }

            Console.WriteLine();
            if (availableActions.Count == 0)
            {
                Console.WriteLine("  All actions have already been recorded.");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine("  Available actions:");
                for (int i = 0; i < availableActions.Count; i++)
                    Console.WriteLine($"  [{i + 1}] {availableActions[i].Description}");
                Console.WriteLine();
            }

            int maxOption = availableActions.Count;
            if (session.Actions.Count > 0)
            {
                maxOption = availableActions.Count + 1;
                Console.WriteLine($"  [{availableActions.Count + 1}] Undo last recorded action");
            }

            Console.WriteLine("  [0] Back");
            Console.WriteLine();

            int choice = ReadChoice(0, maxOption);
            if (choice == 0) return;

            // Undo option
            if (session.Actions.Count > 0 && choice == availableActions.Count + 1)
            {
                var last = session.Actions[^1];
                Console.WriteLine($"  Undo: {last.Description} [{last.Timestamp:MM/dd HH:mm}]?");
                Console.WriteLine("  [1] Yes, undo");
                Console.WriteLine("  [0] Cancel");
                Console.WriteLine();
                int confirm = ReadChoice(0, 1);
                if (confirm == 1)
                {
                    session.RemoveLastAction();
                    Console.WriteLine("  -> Action undone.");
                    session.SaveProgress();
                }
                continue;
            }

            var selectedAction = availableActions[choice - 1];
            session.RecordAction(selectedAction);

            Console.WriteLine();
            Console.WriteLine($"  -> Recorded: {selectedAction.Description}");

            if (selectedAction.ExpectedSideEffects.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  Expected side effects (do not treat as new failures):");
                foreach (var effect in selectedAction.ExpectedSideEffects)
                    Console.WriteLine($"    ! {effect}");
            }

            session.SaveProgress();
        }
    }

    private void PrintSymptomGuidance(SymptomSpec symptom, NodeSpec node)
    {
        Console.WriteLine();
        Console.WriteLine("===============================================================");
        Console.WriteLine($"  SYMPTOM: {symptom.Code} (Stage {symptom.Stage})");
        Console.WriteLine($"  {symptom.Title}");
        Console.WriteLine($"  Generation: {node.Gen} ({node.Platform})");
        Console.WriteLine("===============================================================");

        if (symptom.ComponentIds.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("MOST-LIKELY COMPONENTS:");
            foreach (var compId in symptom.ComponentIds)
            {
                var comp = node.Components.FirstOrDefault(c =>
                    c.Id.Equals(compId, StringComparison.OrdinalIgnoreCase));
                if (comp != null)
                    Console.WriteLine($"  - {comp.Name} [{comp.Category}]");
                else
                    Console.WriteLine($"  - {compId} (not mapped for this generation)");
            }
        }

        Console.WriteLine();
        Console.WriteLine("CHECKS:");
        foreach (var check in symptom.Checks)
        {
            Console.WriteLine($"  [ ] {check}");
        }

        var stageEvidence = symptom.Evidence.Where(e => e.MinStage <= symptom.Stage).ToList();
        var lockedEvidence = symptom.Evidence.Where(e => e.MinStage > symptom.Stage).ToList();

        Console.WriteLine();
        Console.WriteLine("EVIDENCE TO COLLECT:");
        foreach (var evidence in stageEvidence)
        {
            Console.WriteLine($"  >> {evidence.Description}");
        }

        if (lockedEvidence.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("EVIDENCE (locked - later stage):");
            foreach (var evidence in lockedEvidence)
            {
                Console.WriteLine($"  [--] {evidence.Description} (stage {evidence.MinStage}+)");
            }
        }

        if (symptom.RunbookIds.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("RUNBOOK SHORTCUTS:");
            foreach (var rbId in symptom.RunbookIds)
            {
                var runbook = _db.Runbooks.FirstOrDefault(r =>
                    r.Id.Equals(rbId, StringComparison.OrdinalIgnoreCase));
                if (runbook != null)
                {
                    Console.WriteLine($"  -> {runbook.Title} (run: diag {rbId.Replace("diag_", "")})");
                }
            }
        }

        // Show pivot rules if this is Stage 0
        var rules = _db.PivotRules
            .Where(r => r.FromSymptom.Equals(symptom.Code, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (rules.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("AUTO-PIVOT RULES (after Stage 0 triage):");
            foreach (var rule in rules)
            {
                Console.WriteLine($"  {rule.Finding} -> {rule.ToSymptom}");
            }
        }

        Console.WriteLine();
    }

    private static int ReadChoice(int min, int max)
    {
        while (true)
        {
            Console.Write("  > ");
            var input = Console.ReadLine();
            if (int.TryParse(input, out int choice) && choice >= min && choice <= max)
            {
                return choice;
            }
            Console.WriteLine($"  Invalid input. Please enter a number between {min} and {max}.");
        }
    }
}
