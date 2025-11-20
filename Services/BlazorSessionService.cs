namespace ByProxy.Services {
    public class BlazorSessionService {
        private sealed class RegisteredBlazorSession {
            public Guid RegistrationId { get; } = Guid.NewGuid();
            public Guid UserId { get; }
            public Action SessionInvalidationHandler { get; }

            public RegisteredBlazorSession(Guid userId, Action sessionInvalidationHandler) {
                UserId = userId;
                SessionInvalidationHandler = sessionInvalidationHandler;
            }
        }

        private readonly object __lock = new object();
        private List<RegisteredBlazorSession> _sessions = new();

        public Guid RegisterSessionInvalidationHandler(Guid userId, Action sessionInvalidationHandler) {
            var registration = new RegisteredBlazorSession(userId, sessionInvalidationHandler);
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

        public void UnregisterSessionInvalidationHandler(Guid registrationId) {
            lock (__lock) {
                _sessions.RemoveAll(_ => _.RegistrationId == registrationId);
            }
        }
    }
}
