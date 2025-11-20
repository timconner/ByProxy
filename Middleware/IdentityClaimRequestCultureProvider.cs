
namespace ByProxy.Middleware {
    public class IdentityClaimRequestCultureProvider : RequestCultureProvider {
        public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext) {
            var culture = httpContext.User.FindFirstValue(Claims.Culture);
            if (culture == null) return Task.FromResult<ProviderCultureResult?>(null);
            return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(culture));
        }
    }
}
