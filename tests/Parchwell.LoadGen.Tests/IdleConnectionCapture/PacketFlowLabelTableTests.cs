using System.Text;
using Parchwell.LoadGen.IdleConnectionCapture;
using Xunit;

namespace Parchwell.LoadGen.Tests.IdleConnectionCapture;

#pragma warning disable CA1707
// Synthetic identities protect bounded storage and explicit output, not lifecycle validity or capture completeness.
public sealed class PacketFlowLabelTableTests
{
    private static readonly (uint LocalIpv4, ushort LocalPort, uint RemoteIpv4, ushort RemotePort, uint InitialSequence)
        BaseIdentity = (0xc0000214U, 40000, 0xc000020aU, 9093, 100U);

    [Fact]
    public void Given_NewTable_When_AddingAndRepeatingIdentity_Then_RetainsFirstOrdinal()
    {
        var table = new PacketFlowLabelTable();
        Assert.Equal(0, table.Count);
        Assert.Null(table.FailureCode);
        Assert.Equal(64, PacketFlowLabelTable.MaximumFlows);

        Check(table, BaseIdentity, true, 1, 1);
        Check(table, BaseIdentity, true, 1, 1);
        Check(table, BaseIdentity, true, 1, 1);
    }

    [Theory]
    [InlineData(0xc0000215U, 40000, 0xc000020aU, 9093, 100U)]
    [InlineData(0xc0000214U, 40001, 0xc000020aU, 9093, 100U)]
    [InlineData(0xc0000214U, 40000, 0xc000020bU, 9093, 100U)]
    [InlineData(0xc0000214U, 40000, 0xc000020aU, 9094, 100U)]
    [InlineData(0xc0000214U, 40000, 0xc000020aU, 9093, 101U)]
    public void Given_OneChangedIdentityScalar_When_AddingAndRevisiting_Then_KeepsDistinctStableLabels(
        uint localIpv4, ushort localPort, uint remoteIpv4, ushort remotePort, uint initialSequence)
    {
        var table = new PacketFlowLabelTable();
        var variant = (localIpv4, localPort, remoteIpv4, remotePort, initialSequence);

        Check(table, BaseIdentity, true, 1, 1);
        Check(table, variant, true, 2, 2);
        Check(table, BaseIdentity, true, 1, 2);
        Check(table, variant, true, 2, 2);
    }

    [Fact]
    public void Given_ZeroAndMaximumScalars_When_AddingAndRevisiting_Then_UsesExactStorageEquality()
    {
        var table = new PacketFlowLabelTable();
        (uint, ushort, uint, ushort, uint) zero = (0, 0, 0, 0, 0);
        var maximum = (uint.MaxValue, ushort.MaxValue, uint.MaxValue, ushort.MaxValue, uint.MaxValue);

        Check(table, zero, true, 1, 1);
        Check(table, maximum, true, 2, 2);
        Check(table, zero, true, 1, 2);
        Check(table, maximum, true, 2, 2);
    }

    [Fact]
    public void Given_FullTable_When_AddingAnotherIdentity_Then_FailureIsTerminalWithoutEviction()
    {
        var table = new PacketFlowLabelTable();
        var identity = BaseIdentity;
        for (int i = 1; i <= 63; i++)
        {
            identity.LocalPort = (ushort)(40000 + i);
            Check(table, identity, true, i, i);
        }

        identity.LocalPort = 40064;
        Check(table, identity, true, 64, 64);
        identity.LocalPort = 40001;
        Check(table, identity, true, 1, 64);
        identity.LocalPort = 40064;
        Check(table, identity, true, 64, 64);

        identity.LocalPort = 40065;
        Check(table, identity, false, 0, 64, "packet_flow_limit_exceeded");
        identity.LocalPort = 40001;
        Check(table, identity, false, 0, 64, "packet_flow_limit_exceeded");
        identity.LocalPort = 40065;
        Check(table, identity, false, 0, 64, "packet_flow_limit_exceeded");
        Check(table, (0, 0, 0, 0, 0), false, 0, 64, "packet_flow_limit_exceeded");
    }

    [Fact]
    public void Given_IndependentTables_When_AddingSameIdentities_Then_AllocatesLabelsWithinEachInstance()
    {
        var first = new PacketFlowLabelTable();
        var second = new PacketFlowLabelTable();
        var variant = BaseIdentity;
        variant.LocalPort = 40001;

        Check(first, BaseIdentity, true, 1, 1);
        Check(second, BaseIdentity, true, 1, 1);
        Check(first, variant, true, 2, 2);
        Assert.Equal(1, second.Count);
        Assert.Null(second.FailureCode);
        Check(second, variant, true, 2, 2);
        Assert.Equal(2, first.Count);
        Assert.Null(first.FailureCode);
    }

    [Fact]
    public void Given_SyntheticDirectionalHeaders_When_UsingExplicitOpeningIdentity_Then_EncodesOnlyOrdinalEvents()
    {
        var table = new PacketFlowLabelTable();
        byte[] outboundBytes = SyntheticPacketHeaders.Create(flags: (byte)TcpControlFlags.Syn);
        Assert.True(PacketHeaderDecoder.TryDecode(outboundBytes, SyntheticPacketHeaders.RemoteIpv4,
            PacketDirection.Outbound, out var outboundPacket, out var outboundDiagnostic));
        Assert.Equal(PacketHeaderDiagnostic.None, outboundDiagnostic);
        var outbound = Assert.IsType<PrivatePacketHeader>(outboundPacket);
        int outboundFlow = Check(table,
            (outbound.LocalIpv4, outbound.LocalPort, outbound.RemoteIpv4, outbound.RemotePort, outbound.Sequence),
            true, 1, 1);

        Assert.True(PacketSummaryEncoding.TryEncodeEvent(
            new PacketSummaryEvent(outboundFlow, PacketDirection.Outbound, 7, TcpControlFlags.Syn),
            out var outboundEncoded, out var outboundFailure));
        Assert.Null(outboundFailure);
        Assert.Equal("{\"flow\":1,\"direction\":\"outbound\",\"elapsed_ns\":7,\"flags\":[\"SYN\"]}",
            Encoding.UTF8.GetString(outboundEncoded.Span));

        byte[] inboundBytes = SyntheticPacketHeaders.Create(direction: PacketDirection.Inbound,
            flags: (byte)(TcpControlFlags.Syn | TcpControlFlags.Ack));
        Assert.True(PacketHeaderDecoder.TryDecode(inboundBytes, SyntheticPacketHeaders.RemoteIpv4,
            PacketDirection.Inbound, out var inboundPacket, out var inboundDiagnostic));
        Assert.Equal(PacketHeaderDiagnostic.None, inboundDiagnostic);
        var inbound = Assert.IsType<PrivatePacketHeader>(inboundPacket);
        // The opening sequence is supplied explicitly; the table does not select a packet's generation.
        int inboundFlow = Check(table,
            (inbound.LocalIpv4, inbound.LocalPort, inbound.RemoteIpv4, inbound.RemotePort, outbound.Sequence),
            true, 1, 1);

        Assert.True(PacketSummaryEncoding.TryEncodeEvent(
            new PacketSummaryEvent(inboundFlow, PacketDirection.Inbound, 8, TcpControlFlags.Syn | TcpControlFlags.Ack),
            out var inboundEncoded, out var inboundFailure));
        Assert.Null(inboundFailure);
        Assert.Equal("{\"flow\":1,\"direction\":\"inbound\",\"elapsed_ns\":8,\"flags\":[\"SYN\",\"ACK\"]}",
            Encoding.UTF8.GetString(inboundEncoded.Span));
    }

    [Fact]
    public void Given_StoredPrivateIdentity_When_FormattingTable_Then_ReturnsOnlyTypeName()
    {
        var table = new PacketFlowLabelTable();
        Check(table, BaseIdentity, true, 1, 1);
        Assert.Equal(typeof(PacketFlowLabelTable).FullName, table.ToString());
    }

    private static int Check(PacketFlowLabelTable table,
        (uint LocalIpv4, ushort LocalPort, uint RemoteIpv4, ushort RemotePort, uint InitialSequence) identity,
        bool expectedSuccess, int expectedFlow, int expectedCount, string? expectedFailure = null)
    {
        Assert.Equal(expectedSuccess, table.TryGetOrAdd(identity.LocalIpv4, identity.LocalPort,
            identity.RemoteIpv4, identity.RemotePort, identity.InitialSequence, out int flow));
        Assert.Equal(expectedFlow, flow);
        Assert.Equal(expectedCount, table.Count);
        Assert.Equal(expectedFailure, table.FailureCode);
        return flow;
    }
}
#pragma warning restore CA1707
