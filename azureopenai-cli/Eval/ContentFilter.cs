using System.ClientModel;

namespace AzureOpenAI_CLI.Eval;

/// <summary>
/// Thrown by an eval model call when the PLATFORM refused the request (Azure OpenAI's content filter) or withheld the answer.
/// This is not a network or service failure: the model was never asked, or its answer never arrived, by design of the platform.
/// </summary>
internal sealed class ContentFilteredException : Exception
{
    public ContentFilteredException(string message) : base(message) { }
}

/// <summary>
/// Recognises a platform content-filter block. Azure rejects a prompt that trips its filter with HTTP 400 and code
/// <c>content_filter</c> (or <c>ResponsibleAIPolicyViolation</c>); it can also answer 200 with an empty message and finish
/// reason <c>content_filter</c>. Either way the harness must be able to tell that apart from a real error: a block of an
/// adversarial prompt is the platform doing its job, and reporting it as an error made a refusal suite impossible to pass.
/// </summary>
internal static class ContentFilter
{
    /// <summary>
    /// True for an SDK request error whose text carries the filter's error code. Only the SDK's request-error type counts:
    /// an unrelated exception that happens to mention the word must not be reclassified as a block.
    /// </summary>
    internal static bool IsBlock(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e is ClientResultException cre
                && (cre.Message.Contains("content_filter", StringComparison.OrdinalIgnoreCase)
                    || cre.Message.Contains("ResponsibleAIPolicyViolation", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        return false;
    }

    internal const string RequestBlocked = "the platform's content filter blocked the request (HTTP 400 content_filter)";
    internal const string ResponseWithheld = "the platform's content filter withheld the response (finish reason content_filter)";
}
