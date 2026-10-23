using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace PartyKing.API.Metrics;

/// <summary>Measures how long each controller action takes and records it in <see cref="ActionMetrics"/>.</summary>
public sealed class ActionMetricsFilter : IAsyncActionFilter
{
    public const string SuccessOutcome = "success";
    public const string ErrorOutcome = "error";
    public const string ExceptionOutcome = "exception";

    private readonly ActionMetrics _metrics;

    public ActionMetricsFilter(ActionMetrics metrics)
    {
        _metrics = metrics;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        ActionExecutedContext? executed = null;

        try
        {
            executed = await next();
        }
        finally
        {
            Record(context, executed, Stopwatch.GetElapsedTime(startTimestamp));
        }
    }

    private void Record(ActionExecutingContext context, ActionExecutedContext? executed, TimeSpan elapsed)
    {
        var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
        var controller = descriptor?.ControllerName ?? "unknown";
        var action = descriptor?.ActionName ?? "unknown";

        string outcome;
        int statusCode;

        if (executed is null || (executed.Exception is not null && !executed.ExceptionHandled))
        {
            outcome = ExceptionOutcome;
            statusCode = StatusCodes.Status500InternalServerError;
        }
        else
        {
            statusCode = (executed.Result as IStatusCodeActionResult)?.StatusCode
                         ?? context.HttpContext.Response.StatusCode;
            outcome = statusCode >= 400 ? ErrorOutcome : SuccessOutcome;
        }

        _metrics.RecordDuration(elapsed, controller, action, context.HttpContext.Request.Method, statusCode, outcome);
    }
}
