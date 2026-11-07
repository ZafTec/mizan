using FluentAssertions;
using Mizan.Application.Ai;
using Xunit;

namespace Mizan.Tests.Application;

public class StructuredReplyTests
{
    [Theory]
    [InlineData("{\"a\":1}", "{\"a\":1}")]
    [InlineData("  {\"a\":1}\n", "{\"a\":1}")]
    [InlineData("```json\n{\"a\":{\"b\":2}}\n```", "{\"a\":{\"b\":2}}")]
    [InlineData("Here is the plan:\n```\n{\"a\":1}\n```\nEnjoy.", "{\"a\":1}")]
    [InlineData("Sure! {\"a\":\"x\"}", "{\"a\":\"x\"}")]
    [InlineData("No JSON here.", "No JSON here.")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Keeps_the_one_json_object(string? reply, string expected) =>
        StructuredReply.Unwrap(reply).Should().Be(expected);
}
