namespace ByProxy.Services {
    public class BlazorSessionService {
        private sealed class RegisteredBlazorSession {
            public Guid RegistrationId { get; } = Guid.NewGuid();
            public Guid UserId { get; }
            public string SessionKey { get; }
            public Action SessionInvalidationHandler { get; }

            public RegisteredBlazorSession(Guid userId, string sessionKey, Action sessionInvalidationHandler) {
                UserId = userId;
                SessionKey = sessionKey;
                SessionInvalidationHandler = sessionInvalidationHandler;
            }
        }

        private readonly object __lock = new object();
        private List<RegisteredBlazorSession> _sessions = new();

        public Guid RegisterSessionInvalidationHandler(Guid userId, string sessionKey, Action sessionInvalidationHandler) {
            var registration = new RegisteredBlazorSession(userId, sessionKey, sessionInvalidationHandler);
            lock (__lock) {
                _sessions.Add(registration);
            }
            return registration.RegistrationId;
        }

        public void SessionInvalidationRequested(Guid userId) {
            List<RegisteredBlazorSession> sessionsToInvalidate;
            lock (__lock) {
                sessionsToInvalidate = _sessions.Where(_ => _.UserId == userId).ToList();
            }
            sessionsToInvalidate.ForEach(_ => _.SessionInvalidationHandler.Invoke());
        }

        public void SessionInvalidationRequested(string sessionKey) {
            List<RegisteredBlazorSession> sessionsToInvalidate;
            lock (__lock) {
                sessionsToInvalidate = _sessions.Where(_ => _.SessionKey == sessionKey).ToList();
            }
            sessionsToInvalidate.ForEach(_ => _.SessionInvalidationHandler.Invoke());
        }

        public void UnregisterSessionInvalidationHandler(Guid registrationId) {
            lock (__lock) {
                _sessions.RemoveAll(_ => _.RegistrationId == registrationId);
            }
        }
    }
}
