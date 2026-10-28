using System.Globalization;
using System.Reflection;
using System.Text;
using Confluent.Kafka;
using Parchwell.LoadGen.SingleAction;
using Xunit;

namespace Parchwell.LoadGen.Tests.SingleAction;

#pragma warning disable CA1707
public sealed class SingleActionClientLogBudgetTests
{
    private const string Prefix = "single_action_client_log r1 3|F|N| ";
    private static LogMessage Message(string text, string facility = "F", string name = "N") => new(name, SyslogLevel.Error, facility, text);
    private static void Fill(SingleActionClientLogBudget budget, int count = 160)
    {
        for (int index = 0; index < count; index++) budget.Append(Message(new string('x', 988)));
    }

    [Theory]
    [InlineData(159, 162816)]
    [InlineData(160, 163840)]
    [InlineData(161, 163891)]
    public void Given_RepeatByteBoundary_When_Appending_Then_AcceptsEqualityAndAddsOneSeparateMarker(int count, int expectedBytes)
    {
        var budget = new SingleActionClientLogBudget(1);
        Assert.Equal(35, Encoding.UTF8.GetByteCount(Prefix));
        Fill(budget, count);
        var lines = budget.Seal();
        Assert.Equal(Math.Min(count, 160) + (count > 160 ? 1 : 0), lines.Count);
        Assert.All(lines.Take(Math.Min(count, 160)), line => Assert.Equal(1024, Encoding.UTF8.GetByteCount(line)));
        Assert.Equal(expectedBytes, lines.Sum(Encoding.UTF8.GetByteCount));
        if (count > 160)
        {
            Assert.Equal("single_action_client_log_capped r1 dropped_lines 1\n", lines[^1]);
            Assert.Equal(51, Encoding.UTF8.GetByteCount(lines[^1]));
        }
    }

    [Fact]
    public void Given_FiveRepeats_When_OneBudgetIsExhausted_Then_EachHasIndependentAllowance()
    {
        var budgets = Enumerable.Range(1, 5).Select(repeat => new SingleActionClientLogBudget(repeat)).ToArray();
        foreach (var budget in budgets) Fill(budget, 161);
        for (int index = 0; index < budgets.Length; index++)
        {
            var lines = budgets[index].Seal();
            Assert.Equal(161, lines.Count);
            Assert.Equal(163891, lines.Sum(Encoding.UTF8.GetByteCount));
            Assert.Equal($"single_action_client_log_capped r{index + 1} dropped_lines 1\n", lines[^1]);
        }
        Assert.Equal(819840, 5 * (SingleActionClientLogBudget.MaximumBytes + SingleActionClientLogBudget.MaximumMarkerBytes));
        Assert.Equal(228736, 1048576 - 819840);
    }

    [Theory]
    [InlineData(1023, 1023, 1059)]
    [InlineData(1024, 1024, 1060)]
    [InlineData(1025, 1024, 1060)]
    [InlineData(1000000, 1024, 1060)]
    public void Given_MessageByteBoundary_When_Appending_Then_ClipsMessageWithoutMarkingDrop(int length, int emitted, int lineBytes)
    {
        var budget = new SingleActionClientLogBudget(1);
        budget.Append(Message(new string('x', length)));
        string line = Assert.Single(budget.Seal());
        Assert.Equal(Prefix + new string('x', emitted) + "\n", line);
        Assert.Equal(lineBytes, Encoding.UTF8.GetByteCount(line));
    }

    [Theory]
    [InlineData(1022, "\u00e9z", 1024, "\u00e9")]
    [InlineData(1023, "\u00e9", 1023, "")]
    [InlineData(1020, "\U0001f600", 1024, "\U0001f600")]
    [InlineData(1021, "\U0001f600", 1021, "")]
    [InlineData(1021, "\ud800", 1024, "\ufffd")]
    [InlineData(1022, "\ud800", 1022, "")]
    [InlineData(1021, "\udc00", 1024, "\ufffd")]
    [InlineData(1022, "\udc00", 1022, "")]
    [InlineData(1022, "\rZ", 1024, "\\r")]
    [InlineData(1023, "\nZ", 1023, "")]
    public void Given_ScalarOrEscapeBoundary_When_Clipping_Then_StopsBeforeFirstNonfittingScalar(int prefix, string tail, int bytes, string emittedTail)
    {
        var budget = new SingleActionClientLogBudget(1);
        budget.Append(Message(new string('x', prefix) + tail));
        string line = Assert.Single(budget.Seal());
        Assert.Equal(Prefix + new string('x', prefix) + emittedTail + "\n", line);
        Assert.Equal(35 + bytes + 1, Encoding.UTF8.GetByteCount(line));
    }

    [Theory]
    [InlineData(true, 1021, 1024)]
    [InlineData(true, 1022, 1022)]
    [InlineData(false, 1021, 1024)]
    [InlineData(false, 1022, 1022)]
    public void Given_RuntimeInvalidUtf16_When_Clipping_Then_ReplacesOnlyWholeFittingScalar(bool high, int prefix, int bytes)
    {
        var budget = new SingleActionClientLogBudget(1);
        string invalid = new string('x', prefix) + new string(high ? '\ud800' : '\udc00', 1);
        budget.Append(Message(invalid));
        string line = Assert.Single(budget.Seal());
        Assert.Equal(Prefix + new string('x', prefix) + (prefix == 1021 ? "\ufffd" : "") + "\n", line);
        Assert.Equal(35 + bytes + 1, Encoding.UTF8.GetByteCount(line));
    }

    [Theory]
    [InlineData("facility", 988, true)]
    [InlineData("facility", 989, false)]
    [InlineData("name", 988, true)]
    [InlineData("name", 989, false)]
    public void Given_HeaderAtRemainingAllowance_When_Appending_Then_ReservesFinalLfAndNeverTruncatesHeader(string field, int extra, bool fits)
    {
        var budget = new SingleActionClientLogBudget(1);
        Fill(budget, 159);
        budget.Append(Message("", field == "facility" ? "F" + new string('h', extra) : "F",
            field == "name" ? "N" + new string('h', extra) : "N"));
        var lines = budget.Seal();
        Assert.Equal(160, lines.Count);
        if (fits)
        {
            Assert.Equal(1024, Encoding.UTF8.GetByteCount(lines[^1]));
            Assert.EndsWith("| \n", lines[^1], StringComparison.Ordinal);
        }
        else Assert.Equal("single_action_client_log_capped r1 dropped_lines 1\n", lines[^1]);
    }

    [Fact]
    public void Given_ControlCharactersInAllFields_When_Appending_Then_EscapesAndKeepsOnePhysicalLine()
    {
        var budget = new SingleActionClientLogBudget(1);
        budget.Append(Message("a\r\nb", "F\r\n", "N\n\r"));
        string line = Assert.Single(budget.Seal());
        Assert.Equal("single_action_client_log r1 3|F\\r\\n|N\\n\\r| a\\r\\nb\n", line);
        Assert.Equal(1, line.Count(character => character == '\n'));
        Assert.DoesNotContain('\r', line);
    }

    [Fact]
    public void Given_MillionCharacterFacility_When_Appending_Then_DropsWholeHeaderWithinFiniteBudget()
    {
        var budget = new SingleActionClientLogBudget(1);
        budget.Append(Message("synthetic", new string('F', 1000000)));
        Assert.Equal("single_action_client_log_capped r1 dropped_lines 1\n", Assert.Single(budget.Seal()));
    }

    [Fact]
    public void Given_Only1024BytesRemain_When_1060ByteLineArrives_Then_DropsWholeLineWithoutPartialOutput()
    {
        var budget = new SingleActionClientLogBudget(1);
        Fill(budget, 159);
        budget.Append(Message(new string('x', 1024)));
        var lines = budget.Seal();
        Assert.Equal(160, lines.Count);
        Assert.All(lines.Take(159), line => Assert.Equal(1024, Encoding.UTF8.GetByteCount(line)));
        Assert.Equal("single_action_client_log_capped r1 dropped_lines 1\n", lines[^1]);
        Assert.Equal(162867, lines.Sum(Encoding.UTF8.GetByteCount));
    }

    [Fact]
    public async Task Given_ConcurrentCallbacks_When_Sealed_Then_CountsDropsOnceAndIgnoresLaterCalls()
    {
        var budget = new SingleActionClientLogBudget(1);
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => Fill(budget, 40))));
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => budget.Append(Message("late")))));
        var lines = budget.Seal();
        Assert.Equal(161, lines.Count);
        Assert.Equal("single_action_client_log_capped r1 dropped_lines 4\n", lines[^1]);
        Assert.Single(lines, line => line.StartsWith("single_action_client_log_capped", StringComparison.Ordinal));
        Assert.Empty(budget.Seal());
        budget.Append(Message("after seal"));
        Assert.Empty(budget.Seal());
    }

    [Fact]
    public void Given_DroppedCounterNearMaximum_When_MoreCallbacksArrive_Then_SaturatesAndBoundsMarker()
    {
        var budget = new SingleActionClientLogBudget(1);
        Fill(budget);
        typeof(SingleActionClientLogBudget).GetField("_droppedLines", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(budget, long.MaxValue - 1);
        budget.Append(Message("one"));
        budget.Append(Message("two"));
        var lines = budget.Seal();
        Assert.Equal("single_action_client_log_capped r1 dropped_lines " + long.MaxValue.ToString(CultureInfo.InvariantCulture) + "\n", lines[^1]);
        Assert.Equal(69, Encoding.UTF8.GetByteCount(lines[^1]));
        Assert.True(Encoding.UTF8.GetByteCount(lines[^1]) <= SingleActionClientLogBudget.MaximumMarkerBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_LogsDuringCleanup_When_WorkerFreezes_Then_WritesOnceAfterDisposeAndIgnoresLateCallbacks(bool cleanupThrows)
    {
        SingleActionCaptureTests.Fixture? fixture = null;
        bool cleanupReturned = false;
        using var writer = new RecordingWriter(() => Assert.True(cleanupReturned));
        fixture = new SingleActionCaptureTests.Fixture(writer);
        fixture.Native.OnDispose = () =>
        {
            Task.Run(() =>
            {
                for (int index = 0; index < 161; index++) fixture.Native.Capture.Log(Message(new string('x', 988)));
            }).GetAwaiter().GetResult();
            cleanupReturned = true;
            if (cleanupThrows) throw new IOException("synthetic_cleanup_failure");
        };
        var repeat = await fixture.RunAsync(SingleActionCaptureTests.Action("produce_one", 330000));
        Assert.Equal(1, fixture.Native.DisposeCalls);
        Assert.True(repeat.Terminal);
        Assert.Equal("success", repeat.Result);
        Assert.DoesNotContain("capture_limit", repeat.Diagnostics);
        Assert.Equal(cleanupThrows, repeat.Diagnostics.Any(value => value.StartsWith("client_cleanup_failed:", StringComparison.Ordinal)));
        Assert.Equal(161, writer.Lines.Count);
        Assert.Equal("single_action_client_log_capped r1 dropped_lines 1\n", writer.Lines[^1]);
        fixture.Native.Capture.Log(Message("after cleanup"));
        Assert.Equal(161, writer.Lines.Count);
    }

    private sealed class RecordingWriter(Action beforeWrite) : StringWriter
    {
        internal List<string> Lines { get; } = [];
        public override void Write(string? value)
        {
            beforeWrite();
            Lines.Add(value!);
            base.Write(value);
        }
    }
}
