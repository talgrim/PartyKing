using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using PartyKing.API.Metrics;

namespace PartyKing.Api.Tests;

public class ActionMetricsFilterTests
{
    private sealed record Measurement(double Value, Dictionary<string, object?> Tags);

    private static (ActionMetricsFilter Filter, List<Measurement> Measurements, IDisposable Listener, ActionMetrics Metrics)
        Create()
    {
        var metrics = new ActionMetrics();
        var measurements = new List<Measurement>();
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == ActionMetrics.MeterName
                && instrument.Name == ActionMetrics.DurationInstrumentName)
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
            measurements.Add(new Measurement(value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value))));
        listener.Start();

        return (new ActionMetricsFilter(metrics), measurements, listener, metrics);
    }

    private static ActionExecutingContext CreateContext(string method = "GET")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = method;
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ControllerActionDescriptor { ControllerName = "Queue", ActionName = "GetSongs" });

        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
    }

    private static ActionExecutionDelegate Next(ActionExecutingContext context, IActionResult? result, Exception? exception = null)
    {
        return () =>
        {
            if (exception is not null)
            {
                throw exception;
            }

            return Task.FromResult(new ActionExecutedContext(context, context.Filters, context.Controller) { Result = result });
        };
    }

    [Fact]
    public async Task OnActionExecution_WhenTheActionSucceeds_RecordsDurationWithTags()
    {
        var (filter, measurements, listener, metrics) = Create();
        using var _ = listener;
        using var __ = metrics;
        var context = CreateContext();

        await filter.OnActionExecutionAsync(context, Next(context, new OkObjectResult("x")));

        var measurement = Assert.Single(measurements);
        Assert.True(measurement.Value >= 0);
        Assert.Equal("Queue", measurement.Tags["controller"]);
        Assert.Equal("GetSongs", measurement.Tags["action"]);
        Assert.Equal("GET", measurement.Tags["http.request.method"]);
        Assert.Equal(200, measurement.Tags["http.response.status_code"]);
        Assert.Equal(ActionMetricsFilter.SuccessOutcome, measurement.Tags["outcome"]);
    }

    [Fact]
    public async Task OnActionExecution_WhenTheResultIsAnErrorStatus_RecordsAnErrorOutcome()
    {
        var (filter, measurements, listener, metrics) = Create();
        using var _ = listener;
        using var __ = metrics;
        var context = CreateContext("POST");

        await filter.OnActionExecutionAsync(context, Next(context, new NotFoundResult()));

        var measurement = Assert.Single(measurements);
        Assert.Equal(404, measurement.Tags["http.response.status_code"]);
        Assert.Equal(ActionMetricsFilter.ErrorOutcome, measurement.Tags["outcome"]);
        Assert.Equal("POST", measurement.Tags["http.request.method"]);
    }

    [Fact]
    public async Task OnActionExecution_WhenTheActionThrows_RecordsAnExceptionOutcomeAndRethrows()
    {
        var (filter, measurements, listener, metrics) = Create();
        using var _ = listener;
        using var __ = metrics;
        var context = CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            filter.OnActionExecutionAsync(context, Next(context, null, new InvalidOperationException("boom"))));

        var measurement = Assert.Single(measurements);
        Assert.Equal(500, measurement.Tags["http.response.status_code"]);
        Assert.Equal(ActionMetricsFilter.ExceptionOutcome, measurement.Tags["outcome"]);
    }
}
