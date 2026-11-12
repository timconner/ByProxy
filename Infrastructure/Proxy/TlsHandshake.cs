namespace ByProxy.Infrastructure.Proxy {
    public static class TlsHandshake {
        public static TlsHandshakeCallbackOptions CreateCallbackOptions(Func<ConnectionContext?, string?, SslStreamCertificateContext?> certificateSelector) {
            return new TlsHandshakeCallbackOptions {
                OnConnection = connection => {
                    var certificate = certificateSelector(connection.Connection, connection.ClientHelloInfo.ServerName);
                    if (certificate == null) {
                        throw new AuthenticationException("No matching TLS certificate or fallback certificate configured.");
                    }

                    return new ValueTask<SslServerAuthenticationOptions>(new SslServerAuthenticationOptions {
                        ServerCertificateContext = certificate,
                        ApplicationProtocols = [SslApplicationProtocol.Http2, SslApplicationProtocol.Http11]
                    });
                }
            };
        }
    }
}
