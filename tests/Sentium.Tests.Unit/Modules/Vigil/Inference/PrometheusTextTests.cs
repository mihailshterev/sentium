using Sentium.Vigil.Features.Inference;
using Xunit;

namespace Sentium.Tests.Unit.Modules.Vigil.Inference;

public sealed class PrometheusTextTests
{
    [Fact]
    public void ParseUnlabelled_ReadsSamples_AndSkipsCommentsLabelsAndNonFiniteValues()
    {
        const string text = """
            # HELP llamacpp:prompt_tokens_total Number of prompt tokens processed.
            # TYPE llamacpp:prompt_tokens_total counter
            llamacpp:prompt_tokens_total 1234
            llamacpp:predicted_tokens_seconds 42.5 1732190000000
            llamacpp:spec_decode_num_accepted_tokens_per_pos_total{position="0"} 7
            llamacpp:n_busy_slots_per_decode NaN

            malformed-line
            """;

        var samples = PrometheusText.ParseUnlabelled(text);

        Assert.Equal(2, samples.Count);
        Assert.Equal(1234, samples["llamacpp:prompt_tokens_total"]);
        Assert.Equal(42.5, samples["llamacpp:predicted_tokens_seconds"]);
    }

    [Fact]
    public void Snapshot_IsNull_WhenACounterIsMissing()
    {
        var samples = PrometheusText.ParseUnlabelled("llamacpp:prompt_tokens_total 1\nllamacpp:prompt_seconds_total 1\n");

        Assert.Null(LlamaServerSnapshot.FromSamples(samples));
    }

    [Fact]
    public void Snapshot_ReadsCountersAndQueue()
    {
        const string text = """
            llamacpp:prompt_tokens_total 100
            llamacpp:prompt_seconds_total 0.5
            llamacpp:tokens_predicted_total 40
            llamacpp:tokens_predicted_seconds_total 2
            llamacpp:requests_processing 2
            llamacpp:requests_deferred 1
            """;

        var snapshot = LlamaServerSnapshot.FromSamples(PrometheusText.ParseUnlabelled(text));

        Assert.Equal(new LlamaServerSnapshot(100, 0.5, 40, 2, 2, 1), snapshot);
    }
}
