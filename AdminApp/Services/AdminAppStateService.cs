using ByProxy.AdminApp.Components;

namespace ByProxy.AdminApp.Services {
    public class AdminAppStateService {
        private readonly AuthService _auth;
        private readonly IHttpContextAccessor _context;
        private readonly IJSRuntime _js;

        public AppTheme? PreferredTheme { get; private set; }

        public event Action? OnThemeChange;

        public bool ConfigurationChangeInProgress = false;

        public AdminAppStateService(AuthService auth, IHttpContextAccessor context, IJSRuntime js) {
            _auth = auth;
            _context = context;
            _js = js;

            var preferredTheme = _auth.GetPreferredTheme();
            if (preferredTheme == null && _context.HttpContext != null) {
                _context.HttpContext.Request.Cookies.TryGetValue(CookieNames.AdminTheme, out preferredTheme);
            }
            if (preferredTheme != null) {
                PreferredTheme = Themes.AvailableThemes.FirstOrDefault(_ => _.Name == preferredTheme);
            }
        }

        public async Task UpdatePreferredTheme(AppTheme theme) {
            PreferredTheme = theme;
            if (_auth.GetPreferredTheme() != theme.Name) {
                await _auth.UpdatePreferredTheme(theme);
            }
            await UpdateThemeCookie();
            OnThemeChange?.Invoke();
        }

        public async Task UpdateThemeCookie() {
            if (PreferredTheme == null) return;
            await _js.InvokeVoidAsync("setCookie", $"{CookieNames.AdminTheme}={PreferredTheme.Name}; path=/; max-age=31536000;");
        }
    }
}
