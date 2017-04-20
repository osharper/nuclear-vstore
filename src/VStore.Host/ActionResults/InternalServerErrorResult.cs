using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;

namespace NuClear.VStore.Host.ActionResults
{
    public sealed class InternalServerErrorResult : ContentResult
    {
        private readonly Exception _exception;
        private readonly string _message;
        private readonly object[] _args;

        public InternalServerErrorResult(Exception exception, string message, params object[] args)
        {
            _exception = exception;
            _message = message;
            _args = args;

            StatusCode = 500;
        }

        public override void ExecuteResult(ActionContext context)
        {
            LogAndSetContent(context);
            base.ExecuteResult(context);
        }

        public override Task ExecuteResultAsync(ActionContext context)
        {
            LogAndSetContent(context);
            return base.ExecuteResultAsync(context);
        }

        private void LogAndSetContent(ActionContext context)
        {
            var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger(typeof(InternalServerErrorResult));
            logger.LogError(new EventId(), _exception, _message, _args);

            var logValues = new FormattedLogValues(_message, _args);
            Content = $"{logValues}. See logs for details. RequestId: '{context.HttpContext.TraceIdentifier}'";
        }
    }
}