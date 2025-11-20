using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System.Globalization;
using ByProxy.AdminApp.Components;

namespace ByProxy.Services {
    public class AuthService {
        private readonly ILogger<AuthService> _logger;
        private readonly ProxyDb _db;
        private readonly IMemoryCache _cache;
        private readonly IHttpContextAccessor _context;
        private readonly ConfigurationService _config;
        private readonly BlazorSessionService _blazorSessions;

        private const string CachePrefix = "AuthSession_";

        public enum LoginResult {
            Failed,
            Success,
            PasswordResetRequired
        }

        public AuthService(
            ILogger<AuthService> logger,
            ProxyDb db,
            IMemoryCache cache,
            IHttpContextAccessor context,
            ConfigurationService config,
            BlazorSessionService blazorSessions
        ) {
            _logger = logger;
            _db = db;
            _cache = cache;
            _context = context;
            _config = config;
            _blazorSessions = blazorSessions;
        }

        public Guid? GetUserId() {
            if (_context.HttpContext == null) return null;
            string? userId = _context.HttpContext.User.FindFirstValue(Claims.UserId);
            return userId == null ? null : Guid.Parse(userId);
        }

        public string? GetDisplayName() {
            if (_context.HttpContext == null) return null;
            return _context.HttpContext.User.FindFirstValue(Claims.DisplayName);
        }

        public string? GetFullName() {
            if (_context.HttpContext == null) return null;
            return _context.HttpContext.User.FindFirstValue(Claims.FullName);
        }

        public string? GetPreferredTheme() {
            if (_context.HttpContext == null) return null;
            return _context.HttpContext.User.FindFirstValue(Claims.PreferredTheme);
        }

        public async Task UpdateUserCulture(CultureInfo culture) {
            if (_context.HttpContext == null) return;

            var identity = _context.HttpContext.User.Identity as ClaimsIdentity;
            if (identity == null) return;

            var existingClaim = identity.FindFirst(Claims.Culture);
            if (existingClaim != null) identity.RemoveClaim(existingClaim);
            identity.AddClaim(new Claim(Claims.Culture, culture.Name));

            string? userId = _context.HttpContext.User.FindFirstValue(Claims.UserId);
            if (userId == null) return;
            await _db.Users
                .Where(_ => _.Id == Guid.Parse(userId))
                .ExecuteUpdateAsync(setters => setters.SetProperty(_ => _.Culture,culture.Name));
        }

        public async Task UpdatePreferredTheme(AppTheme theme) {
            if (_context.HttpContext == null) return;
            
            var identity = _context.HttpContext.User.Identity as ClaimsIdentity;
            if (identity == null) return;
            
            var existingClaim = identity.FindFirst(Claims.PreferredTheme);
            if (existingClaim != null) identity.RemoveClaim(existingClaim);
            identity.AddClaim(new Claim(Claims.PreferredTheme, theme.Name));

            string? userId = _context.HttpContext.User.FindFirstValue(Claims.UserId);
            if (userId == null) return;
            await _db.Users
                .Where(_ => _.Id == Guid.Parse(userId))
                .ExecuteUpdateAsync(setters => setters.SetProperty(_ => _.PreferredTheme, theme.Name));
        }

        public async Task InvalidateCurrentUsersSessions() {
            if (_context.HttpContext == null) return;

            string? userIdString = _context.HttpContext.User.FindFirstValue(Claims.UserId);
            if (userIdString == null) return;
            if (!Guid.TryParse(userIdString, out Guid userId)) return;

            await InvalidateUsersSessions(userId);
        }

        public async Task InvalidateUsersSessions(Guid userId) {
            var userSessions = await _db.AuthSessions
                .Where(_ => _.UserId == userId)
                .ToListAsync();

            foreach (var session in userSessions) {
                _cache.Remove($"{CachePrefix}{session.Key}");
                _db.AuthSessions.Remove(session);
            }
            await _db.SaveChangesAsync();

            _blazorSessions.SessionInvalidationRequested(userId);
        }

        public async Task<string?> CreateInitialAdmin(string fullName, string displayName, string username, string password) {
            if (
                string.IsNullOrWhiteSpace(username)
                || string.IsNullOrWhiteSpace(displayName)
                || string.IsNullOrWhiteSpace(username)
                || string.IsNullOrEmpty(password)
            ) {
                return "AllFieldsRequired";
            }

            if (await _config.CheckAdminInitialized()) {
                throw new Exception("One or more admin accounts already exist.");
            }
            
            var user = await CreateUser(fullName, displayName, username, password, false);
            var adminRole = await _db.AuthRoles.FirstAsync(_ => _.Name == AuthRoles.Admin);
            user.Roles = new List<AuthRole> { adminRole };

            await _db.SaveChangesAsync();

            if (!await _config.CheckAdminInitialized()) {
                throw new Exception("Configuration Service Reports Admin Doesn't Exist");
            }

            return null;
        }

        public async Task<UserEntity> CreateUser(
            string fullName,
            string displayName,
            string username,
            string password,
            bool passwordResetRequired = true
        ) {
            if (string.IsNullOrWhiteSpace(fullName)) throw new Exception("Full name is required.");
            if (string.IsNullOrWhiteSpace(displayName)) throw new Exception("Display name is required.");
            if (string.IsNullOrWhiteSpace(username)) throw new Exception("Username is required.");
            if (string.IsNullOrEmpty(password)) throw new Exception("Password is required.");

            username = username.ToLowerInvariant();
            if (await _db.Users.AnyAsync(_ => _.Username == username)) throw new Exception("Username already in use.");

            var user = _db.Users.Add(new UserEntity {
                FullName = fullName,
                DisplayName = displayName,
                Username = username,
                PasswordHash = HashSecret(password),
                PasswordLastSet = DateTime.UtcNow,
                PasswordResetRequired = passwordResetRequired,
            });
            await _db.SaveChangesAsync();

            return user.Entity;
        }

        public async Task DeleteUser(Guid userId) {
            var user = await _db.Users.FirstOrDefaultAsync(_ => _.Id == userId);
            if (user == null) throw new Exception("User not found.");

            await InvalidateUsersSessions(userId);

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
        }

        public async Task<LoginResult> CookieLogin(string username, string password) {
            if (_context.HttpContext == null) throw new Exception("Unable to acquire HttpContext");

            username = username.ToLowerInvariant();
            var user = await _db.Users.FirstOrDefaultAsync(_ => _.Username == username);
            if (user == null) return LoginResult.Failed;

            if (!VerifySecret(password, user.PasswordHash)) return LoginResult.Failed;

            if (user.PasswordResetRequired) return LoginResult.PasswordResetRequired;

            // Authentication Successful - Perform login
            var claimsPrincipal = await GenerateUserPrincipal(CookieAuthenticationDefaults.AuthenticationScheme, user.Id);
            if (claimsPrincipal == null) return LoginResult.Failed;

            await _context.HttpContext.SignInAsync(claimsPrincipal);
            _logger.LogInformation($"{user.DisplayName} ({user.Username}) logged in.");
            return LoginResult.Success;
        }

        public async Task<string?> TryResetPassword(string username, string oldPassword, string newPassword, string confirmPassword) {
            if (
                string.IsNullOrWhiteSpace(username)
                || string.IsNullOrEmpty(oldPassword)
                || string.IsNullOrEmpty(newPassword)
                || string.IsNullOrEmpty(confirmPassword)
            ) {
                return "AllFieldsRequired";
            }

            if (newPassword == oldPassword) return "ERR_SameAsOldPassword";
            if (newPassword != confirmPassword) return "ERR_NewAndConfirmMismatch";

            var passwordValidator = new Regex("^(?=(?:.*[A-Z]){1,})(?=(?:.*[a-z]){1,})(?=(?:.*\\d){1,}).{8,}$");
            if (!passwordValidator.IsMatch(newPassword)) return "ERR_PasswordComplexityFailed";
            
            username = username.ToLowerInvariant();
            var user = await _db.Users.FirstOrDefaultAsync(_ => _.Username == username);
            
            if (user == null) return "Invalid Login";
            if (!VerifySecret(oldPassword, user.PasswordHash)) return "Invalid Login";

            user.PasswordHash = HashSecret(newPassword);
            user.PasswordLastSet = DateTime.UtcNow;
            user.PasswordResetRequired = false;
            await _db.SaveChangesAsync();

            await InvalidateUsersSessions(user.Id);

            return null;
        }

        public async Task SetTempPassword(Guid userId, string tempPassword) {
            var user = await _db.Users.FirstOrDefaultAsync(_ => _.Id == userId);
            if (user == null) throw new Exception("User not found.");

            user.PasswordHash = HashSecret(tempPassword);
            user.PasswordLastSet = DateTime.UtcNow;
            user.PasswordResetRequired = true;
            await _db.SaveChangesAsync();
            await InvalidateUsersSessions(user.Id);
        }

        private string HashSecret(string secret) {
            // Version 1 Hash: Pbkdf2, 128-bit/16-byte salt, HMAC-SHA256, 10000 iterations, 256-bit/32-byte output
            const string hashVersion = "1";
            byte[] saltBytes = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create()) {
                rng.GetBytes(saltBytes);
            }
            string hashedSecret = PerformHashV1(secret, saltBytes);
            return string.Format(CultureInfo.InvariantCulture, "{0}${1}${2}", hashVersion, Convert.ToBase64String(saltBytes), hashedSecret);
        }

        private bool VerifySecret(string secret, string storedHash) {
            string[] hashParts = storedHash.Split('$');
            if (hashParts.Length != 3) return false;

            switch (hashParts[0]) {
                case "1":
                    byte[] saltBytes = Convert.FromBase64String(hashParts[1]);
                    string hashedSecret = PerformHashV1(secret, saltBytes);
                    return hashedSecret == hashParts[2];
                default:
                    return false;
            }
        }

        private string PerformHashV1(string secret, byte[] saltBytes) {
            return Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: secret,
                salt: saltBytes,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 10000,
                numBytesRequested: 256 / 8));
        }

        private async Task<ClaimsPrincipal?> GenerateUserPrincipal(string authScheme, Guid userId) {
            var user = await _db.Users
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .Include(u => u.Roles)
                        .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return null;

            List<Claim> claims = new List<Claim>{
                new Claim(ClaimTypes.AuthenticationMethod, authScheme),
                new Claim(Claims.UserId, user.Id.ToString()),
                new Claim(Claims.DisplayName, user.DisplayName),
                new Claim(Claims.FullName, user.FullName),
                new Claim(ClaimTypes.Role, AuthRoles.User)
            };
            if (user.Culture != null) claims.Add(new Claim(Claims.Culture, user.Culture));
            if (user.PreferredTheme != null) claims.Add(new Claim(Claims.PreferredTheme, user.PreferredTheme));

            if (user.Roles.Any(_ => _.Name == AuthRoles.Admin)) {
                foreach (var role in AuthRoles.AssignableRoles) {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            } else {
                foreach (var roleMembership in user.Roles) {
                    claims.Add(new Claim(ClaimTypes.Role, roleMembership.Name));
                }
            }

            ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims, authScheme);
            return new ClaimsPrincipal(claimsIdentity);
        }        

        public async Task InvalidateToken(string token) {
            _cache.Remove($"{CachePrefix}{token}");
            await _db.AuthSessions
                .Where(_ => _.Key == token)
                .ExecuteDeleteAsync();
        }

        public async Task<string> CreateToken(AuthenticationTicket ticket) {
            if (!Guid.TryParse(ticket.Principal.FindFirstValue(Claims.UserId), out Guid userId)) throw new Exception("UserId Not Found");
            var user = await _db.Users.FirstAsync(_ => _.Id == userId);
            var session = await GenerateUserSession(user);
            _cache.Set($"{CachePrefix}{session.Key}", ticket);
            return session.Key;
        }

        public async Task<AuthenticateResult> AuthenticateToken(string authScheme, string token) {
            string cacheKey = $"{CachePrefix}{token}";

            if (_cache.TryGetValue(cacheKey, out AuthenticationTicket? cachedTicket) && cachedTicket != null) {
                if (cachedTicket.Properties.ExpiresUtc < DateTime.UtcNow) {
                    return AuthenticateResult.Fail("Token Expired");
                } else {
                    return AuthenticateResult.Success(cachedTicket);
                }
            }

            AuthSession? session = await _db.AuthSessions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(_ => _.Key == token);
            
            if (session == null) return AuthenticateResult.Fail("Invalid Token");

            if (session.ExpiresAt < DateTime.UtcNow)
                return AuthenticateResult.Fail("Token Expired");

            var claimsPrincipal = await GenerateUserPrincipal(authScheme, session.UserId!.Value);
            if (claimsPrincipal == null) return AuthenticateResult.Fail("User associated with session not found.");

            AuthenticationProperties authProperties = new AuthenticationProperties { ExpiresUtc = session.ExpiresAt };

            var authTicket = new AuthenticationTicket(claimsPrincipal, authProperties, authScheme);
            _cache.Set(cacheKey, authTicket);
            return AuthenticateResult.Success(authTicket);
        }

        private async Task<AuthSession> GenerateUserSession(UserEntity user) {
            var session = _db.AuthSessions.Add(new AuthSession {
                UserId = user.Id,
                Key = Generators.GenerateSessionKey(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(_config.AdminSessionLimit)
            });
            await _db.SaveChangesAsync();
            return session.Entity;
        }
    }
}
