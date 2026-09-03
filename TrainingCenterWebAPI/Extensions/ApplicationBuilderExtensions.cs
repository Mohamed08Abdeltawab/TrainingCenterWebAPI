using System.Security.Claims;

namespace TrainingCenter.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        // Global Security Audit & Response Status Logging Middleware
        //can call it in program.cs like that -> app.UseSecurityAnditLogging
        public static IApplicationBuilder UseSecurityAuditLogging(this IApplicationBuilder app)
        {
            //middleware have two parametar -> context: http reques and response, next: go to next middleware
            return app.Use(async (context, next) =>
            {
                //don't execute code before finish the control and get response 
                await next();
                
                //authorization
                if (context.Response.StatusCode == StatusCodes.Status403Forbidden)
                {
                    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";//get from claim
                    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    var path = context.Request.Path.ToString();

                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();//not create new logger get it from system (dependency injection)
                    logger.LogWarning("Forbidden access attempt. UserId={UserId}, Path={Path}, IP={IP}", userId, path, ip);
                }//authontication
                else if (context.Response.StatusCode == StatusCodes.Status401Unauthorized)
                {
                    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    var path = context.Request.Path.ToString();

                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogWarning("Unauthorized request block. Path={Path}, IP={IP}", path, ip);
                }
            });
        }

        // Global Unhandled Exception Handling Middleware
        public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                //get any execption occur
                try
                {
                    await next();
                }
                catch (Exception ex)
                {
                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                    var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    var path = context.Request.Path;

                    logger.LogError(ex, "Unhandled exception occurred. Path={Path}, IP={IP}", path, ip);

                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "application/json";

                    var response = new { message = "An internal server error occurred. Please try again later." };
                    await context.Response.WriteAsJsonAsync(response);
                }
            });
        }
    }
}