namespace ByProxy.Infrastructure.Acme {
    public static class AcmeChallengeType {
        public const string HTTP_01 = "http-01";
        public const string DNS_01 = "dns-01";

        //public const string TLS_ALPN_01 = "tls-alpn-01";
        /* Due to API limitations in ASP.NET Core / Kestrel, tls-alpn-01 cannot be implemented.
         * The only workaround available would result in service disruptions while challenges are active
         * and so it has not been implemented. */
    }
}
