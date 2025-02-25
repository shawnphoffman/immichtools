using ImmichTools.Tools;
using System.CommandLine;
using DotNetEnv;

Env.Load();

string defaultHost = Environment.GetEnvironmentVariable("IMMICH_HOST") ?? "";
string defaultKey = Environment.GetEnvironmentVariable("IMMICH_API_KEY") ?? "";
string defaultDir = Environment.GetEnvironmentVariable("IMMICH_DIRECTORY") ?? "";
bool defaultRecursive = bool.TryParse(Environment.GetEnvironmentVariable("IMMICH_RECURSIVE"), out bool r) && r;
bool defaultCopyMetadata = bool.TryParse(Environment.GetEnvironmentVariable("IMMICH_COPY_METADATA"), out bool m) && m;
bool defaultDryRun = bool.TryParse(Environment.GetEnvironmentVariable("IMMICH_DRY_RUN"), out bool d) && d;

string logHost = defaultHost, logApiKey = defaultKey != "" ? "FROM_ENV" : "EMPTY", logDirectory = defaultDir;
bool logRecursive = defaultRecursive, logCopyMetadata = defaultCopyMetadata, logDryRun = defaultDryRun;

List<string> argList = [.. args];
for (int i = 0; i < argList.Count; i++)
{
	switch (argList[i])
	{
		case "-h":
			logHost = argList[++i];
			break;
		case "-k":
			logApiKey = argList[++i] != "" ? "FROM_ARG" : "EMPTY";
			break;
		case "-r":
			logRecursive = true;
			break;
		case "-m":
			logCopyMetadata = true;
			break;
		case "-d":
			logDryRun = true;
			break;
		default:
			logDirectory ??= argList[i];
			break;
	}
}

Console.WriteLine("Configuration:");
Console.WriteLine($" 🔘 Host: {logHost}");
Console.WriteLine($" 🔘 API Key: {logApiKey}");
Console.WriteLine($" 🔘 Directory: {logDirectory}");
Console.WriteLine($" 🔘 Recursive: {logRecursive}");
Console.WriteLine($" 🔘 Copy Metadata: {logCopyMetadata}");
Console.WriteLine($" 🔘 Dry Run: {logDryRun}");
Console.WriteLine($"");

// COMMANDS
var rootCommand = new RootCommand("Tools for interacting with the Immich API");
var autoStackCommand = new Command("autostack", "Automatically combines assets with matching basename into stacks");

// HOST
var hostOption = new Option<string>("--host", () => defaultHost, "The Immich host to talk to");
hostOption.AddAlias("-h");
rootCommand.AddGlobalOption(hostOption);

// API KEY
var apiKeyOption = new Option<string>("--api-key", () => defaultKey, "The Immich API key");
apiKeyOption.AddAlias("-k");
rootCommand.AddGlobalOption(apiKeyOption);

// DIRECTORY
var directoryArgument = new Argument<string>("directory", () => defaultDir, "The directory in which the assets should be searched");
autoStackCommand.AddArgument(directoryArgument);

// RECURSIVE
var recursiveOption = new Option<bool>("-r", () => defaultRecursive, "Recursively include subdirectories");
autoStackCommand.AddOption(recursiveOption);

// COPY METADATA
var copyMetadataOption = new Option<bool>("-m", () => defaultCopyMetadata, "Copy metadata from raw image to edited versions");
autoStackCommand.AddOption(copyMetadataOption);

// DRY RUN
var dryRunOption = new Option<bool>("-d", () => defaultDryRun, "Print what would be done without actually doing it");
autoStackCommand.AddOption(dryRunOption);

// SETUP
autoStackCommand.SetHandler(AutoStack.RunAsync, hostOption, apiKeyOption, directoryArgument, recursiveOption, copyMetadataOption, dryRunOption);
rootCommand.AddCommand(autoStackCommand);

await rootCommand.InvokeAsync(args);