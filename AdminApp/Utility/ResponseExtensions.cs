namespace ByProxy.AdminApp.Utility {
    public static class ResponseExtensions {
        public static List<string> SimulateResponse(this StaticResponse response, IStringLocalizer localizer) {
            var responseDetails = new List<string>();

            responseDetails.Add($"{localizer[response.UseTarpit ? "RouteSummary_Tarpit" : "RouteSummary_Static"]}: {response.HttpStatusCode}");

            if (response.Body != null) {
                responseDetails.Add($"{localizer["With Body"]}: {Functions.GetHumanReadableBytes(Encoding.UTF8.GetByteCount(response.Body))}");
            }

            if (response.Headers != null && response.Headers.Count > 0) {
                if (response.Headers.Count == 1) {
                    responseDetails.Add($"{localizer["With Header"]}: {response.Headers.Keys.First()}");
                } else {
                    responseDetails.Add(localizer["With Multiple Headers"]);
                }
            }

            return responseDetails;
        }
    }
}
