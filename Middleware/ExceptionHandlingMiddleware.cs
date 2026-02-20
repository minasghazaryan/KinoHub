using System.Net;
using System.Text;

namespace KinoHub.Web.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            if (context.Response.HasStarted)
                throw;
            await HandleExceptionAsync(context);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context)
    {
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "text/html; charset=utf-8";

        var html = """
            <!DOCTYPE html>
            <html lang="ru">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Ошибка</title></head>
            <body style="font-family:system-ui,sans-serif;max-width:600px;margin:2rem auto;padding:0 1rem;">
            <h1>Произошла ошибка</h1>
            <p>При обработке запроса произошла непредвиденная ошибка. Попробуйте позже.</p>
            <p><a href="/">Вернуться на главную</a></p>
            </body>
            </html>
            """;

        await context.Response.WriteAsync(html, Encoding.UTF8);
    }
}
