namespace ErpBoulder.Api.Middleware;

using System.Net;
using System.Text.Json;

public sealed class GlobalExceptionMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            context.Response.StatusCode = exception is InvalidOperationException ? (int)HttpStatusCode.BadRequest : (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                message = exception.Message,
                detail = context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment() ? exception.ToString() : null
            }));
        }
    }
}
