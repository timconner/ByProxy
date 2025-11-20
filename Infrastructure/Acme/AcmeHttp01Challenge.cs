namespace ByProxy.Infrastructure.Acme {
    public class AcmeHttp01Challenge {
        public byte[] KeyAuthorization { get; init; }
        public TaskCompletionSource TaskCompletionSource { get; init; }

        public AcmeHttp01Challenge(byte[] keyAuthorization) {
            KeyAuthorization = keyAuthorization;
            TaskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
