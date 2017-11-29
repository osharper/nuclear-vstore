﻿using Microsoft.AspNetCore.Mvc;

namespace NuClear.VStore.Http.Core.ActionResults
{
    public sealed class BadRequestContentResult : ContentResult
    {
        public BadRequestContentResult(string message)
        {
            StatusCode = 400;
            Content = message;
        }
    }
}