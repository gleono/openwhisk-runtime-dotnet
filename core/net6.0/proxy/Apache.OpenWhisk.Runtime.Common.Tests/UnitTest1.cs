using Xunit;
using System.Text.Json;

namespace Apache.OpenWhisk.Runtime.Common.Tests;

public class UnitTest1
{
    [Fact]
    public void Test1()
    {
        var initMessage = "{\"value\":{\"name\":\"test_name\"}}";
        var result = JsonSerializer.Deserialize<InitMessage>(initMessage, InitMessage.Options);

        Assert.Equal("test_name", result.Value.Name);
    }
}