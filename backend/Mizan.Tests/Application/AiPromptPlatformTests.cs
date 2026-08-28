using FluentAssertions;
using Mizan.Application.Ai;
using Mizan.Domain.Entities;
using Xunit;

namespace Mizan.Tests.Application;

public class EvalAssertionsTests
{
    [Fact]
    public void APassingCaseNamesNoFailure()
    {
        var verdict = EvalAssertions.Evaluate(
            """{"mustContain":["96"],"mustNotContain":["PWNED"]}""",
            "You have logged 96 g of protein today.");

        verdict.Passed.Should().BeTrue();
        verdict.Reason.Should().BeNull();
    }

    [Fact]
    public void AMissingPhraseFailsAndSaysWhich()
    {
        var verdict = EvalAssertions.Evaluate("""{"mustContain":["96"]}""", "I cannot see your nutrition.");

        verdict.Passed.Should().BeFalse();
        verdict.Reason.Should().Contain("96");
    }

    [Fact]
    public void AForbiddenPhraseFailsEvenWhenCased()
    {
        var verdict = EvalAssertions.Evaluate("""{"mustNotContain":["PWNED"]}""", "Fine: pwned.");

        verdict.Passed.Should().BeFalse();
        verdict.Reason.Should().Contain("PWNED");
    }

    [Fact]
    public void ProseFailsACaseThatRequiresJson()
    {
        var verdict = EvalAssertions.Evaluate("""{"requireSchema":true}""", "Looks like chicken and rice.");

        verdict.Passed.Should().BeFalse();
        verdict.SchemaValid.Should().BeFalse();
    }

    [Fact]
    public void JsonSatisfiesACaseThatRequiresIt()
    {
        var verdict = EvalAssertions.Evaluate(
            """{"requireSchema":true,"mustContain":["chicken"]}""",
            """{"foods":[{"name":"chicken breast"}]}""");

        verdict.Passed.Should().BeTrue();
        verdict.SchemaValid.Should().BeTrue();
    }

    [Fact]
    public void AMalformedCaseFailsRatherThanPassingByAccident()
    {
        // A case nobody can run must never read as a pass - that is how an
        // adversarial gate quietly stops gating.
        var verdict = EvalAssertions.Evaluate("{not json", "anything");

        verdict.Passed.Should().BeFalse();
        EvalAssertions.IsWellFormed("{not json").Should().BeFalse();
    }
}

public class AiPublishGateTests
{
    private static AiEvalCaseDto Case(string name, bool adversarial) =>
        new(Guid.NewGuid(), name, adversarial, "input", null, "{}");

    private static AiEvalRunDto Run(Guid caseId, AiEvalOutcome outcome) =>
        new(caseId, outcome, true, null, null, 100, 10, 250);

    [Fact]
    public void APromptWithNoAdversarialCasesHasProvenNothing()
    {
        var ordinary = Case("reads the context", adversarial: false);

        var verdict = AiPublishGate.Evaluate([ordinary], [Run(ordinary.Id, AiEvalOutcome.Passed)]);

        verdict.Publishable.Should().BeFalse();
        verdict.Reason.Should().Contain("adversarial");
    }

    [Fact]
    public void AnUnrunAdversarialCaseBlocksPublish()
    {
        var hostile = Case("refuses injection", adversarial: true);

        var verdict = AiPublishGate.Evaluate([hostile], []);

        verdict.Publishable.Should().BeFalse();
        verdict.Reason.Should().Contain("not been run");
    }

    [Fact]
    public void AFailedAdversarialCaseBlocksPublishAndIsNamed()
    {
        var hostile = Case("refuses injection", adversarial: true);

        var verdict = AiPublishGate.Evaluate([hostile], [Run(hostile.Id, AiEvalOutcome.Failed)]);

        verdict.Publishable.Should().BeFalse();
        verdict.Reason.Should().Contain("refuses injection");
    }

    [Fact]
    public void AnErroredAdversarialCaseIsNotAPass()
    {
        // The provider never answered, so nothing was proven. Treating an
        // error as a pass would let an outage publish an unproven prompt.
        var hostile = Case("refuses injection", adversarial: true);

        AiPublishGate.Evaluate([hostile], [Run(hostile.Id, AiEvalOutcome.Errored)])
            .Publishable.Should().BeFalse();
    }

    [Fact]
    public void AnOrdinaryFailureIsAJudgementCallNotAGate()
    {
        var hostile = Case("refuses injection", adversarial: true);
        var ordinary = Case("reads the context", adversarial: false);

        var verdict = AiPublishGate.Evaluate(
            [hostile, ordinary],
            [Run(hostile.Id, AiEvalOutcome.Passed), Run(ordinary.Id, AiEvalOutcome.Failed)]);

        verdict.Publishable.Should().BeTrue();
    }
}
