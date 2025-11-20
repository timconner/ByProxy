namespace ByProxy.Data {
    public class ProxyDb : DbContext, IDataProtectionKeyContext {
        private readonly IProxyStateService _proxyState;
        public ProxyDb(DbContextOptions options, IProxyStateService proxyState) : base(options) {
            _proxyState = proxyState;
        }
        
        public DbSet<ProxyConfig> Configurations => Set<ProxyConfig>();
        public DbSet<ProxyCluster> Clusters => Set<ProxyCluster>();
        public DbSet<ProxyDestination> Destinations => Set<ProxyDestination>();
        public DbSet<ProxyRoute> Routes  => Set<ProxyRoute>();
        public DbSet<ProxyRouteMatch> MatchCriteria => Set<ProxyRouteMatch>();
        public DbSet<ProxyRouteTransform> RouteTransforms => Set<ProxyRouteTransform>();
        public DbSet<ProxySniMap> SniMaps => Set<ProxySniMap>();

        public DbSet<AuthSession> AuthSessions => Set<AuthSession>();
        public DbSet<AuthRole> AuthRoles => Set<AuthRole>();
        public DbSet<UserEntity> Users => Set<UserEntity>();
        
        public DbSet<AcmeAccount> AcmeAccounts => Set<AcmeAccount>();
        public DbSet<AcmeDnsProvider> AcmeDnsProviders => Set<AcmeDnsProvider>();
        public DbSet<CACert> CertificateAuthorities => Set<CACert>();

        public DbSet<ProxyCert> Certificates => Set<ProxyCert>();
        public DbSet<ServerCert> ServerCerts => Set<ServerCert>();
        public DbSet<ImportedCert> ImportedCerts => Set<ImportedCert>();
        public DbSet<IssuedCert> IssuedCerts => Set<IssuedCert>();
        public DbSet<AcmeCert> AcmeCerts => Set<AcmeCert>();
        public DbSet<AcmeCertHost> AcmeHosts => Set<AcmeCertHost>();

        public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            // Keys
            modelBuilder.Entity<AuthSession>()
                .HasKey(_ => _.Key);

            modelBuilder.Entity<ProxyConfig>()
                .HasKey(_ => _.Revision);

            modelBuilder.Entity<ProxyRoute>()
                .HasKey(_ => new { _.Id, _.ConfigRevision });

            modelBuilder.Entity<ProxyRouteMatch>()
                .HasKey(_ => new { _.Id, _.ConfigRevision });

            modelBuilder.Entity<ProxyRouteTransform>()
                .HasKey(_ => new { _.Id, _.ConfigRevision });

            modelBuilder.Entity<ProxyCluster>()
                .HasKey(_ => new { _.Id, _.ConfigRevision });

            modelBuilder.Entity<ProxyDestination>()
                .HasKey(_ => new { _.Id, _.ConfigRevision });

            modelBuilder.Entity<ProxySniMap>()
                .HasKey(_ => new { _.Host, _.ConfigRevision });

            modelBuilder.Entity<AcmeCertHost>()
                .HasKey(_ => new { _.CertificateId, _.Host });


            // Relations
            modelBuilder.Entity<AuthRole>()
                .HasMany<UserEntity>()
                .WithMany(user => user.Roles)
                .UsingEntity(_ => _.ToTable("UserRoles"));

            modelBuilder.Entity<UserEntity>()
                .HasMany<AuthSession>()
                .WithOne(_ => _.User)
                .HasForeignKey(_ => _.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProxyCluster>()
                .HasMany<ProxyRoute>()
                .WithOne(_ => _.Cluster)
                .OnDelete(DeleteBehavior.ClientNoAction);                
            
            // AutoIncludes
            modelBuilder.Entity<ProxyConfig>()
                .Navigation(_ => _.Routes)
                .AutoInclude();

            modelBuilder.Entity<ProxyRoute>()
                .Navigation(_ => _.MatchCriteria)
                .AutoInclude();

            modelBuilder.Entity<ProxyRoute>()
                .Navigation(_ => _.Transforms)
                .AutoInclude();

            modelBuilder.Entity<ProxyConfig>()
                .Navigation(_ => _.Clusters)
                .AutoInclude();
            
            modelBuilder.Entity<ProxyCluster>()
                .Navigation(_ => _.Destinations)
                .AutoInclude();

            modelBuilder.Entity<ProxyConfig>()
                .Navigation(_ => _.SniMaps)
                .AutoInclude();

            // Indexes
            modelBuilder.Entity<UserEntity>()
                .HasIndex(_ => _.Username)
                .IsUnique();


            // Discriminators
            modelBuilder.Entity<ProxyRouteMatch>()
                .HasDiscriminator<string>(RouteMatchType.Constants.Discriminator)
                .HasValue<ProxyRouteHeaderMatch>(RouteMatchType.Header.Type)
                .HasValue<ProxyRouteQueryMatch>(RouteMatchType.Query.Type);

            modelBuilder.Entity<ProxyRouteTransform>()
                .HasDiscriminator<string>(RouteTransformType.Constants.Discriminator)
                .HasValue<ProxyRouteMethodTransform>(RouteTransformType.Method.Type)
                .HasValue<ProxyRouteHeaderTransform>(RouteTransformType.Header.Type)
                .HasValue<ProxyRoutePathTransform>(RouteTransformType.Path.Type)
                .HasValue<ProxyRouteQueryTransform>(RouteTransformType.Query.Type);

            modelBuilder.Entity<ProxyCert>()
                .HasDiscriminator<string>(CertType.Constants.Discriminator)
                .HasValue<CACert>(CertType.CA.Type)
                .HasValue<AcmeCert>(CertType.Acme.Type)
                .HasValue<IssuedCert>(CertType.Issued.Type)
                .HasValue<ImportedCert>(CertType.Imported.Type);

            modelBuilder.Entity<AcmeCertHost>()
                .HasDiscriminator(_ => _.ChallengeType)
                .HasValue<AcmeHttpCertHost>(AcmeChallengeType.HTTP_01)
                .HasValue<AcmeDnsCertHost>(AcmeChallengeType.DNS_01);

            // Converters
            modelBuilder.Entity<ProxyRoute>()
                .Property(_ => _.ResponseType)
                .HasConversion<RouteResponseType.RouteResponseTypeConverter>();

            modelBuilder.Entity<ProxyRoute>()
                .Property(_ => _.XForwardedForAction)
                .HasConversion<ForwardedTransformAction.ForwardedTransformActionConverter>();

            modelBuilder.Entity<ProxyRoute>()
                .Property(_ => _.XForwardedProtoAction)
                .HasConversion<ForwardedTransformAction.ForwardedTransformActionConverter>();

            modelBuilder.Entity<ProxyRoute>()
                .Property(_ => _.XForwardedHostAction)
                .HasConversion<ForwardedTransformAction.ForwardedTransformActionConverter>();

            modelBuilder.Entity<ProxyRoute>()
                .Property(_ => _.XForwardedPrefixAction)
                .HasConversion<ForwardedTransformAction.ForwardedTransformActionConverter>();

            modelBuilder.Entity<ProxyCluster>()
                .Property(_ => _.LoadBalancing)
                .HasConversion<LoadBalancingPolicy.LoadBalancingPolicyConverter>();

            modelBuilder.Entity<ProxyRouteHeaderMatch>()
                .Property(_ => _.HeaderMatchMode)
                .HasConversion<ProxyHeaderMatchMode.HeaderMatchModeConverter>();

            modelBuilder.Entity<ProxyRouteQueryMatch>()
                .Property(_ => _.QueryMatchMode)
                .HasConversion<ProxyQueryMatchMode.QueryMatchModeConverter>();

            modelBuilder.Entity<ProxyRouteHeaderTransform>()
                .Property(_ => _.HeaderOperation)
                .HasConversion<HeaderTransformOperation.HeaderTransformOperationConverter>();

            modelBuilder.Entity<ProxyRoutePathTransform>()
                .Property(_ => _.PathMode)
                .HasConversion<PathTransformMode.PathTransformModeConverter>();

            modelBuilder.Entity<ProxyRouteQueryTransform>()
                .Property(_ => _.QueryOperation)
                .HasConversion<QueryTransformOperation.QueryTransformOperationConverter>();

            modelBuilder.Entity<AcmeCert>()
                .Property(_ => _.LastAttempt)
                .HasConversion<DateTimeUtcKindConverter>();            

            // Query Filters
            modelBuilder.Entity<ProxyRoute>()
                .HasQueryFilter(_ => _.ConfigRevision == _proxyState.CandidateConfigRevision);

            modelBuilder.Entity<ProxyRouteMatch>()
                .HasQueryFilter(_ => _.ConfigRevision == _proxyState.CandidateConfigRevision);

            modelBuilder.Entity<ProxyRouteTransform>()
                .HasQueryFilter(_ => _.ConfigRevision == _proxyState.CandidateConfigRevision);

            modelBuilder.Entity<ProxyCluster>()
                .HasQueryFilter(_ => _.ConfigRevision == _proxyState.CandidateConfigRevision);

            modelBuilder.Entity<ProxyDestination>()
                .HasQueryFilter(_ => _.ConfigRevision == _proxyState.CandidateConfigRevision);

            modelBuilder.Entity<ProxySniMap>()
                .HasQueryFilter(_ => _.ConfigRevision == _proxyState.CandidateConfigRevision);
        }
    }
}
