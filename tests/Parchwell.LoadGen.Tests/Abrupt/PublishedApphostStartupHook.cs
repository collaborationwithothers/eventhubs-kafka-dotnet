using System.Reflection;

// Loaded only by the published-host acceptance test through DOTNET_STARTUP_HOOKS.
internal static class StartupHook
{
    public static void Initialize()
    {
        string? output = Environment.GetEnvironmentVariable("B10Abrupt__OutputRoot");
        if (output is not null && File.Exists(Path.Combine(output, "test-control.json")))
        {
            Parchwell.LoadGen.Tests.Abrupt.B10AbruptProcessHarness.Install(output);
            return;
        }
        Type command = Assembly.Load("Parchwell.LoadGen").GetType("Parchwell.LoadGen.Run.Abrupt.ChildCommand", throwOnError: true)!;
        command.GetMethod("ForRunningApphost", BindingFlags.Static | BindingFlags.NonPublic, Type.EmptyTypes)!.Invoke(null, null);
        Console.WriteLine("b10_abrupt_running_apphost_checked");
        Environment.Exit(0);
    }
}
