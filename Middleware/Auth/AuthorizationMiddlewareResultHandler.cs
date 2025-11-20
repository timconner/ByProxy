using Microsoft.AspNetCore.Authorization.Policy;

namespace ByProxy.Middleware.Auth {
    public class AuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler {
        public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult) {
            //Allows for content to be handled by AuthorizationViews instead of the Cookie Authentication Middleware
            return next(context);
        }
    }
}
