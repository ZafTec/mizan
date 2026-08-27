extern alias telegram;

using FluentAssertions;
using telegram::Mizan.Telegram.Bot;
using Xunit;

namespace Mizan.Tests.Application;

/// <summary>
/// The converter is regex-heavy and its failure mode is quiet: Telegram
/// rejects a message whose markup does not parse, and the client only logs it.
/// These cover the shapes a model actually produces.
/// </summary>
public class TelegramMarkdownTests
{
    [Fact]
    public void Converts_bold_and_italic()
    {
        TelegramMarkdown.ToHtml("**strong** and *soft*")
            .Should().Be("<b>strong</b> and <i>soft</i>");
    }

    [Fact]
    public void Turns_bullets_into_characters_rather_than_list_tags()
    {
        // Telegram has no <ul>. A bullet that stayed markdown would show the
        // dash; one emitted as a tag would be rejected outright.
        TelegramMarkdown.ToHtml("- one\n- two")
            .Should().Be("• one\n• two");
    }

    [Fact]
    public void Renders_headings_as_bold_since_telegram_has_none()
    {
        TelegramMarkdown.ToHtml("## Today").Should().Be("<b>Today</b>");
    }

    [Fact]
    public void Escapes_markup_the_model_wrote_before_adding_its_own()
    {
        // The whole security property in one case: input tags must not survive.
        TelegramMarkdown.ToHtml("<script>alert(1)</script>")
            .Should().Be("&lt;script&gt;alert(1)&lt;/script&gt;");
    }

    [Fact]
    public void Keeps_code_blocks_verbatim_and_escaped()
    {
        TelegramMarkdown.ToHtml("```\na < b\n```")
            .Should().Be("<pre>a &lt; b</pre>");
    }

    [Fact]
    public void Does_not_treat_emphasis_inside_code_as_markup()
    {
        TelegramMarkdown.ToHtml("`**not bold**`")
            .Should().Be("<code>**not bold**</code>");
    }

    [Fact]
    public void Keeps_http_links_and_drops_other_schemes()
    {
        TelegramMarkdown.ToHtml("[site](https://example.com)")
            .Should().Be("<a href=\"https://example.com\">site</a>");

        TelegramMarkdown.ToHtml("[x](javascript:alert(1))")
            .Should().Be("x");
    }

    [Fact]
    public void Drops_horizontal_rules()
    {
        TelegramMarkdown.ToHtml("a\n---\nb").Should().Be("a\n\nb");
    }

    [Fact]
    public void Leaves_a_bare_asterisk_alone()
    {
        // A model writing "2 * 3" must not produce an unclosed tag.
        TelegramMarkdown.ToHtml("2 * 3 = 6").Should().Be("2 * 3 = 6");
    }
}
