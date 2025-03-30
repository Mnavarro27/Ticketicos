using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace WebApplication1.Models
{
    public class AuthenticationFilter : IPageFilter
    {
        private readonly ILogger<AuthenticationFilter> _logger;

        public AuthenticationFilter(ILogger<AuthenticationFilter> logger)
        {
            _logger = logger;
        }

        public void OnPageHandlerSelected(PageHandlerSelectedContext context)
        {
            // No action needed
        }

        public void OnPageHandlerExecuting(PageHandlerExecutingContext context)
        {
            var httpContext = context.HttpContext;
            var path = httpContext.Request.Path.Value?.ToLower() ?? "";

            // Skip authentication check for login page and public pages
            if (path.Contains("/cliente/login") ||
                path.Contains("/index") ||
                path.Contains("/error") ||
                path.Contains("/home"))
            {
                return;
            }

            // Check if user is authenticated in session
            var userId = httpContext.Session.GetInt32("UserId");
            var twoFactorAuthenticated = httpContext.Session.GetString("TwoFactorAuthenticated");

            // If not in session, try to get from cookie
            if (!userId.HasValue)
            {
                string? userIdStr = httpContext.Request.Cookies["UserId"];
                if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
                {
                    // If found in cookie, restore to session as well
                    userId = cookieUserId;
                    httpContext.Session.SetInt32("UserId", cookieUserId);
                    httpContext.Session.SetString("TwoFactorAuthenticated", "true");
                    _logger.LogInformation($"Usuario {userId} autenticado desde cookie");
                }
            }

            _logger.LogInformation($"Auth check: Path={path}, UserId={userId}, 2FA={twoFactorAuthenticated}");

            // If user is not authenticated, redirect to login with return URL
            if (!userId.HasValue)
            {
                _logger.LogInformation("User not authenticated, redirecting to login");

                // Store the current path as the return URL
                var returnUrl = httpContext.Request.Path + httpContext.Request.QueryString;
                _logger.LogInformation($"Guardando URL de retorno: {returnUrl}");
                context.Result = new RedirectToPageResult("/Cliente/Login", new { returnUrl });
                return;
            }

            // If two-factor authentication is required but not completed
            if (httpContext.Session.GetString("RequiresTwoFactor") == "true" &&
                twoFactorAuthenticated != "true" &&
                !path.Contains("/cliente/doblefactor"))
            {
                _logger.LogInformation("Two-factor authentication required but not completed");

                // Store the current path as the return URL
                var returnUrl = httpContext.Request.Path + httpContext.Request.QueryString;
                _logger.LogInformation($"Guardando URL de retorno para 2FA: {returnUrl}");
                context.Result = new RedirectToPageResult("/Cliente/doblefactor", new { returnUrl });
                return;
            }
        }

        public void OnPageHandlerExecuted(PageHandlerExecutedContext context)
        {
            // No action needed
        }
    }
}

