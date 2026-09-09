using Avalonia;
using TWXProxy.Core;

if (MTC.UnixAutoDetach.TryRelaunchDetached(args))
    return 0;

// MTC is a GUI application — suppress all Console output so diagnostic
// Console.WriteLine calls in Core do not leak to the terminal.
Console.SetOut(TextWriter.Null);

MTC.MtcStartupFlags.Parse(args);

var prefs = MTC.AppPreferences.Load();
MTC.AppPaths.SetConfiguredProgramDir(prefs.ProgramDirectory);
MTC.AppPaths.EnsureDebugLogDir();

if (MTC.MtcStartupFlags.FailImmediately)
{
    int port = MTC.MtcStartupFlags.JsonRpcPortOverride ?? MTC.AppPreferences.NormalizeJsonRpcPort(prefs.JsonRpcPort);
    if (MTC.MtcStartupFlags.IsPortInUse(prefs.JsonRpcBindAddress, port))
    {
        Console.Error.WriteLine(
            $"MTC: JSON-RPC port {port} is already in use by another MTC instance or process " +
            "(--fail-immediately). Not starting.");
        return 1;
    }
}
GlobalModules.ProgramDir = MTC.AppPaths.ProgramDir;
GlobalModules.PreferPreparedVm = prefs.PreparedVmEnabled;
GlobalModules.EnableVmMetrics = prefs.VmMetricsEnabled;
MTC.AppPaths.EnsureDebugLogDir();
var defaultDebug = new MTC.EmbeddedMtcDebugConfig();
GlobalModules.ConfigureDebugLogging(
    MTC.AppPaths.GetDebugLogPath(),
    defaultDebug.DebugLoggingEnabled,
    defaultDebug.VerboseDebugLogging,
    defaultDebug.TriggerDebugLogging,
    defaultDebug.ScriptTraceDebugLogging,
    defaultDebug.AutoRecorderDebugLogging,
    defaultDebug.VariablePersistenceDebugLogging);
GlobalModules.ConfigureHaggleDebugLogging(
    MTC.AppPaths.GetPortHaggleDebugLogPath(),
    defaultDebug.DebugPortHaggleEnabled,
    MTC.AppPaths.GetPlanetHaggleDebugLogPath(),
    defaultDebug.DebugPlanetHaggleEnabled);
GlobalModules.ConfigureDatabaseCorrectionLogging(
    MTC.AppPaths.GetDatabaseCorrectionLogPath(),
    defaultDebug.DebugLoggingEnabled && defaultDebug.DebugDatabaseChanges);

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    try
    {
        GlobalModules.DebugLog($"[UnhandledException] {e.ExceptionObject}\n");
    }
    catch
    {
    }
};

TaskScheduler.UnobservedTaskException += (_, e) =>
{
    try
    {
        GlobalModules.DebugLog($"[UnobservedTaskException] {e.Exception}\n");
    }
    catch
    {
    }
};

return AppBuilder.Configure<MTC.App>()
    .UsePlatformDetect()
    .WithInterFont()
    .LogToTrace()
    .StartWithClassicDesktopLifetime(args);
