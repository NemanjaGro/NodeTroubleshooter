using System.CommandLine;
using NodeTroubleshooter.Core;

var dataOption = new Option<string>(
    name: "--data",
    description: "Path to nodespecs.json file",
    getDefaultValue: () => Path.Combine(AppContext.BaseDirectory, "Data", "nodespecs.json"));

var rootCommand = new RootCommand("NodeTroubleshooter - Node Recovery Wizard (FPGA-STP Platform)");
rootCommand.AddGlobalOption(dataOption);

// show command with gen6/gen8 subcommands
var showCommand = new Command("show", "Show node information");
var gen6SubCommand = new Command("gen6", "Show Gen6.1 (C2310) node inventory and facts");
var gen8SubCommand = new Command("gen8", "Show Gen8.1 (C2390) node inventory and facts");

gen6SubCommand.SetHandler((string dataPath) =>
{
    var commands = new Commands(dataPath);
    commands.ShowGeneration("gen6.1");
}, dataOption);

gen8SubCommand.SetHandler((string dataPath) =>
{
    var commands = new Commands(dataPath);
    commands.ShowGeneration("gen8.1");
}, dataOption);

showCommand.AddCommand(gen6SubCommand);
showCommand.AddCommand(gen8SubCommand);
rootCommand.AddCommand(showCommand);

// wizard command
var wizardCommand = new Command("wizard", "Start interactive troubleshooting wizard");
wizardCommand.SetHandler((string dataPath) =>
{
    var commands = new Commands(dataPath);
    commands.RunWizard();
}, dataOption);
rootCommand.AddCommand(wizardCommand);

// symptom command
var symptomCodeArg = new Argument<string>("code", "Symptom code (e.g., PXE_TIMEOUT)");
var symptomCommand = new Command("symptom", "Show guidance for a specific symptom code");
symptomCommand.AddArgument(symptomCodeArg);
symptomCommand.SetHandler((string code, string dataPath) =>
{
    var commands = new Commands(dataPath);
    commands.ShowSymptom(code);
}, symptomCodeArg, dataOption);
rootCommand.AddCommand(symptomCommand);

// diag command
var diagCommand = new Command("diag", "Run diagnostic runbook steps");
var diagTypeArg = new Argument<string>("type", "Diagnostic type (e.g., fpga)");
diagCommand.AddArgument(diagTypeArg);
diagCommand.SetHandler((string type, string dataPath) =>
{
    var commands = new Commands(dataPath);
    commands.RunDiag(type);
}, diagTypeArg, dataOption);
rootCommand.AddCommand(diagCommand);

// Default to wizard if no args
if (args.Length == 0)
{
    var commands = new Commands(Path.Combine(AppContext.BaseDirectory, "Data", "nodespecs.json"));
    commands.RunWizard();
    return 0;
}

return await rootCommand.InvokeAsync(args);
