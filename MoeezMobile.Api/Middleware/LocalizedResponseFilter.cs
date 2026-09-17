using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MoeezMobile.Api.Models.Common;

namespace MoeezMobile.Api.Middleware;

public static class RequestLanguage
{
    /// <summary>The caller's language ("ur" or "en"), taken from the Accept-Language header.</summary>
    public static string Of(HttpContext context) =>
        Messages.Normalize(context.Request.Headers.AcceptLanguage.ToString());
}

/// <summary>
/// Controllers put message KEYS on the response envelope; this turns them into text in the
/// caller's language at the last moment, so no controller has to know about languages.
/// </summary>
public class LocalizedResponseFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult { Value: IApiResponse envelope }) return;

        var language = RequestLanguage.Of(context.HttpContext);
        envelope.Message = Messages.Resolve(envelope.Message, language);
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
