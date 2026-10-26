using NUnit.Framework;
using Symphony.Dto;
using Symphony.Services;
using System;
using System.Linq;

namespace Symphony.Tests;

public class JobTelemetryOtlpParsingTests
{
    const string JobId = "12345678-1234-1234-1234-123456789abc";

    static string TracesPayload() => $$"""
        {
          "resourceSpans": [
            {
              "resource": {
                "attributes": [
                  { "key": "job.id", "value": { "stringValue": "{{JobId}}" } },
                  { "key": "service.name", "value": { "stringValue": "symphony" } }
                ]
              },
              "scopeSpans": [
                {
                  "spans": [
                    {
                      "traceId": "11111111111111111111111111111111",
                      "spanId": "2222222222222222",
                      "parentSpanId": "3333333333333333",
                      "name": "chat gpt-test",
                      "kind": 3,
                      "startTimeUnixNano": "1700000000000000000",
                      "endTimeUnixNano": "1700000000123000000",
                      "status": { "code": 1 },
                      "attributes": []
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

    [Test]
    public void TryGetJobId_ReadsJobIdFromResourceAttributes()
    {
        var jobId = JobTelemetryCapture.TryGetJobId(TracesPayload());

        Assert.That(jobId, Is.EqualTo(Guid.Parse(JobId)));
    }

    [Test]
    public void ParseOtlpTraces_MapsSpanFields()
    {
        var entries = JobTelemetryCapture.ParseOtlpTraces(TracesPayload());

        Assert.That(entries, Has.Count.EqualTo(1));
        var entry = entries.Single();
        Assert.That(entry.Signal, Is.EqualTo("span"));
        Assert.That(entry.Name, Is.EqualTo("chat gpt-test"));
        Assert.That(entry.TraceId, Is.EqualTo("11111111111111111111111111111111"));
        Assert.That(entry.SpanId, Is.EqualTo("2222222222222222"));
        Assert.That(entry.ParentSpanId, Is.EqualTo("3333333333333333"));
        Assert.That(entry.DurationMs, Is.EqualTo(123));
        Assert.That(entry.PayloadJson, Does.Contain("chat gpt-test"));
    }

    [Test]
    public void ParseOtlpMetrics_MapsDataPoint()
    {
        var json = """
            {
              "resourceMetrics": [
                {
                  "resource": { "attributes": [ { "key": "job.id", "value": { "stringValue": "12345678-1234-1234-1234-123456789abc" } } ] },
                  "scopeMetrics": [
                    {
                      "metrics": [
                        { "name": "gen_ai.client.token.usage", "sum": { "dataPoints": [ { "timeUnixNano": "1700000000000000000" } ] } }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var entries = JobTelemetryCapture.ParseOtlpMetrics(json);

        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries.Single().Signal, Is.EqualTo("metric"));
        Assert.That(entries.Single().Name, Is.EqualTo("gen_ai.client.token.usage"));
    }

    [Test]
    public void ParseOtlpLogs_MapsLogRecord()
    {
        var json = """
            {
              "resourceLogs": [
                {
                  "scopeLogs": [
                    {
                      "logRecords": [
                        {
                          "timeUnixNano": "1700000000000000000",
                          "severityText": "INFO",
                          "traceId": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                          "spanId": "bbbbbbbbbbbbbbbb",
                          "body": { "stringValue": "hello from cli" }
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var entries = JobTelemetryCapture.ParseOtlpLogs(json);

        Assert.That(entries, Has.Count.EqualTo(1));
        var entry = entries.Single();
        Assert.That(entry.Signal, Is.EqualTo("log"));
        Assert.That(entry.Name, Is.EqualTo("hello from cli"));
        Assert.That(entry.Severity, Is.EqualTo("INFO"));
    }
}
