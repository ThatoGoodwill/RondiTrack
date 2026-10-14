using System.Diagnostics;
using OpenTelemetry;

namespace RondiTrack.Api.Observability;

// A span is a data store (README, "Telemetry is a data store"). ASP.NET Core's instrumentation records the URL's query
// string on the request span (`url.query`). For RondiTrack that is a problem: the contributions list is paged with a
// `pageToken`, and when the list is sorted by Amount that token contains the last amount on the page.
// An amount in a trace, readable by everyone with dashboard access, is exactly what this assignment is about.
//
// So the query string is replaced before the span leaves the process. The PATH stays (it is how you find a request);
// the query does not (it is how a client's data gets in).
public sealed class QueryStringRedactionProcessor : BaseProcessor<Activity>
{
    private const string Redacted = "[redacted]";

    // Tags that hold the query on its own, and tags that hold a whole URL or path-and-query.
    private static readonly string[] QueryTags = ["url.query", "http.query"];
    private static readonly string[] UrlTags = ["url.full", "http.url", "http.target"];

    public override void OnEnd(Activity data)
    {
        foreach (var tag in QueryTags)
            if (data.GetTagItem(tag) is not null)
                data.SetTag(tag, Redacted);

        foreach (var tag in UrlTags)
            if (data.GetTagItem(tag) is string url)
            {
                var question = url.IndexOf('?');
                if (question >= 0)
                    data.SetTag(tag, url[..question]);
            }
    }
}
