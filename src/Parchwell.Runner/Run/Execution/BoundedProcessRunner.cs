using System.Diagnostics;
using System.Text;
using Parchwell.Runner.Run.Contracts;

namespace Parchwell.Runner.Run.Execution;

public sealed class BoundedProcessRunner(TimeProvider clock) : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Timeout <= TimeSpan.Zero || request.Timeout > TimeSpan.FromSeconds(900) || request.MaxOutputBytes is <= 0 or > 16777216)
            throw new ArgumentOutOfRangeException(nameof(request));
        var start = new ProcessStartInfo(request.FileName)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true, WorkingDirectory = request.WorkingDirectory ?? System.Environment.CurrentDirectory
        };
        foreach (string argument in request.Arguments) start.ArgumentList.Add(argument);
        if (request.Environment is not null)
            foreach ((string key, string value) in request.Environment) start.Environment[key] = value;
        using var timeout = new CancellationTokenSource(request.Timeout, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new IOException("process_start_failed");
        Task<string> stdout = ReadAsync(process.StandardOutput, request.MaxOutputBytes, linked.Token);
        Task<string> stderr = ReadAsync(process.StandardError, request.MaxOutputBytes, linked.Token);
        try
        {
            await Task.WhenAll(process.WaitForExitAsync(linked.Token), stdout, stderr).ConfigureAwait(false);
            return new(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            try { await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1), clock, CancellationToken.None).ConfigureAwait(false); }
            catch (InvalidOperationException) { }
            catch (TimeoutException) { }
            throw;
        }
    }

    private static async Task<string> ReadAsync(StreamReader reader, int maximum, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        char[] buffer = new char[4096];
        int bytes = 0;
        while (true)
        {
            int read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0) return text.ToString();
            bytes = checked(bytes + Encoding.UTF8.GetByteCount(buffer, 0, read));
            if (bytes > maximum) throw new InvalidDataException("process_output_limit");
            text.Append(buffer, 0, read);
        }
    }
}
