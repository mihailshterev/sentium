using System.Text;
using Sentium.Sandbox.Docker;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Sandbox;

public sealed class BoundedOutputBufferTests
{
    [Fact]
    public async Task KeepsEverything_UnderTheCapacity()
    {
        await using var buffer = new BoundedOutputBuffer(16);

        await buffer.WriteAsync(Encoding.UTF8.GetBytes("hello"), TestContext.Current.CancellationToken);

        Assert.Equal("hello", buffer.ToText("stdout"));
    }

    [Fact]
    public async Task KeepsOnlyTheCapacity_AndCountsTheRest()
    {
        await using var buffer = new BoundedOutputBuffer(4);

        for (var i = 0; i < 1000; i++)
        {
            await buffer.WriteAsync(Encoding.UTF8.GetBytes("abc"), TestContext.Current.CancellationToken);
        }

        Assert.Equal(3000, buffer.TotalBytes);
        Assert.Equal("abca\n[stdout truncated at 4 bytes]", buffer.ToText("stdout"));
    }
}
