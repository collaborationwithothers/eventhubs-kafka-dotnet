#pragma warning disable CA1707 // Binding engineering standards require Given_When_Then names.
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Parchwell.LoadGen.Run.Abrupt;
using Xunit;
using static Parchwell.LoadGen.Run.Abrupt.B10AbruptDiagnostics;

namespace Parchwell.LoadGen.Tests.Abrupt;

public sealed class B10AbruptDiagnosticsTests
{
    [Fact]
    public void Given_NeverThrownException_When_ChainIsCaptured_Then_ThrowerIsNull()
    {
        var chain = CaptureChain(new IOException("synthetic"));
        var entry = Assert.Single(chain.Entries);
        Assert.Equal("System.IO.IOException", entry.ExceptionType);
        Assert.Null(entry.ThrowerType);
        Assert.Null(entry.ThrowerMethod);
        Assert.False(chain.Truncated);
    }

    [Fact]
    public void Given_ThrownOuterAndInner_When_ChainIsCaptured_Then_ExactThrowersSurvive()
    {
        var exception = Assert.Throws<InvalidOperationException>(ThrowOuter);
        var chain = CaptureChain(exception);
        Assert.Collection(chain.Entries,
            entry => { Assert.Equal("System.InvalidOperationException", entry.ExceptionType); Assert.Equal("Parchwell.LoadGen.Tests.Abrupt.B10AbruptDiagnosticsTests", entry.ThrowerType); Assert.Equal("ThrowOuter", entry.ThrowerMethod); },
            entry => { Assert.Equal("System.IO.IOException", entry.ExceptionType); Assert.Equal("Parchwell.LoadGen.Tests.Abrupt.B10AbruptDiagnosticsTests", entry.ThrowerType); Assert.Equal("ThrowInner", entry.ThrowerMethod); });
        Assert.False(chain.Truncated);
    }

    [Theory]
    [InlineData(7, 7, false)]
    [InlineData(8, 8, false)]
    [InlineData(9, 8, true)]
    public void Given_LinearExceptionChain_When_Captured_Then_DepthIsBounded(int length, int expected, bool truncated)
    {
        Exception? exception = null;
        for (int index = 0; index < length; index++) exception = new IOException("synthetic", exception);
        var chain = CaptureChain(exception!);
        Assert.Equal(expected, chain.Entries.Count);
        Assert.Equal(truncated, chain.Truncated);
        Assert.All(chain.Entries, entry => Assert.Equal("System.IO.IOException", entry.ExceptionType));
    }

    [Fact]
    public void Given_AggregateWithTwoInners_When_Captured_Then_OnlyInnerExceptionIsWalked()
    {
        var chain = CaptureChain(new AggregateException(new IOException("first"), new InvalidOperationException("second")));
        Assert.Equal(["System.AggregateException", "System.IO.IOException"], chain.Entries.Select(entry => entry.ExceptionType));
        Assert.False(chain.Truncated);
    }

    [Fact]
    public void Given_AttachedFacts_When_Described_Then_CallerSiteAndFirstDuplicateSurvive()
    {
        var (exception, line) = (B10AbruptDiagnostics.Attach(new InvalidDataException("b10_abrupt_guard_failed"), [Fact(B10AbruptFact.SelectedCount, 0), Fact(B10AbruptFact.SelectedCount, 5), Fact(B10AbruptFact.Delivered, true)]), CurrentLine());
        Assert.IsType<InvalidDataException>(exception);
        Assert.Equal("b10_abrupt_guard_failed", exception.Message);
        var detail = DescribeElement(exception).GetProperty("detail");
        Assert.Equal("B10AbruptDiagnosticsTests.cs", detail.GetProperty("file").GetString());
        Assert.Equal(nameof(Given_AttachedFacts_When_Described_Then_CallerSiteAndFirstDuplicateSurvive), detail.GetProperty("member").GetString());
        Assert.Equal(line, detail.GetProperty("line").GetInt32());
        Assert.Equal("{\"selected_count\":0,\"delivered\":1}", detail.GetProperty("facts").GetRawText());
    }

    [Fact]
    public void Given_ExcessFactsAndFractionalMilliseconds_When_Attached_Then_FirstThirtyFiveAndWholeMillisecondsRemain()
    {
        B10AbruptFactValue[] facts = Enum.GetValues<B10AbruptFact>().Take(36).Select(key => Fact(key, (long)key)).ToArray();
        var detail = DescribeElement(Attach(new IOException("synthetic"), facts)).GetProperty("detail");
        Assert.Equal(35, detail.GetProperty("facts").EnumerateObject().Count());
        Assert.Equal(facts.Take(35).Select(fact => JsonNamingPolicy.SnakeCaseLower.ConvertName(fact.Key.ToString())), detail.GetProperty("facts").EnumerateObject().Select(property => property.Name));
        Assert.Equal(1999, Fact(B10AbruptFact.ElapsedMilliseconds, TimeSpan.FromTicks(19_999_999)).Value);
    }

    [Fact]
    public void Given_TwoAttachedExceptions_When_Described_Then_OuterAndInnerSitesRemain()
    {
        var inner = Attach(new IOException("synthetic"), file: "/private/inner.cs", member: "Inner", line: 2);
        var outer = Attach(new InvalidOperationException("synthetic", inner), file: "/private/outer.cs", member: "Outer", line: 3);
        var described = DescribeElement(outer);
        Assert.Equal("outer.cs", described.GetProperty("detail").GetProperty("file").GetString());
        Assert.Equal("Outer", described.GetProperty("detail").GetProperty("member").GetString());
        Assert.Equal(3, described.GetProperty("detail").GetProperty("line").GetInt32());
        Assert.Equal("inner.cs", described.GetProperty("inner_detail").GetProperty("file").GetString());
        Assert.Equal("Inner", described.GetProperty("inner_detail").GetProperty("member").GetString());
        Assert.Equal(2, described.GetProperty("inner_detail").GetProperty("line").GetInt32());
    }

    [Fact]
    public void Given_PrivateExceptionContent_When_Described_Then_OnlyMetadataIsSerialized()
    {
        var exception = new IOException("private-message /private/path private-token");
        exception.Data["private-key"] = "private-value";
        var described = DescribeElement(exception);
        B10AbruptWire.ExactMembers(described, "exception_chain", "detail", "inner_detail", "reduced");
        foreach (string value in new[] { "private-message", "/private/path", "private-token", "private-key", "private-value" })
            Assert.DoesNotContain(value, described.GetRawText(), StringComparison.Ordinal);
        Assert.Null(described.GetProperty("detail").GetString());
        Assert.Null(described.GetProperty("inner_detail").GetString());
        Assert.False(described.GetProperty("reduced").GetBoolean());
    }

    [Fact]
    public void Given_OversizeTypeChain_When_PlainSuffixAndBoundedAreBuilt_Then_ChainIsReduced()
    {
        var huge = HugeChain();
        Assert.True(huge.GetType().FullName!.Length > 9000);
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(Describe(huge), B10AbruptWire.JsonOptions).Length > B10AbruptWire.MaximumBodyBytes);
        string suffix = PlainSuffix(huge);
        Assert.StartsWith(" diagnostic=", suffix, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(suffix[" diagnostic=".Length..]);
        var bounded = B10AbruptWire.Element(Bounded(huge));
        foreach (var value in new[] { document.RootElement, bounded })
        {
            Assert.True(value.GetProperty("reduced").GetBoolean());
            Assert.Equal(JsonValueKind.Null, value.GetProperty("exception_chain").ValueKind);
            Assert.True(Encoding.UTF8.GetByteCount(value.GetRawText()) <= B10AbruptWire.MaximumBodyBytes);
        }
    }

    [Fact]
    public void Given_HeldStagesAndManualClock_When_MarkedAndFlushed_Then_OrderAndCapsAreExact()
    {
        var clock = new ManualClock();
        using var writer = new StringWriter();
        var timeline = new B10AbruptTimeline(clock, "supervisor", writer);
        clock.Advance(TimeSpan.FromMilliseconds(1500));
        timeline.Hold(B10AbruptStage.StopRequested, Fact(B10AbruptFact.Arm, 1));
        Assert.Equal("", writer.ToString());
        timeline.Mark(B10AbruptStage.CheckpointWritten, Fact(B10AbruptFact.Arm, 1), Fact(B10AbruptFact.SelectedCount, 0));
        Assert.Equal("b10_abrupt_stage {\"role\":\"supervisor\",\"stage\":\"stop_requested\",\"elapsed_ms\":1500,\"ticks\":15000000,\"facts\":{\"arm\":1}}" + Environment.NewLine +
            "b10_abrupt_stage {\"role\":\"supervisor\",\"stage\":\"checkpoint_written\",\"elapsed_ms\":1500,\"ticks\":15000000,\"facts\":{\"arm\":1,\"selected_count\":0}}" + Environment.NewLine, writer.ToString());
        for (int index = 0; index < 48; index++) timeline.Hold(B10AbruptStage.StopRequested);
        timeline.Flush();
        Assert.Equal(48, writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
        var snapshot = B10AbruptWire.Element(timeline.Snapshot());
        B10AbruptWire.ExactMembers(snapshot, "role", "start_utc", "origin_ticks", "timestamp_frequency", "entries", "dropped", "omitted_for_size");
        Assert.Equal("supervisor", snapshot.GetProperty("role").GetString());
        Assert.Equal(0, snapshot.GetProperty("origin_ticks").GetInt64());
        Assert.Equal(10000000, snapshot.GetProperty("timestamp_frequency").GetInt64());
        Assert.Equal(48, snapshot.GetProperty("entries").GetArrayLength());
        Assert.Equal(2, snapshot.GetProperty("dropped").GetInt64());
        Assert.False(snapshot.GetProperty("omitted_for_size").GetBoolean());
        foreach (var entry in snapshot.GetProperty("entries").EnumerateArray()) B10AbruptWire.ExactMembers(entry, "stage", "elapsed_ms", "ticks", "facts");
    }

    [Fact]
    public void Given_ClosedFactAndStageNames_When_SnakeCased_Then_NamesAreUniqueAndBounded()
    {
        var facts = Enum.GetNames<B10AbruptFact>().Select(JsonNamingPolicy.SnakeCaseLower.ConvertName).ToArray();
        var stages = Enum.GetNames<B10AbruptStage>().Select(JsonNamingPolicy.SnakeCaseLower.ConvertName).ToArray();
        Assert.Equal(facts.Length, facts.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(stages.Length, stages.Distinct(StringComparer.Ordinal).Count());
        Assert.All(facts.Concat(stages), name => Assert.InRange(name.Length, 1, 40));
        Assert.Contains("ack_sent", stages);
    }

    internal static void AssertPlainReport(string report, string expectedPrefix)
    {
        Assert.EndsWith(Environment.NewLine, report, StringComparison.Ordinal);
        string line = report[..^Environment.NewLine.Length];
        Assert.DoesNotContain('\r', line);
        Assert.DoesNotContain('\n', line);
        int separator = line.IndexOf(" diagnostic=", StringComparison.Ordinal);
        Assert.True(separator >= 0);
        Assert.Equal(expectedPrefix, line[..separator]);
        using var document = JsonDocument.Parse(line[(separator + " diagnostic=".Length)..]);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        B10AbruptWire.ExactMembers(document.RootElement, "exception_chain", "detail", "inner_detail", "reduced");
        Assert.False(document.RootElement.GetProperty("reduced").GetBoolean());
    }

    internal static Exception HugeChain(int depth = 8)
    {
        Type nested = typeof(int);
        for (int index = 0; index < 100; index++) nested = typeof(List<>).MakeGenericType(nested);
        Type exceptionType = typeof(SyntheticException<>).MakeGenericType(nested);
        Exception? chain = null;
        for (int index = 0; index < depth; index++) chain = (Exception)Activator.CreateInstance(exceptionType, [chain])!;
        return chain!;
    }

    private static JsonElement DescribeElement(Exception exception) => B10AbruptWire.Element(Describe(exception));
    private static int CurrentLine([CallerLineNumber] int line = 0) => line;
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInner() => throw new IOException("synthetic");
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowOuter()
    {
        try { ThrowInner(); }
        catch (IOException exception) { throw new InvalidOperationException("synthetic", exception); }
    }

    private sealed class ManualClock : TimeProvider
    {
        private long ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref ticks);
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.Zero).AddTicks(GetTimestamp());
        internal void Advance(TimeSpan elapsed) => Interlocked.Add(ref ticks, elapsed.Ticks);
    }
}

internal sealed class SyntheticException<T>(Exception? inner) : Exception("synthetic", inner);
