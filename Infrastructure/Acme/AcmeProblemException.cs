namespace ByProxy.Infrastructure.Acme {
    public class AcmeProblemException : HttpRequestException {
        public AcmeProblem AcmeProblem { get; init; }

        public AcmeProblemException(HttpResponseMessage response, AcmeProblem acmeProblem) :
            base(
                $"{(int)response.StatusCode} {response.ReasonPhrase} on {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}:\n{JsonSerializer.Serialize(acmeProblem)}",
                    null,
                    response.StatusCode
        ) {
            AcmeProblem = acmeProblem;
        }
    }
}
