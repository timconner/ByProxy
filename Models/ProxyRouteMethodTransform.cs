namespace ByProxy.Models {
    public class ProxyRouteMethodTransform : ProxyRouteTransform {
        public override RouteTransformType TransformType => RouteTransformType.Method;

        public string FromHttpMethod { get; set; }
        public string ToHttpMethod { get; set; }

        public override ProxyRouteTransform Clone(int newRevision) {
            return new ProxyRouteMethodTransform {
                Id = Id,
                ConfigRevision = newRevision,
                RouteId = RouteId,
                RouteConfigRevision = newRevision,
                FromHttpMethod = FromHttpMethod,
                ToHttpMethod = ToHttpMethod
            };
        }

        public override RouteTransformExport ToExport() {
            return new RouteMethodTransformExport {
                Id = Id,
                RouteId = RouteId,
                FromHttpMethod = FromHttpMethod,
                ToHttpMethod = ToHttpMethod
            };
        }
    }

    public class RouteMethodTransformExport : RouteTransformExport {
        public string FromHttpMethod { get; set; }
        public string ToHttpMethod { get; set; }

        public override ProxyRouteTransform Realize(int revision) {
            return new ProxyRouteMethodTransform {
                Id = Id,
                ConfigRevision = revision,
                RouteId = RouteId,
                RouteConfigRevision = revision,
                FromHttpMethod = FromHttpMethod,
                ToHttpMethod = ToHttpMethod
            };
        }
    }
}
