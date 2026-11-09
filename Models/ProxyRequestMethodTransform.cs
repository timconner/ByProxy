namespace ByProxy.Models {
    public class ProxyRequestMethodTransform : ProxyRequestTransform {
        public override RequestTransformType TransformType => RequestTransformType.Method;

        public string FromHttpMethod { get; set; }
        public string ToHttpMethod { get; set; }

        public override ProxyRequestTransform Clone(int newRevision) {
            return new ProxyRequestMethodTransform {
                Id = Id,
                ConfigRevision = newRevision,
                RouteId = RouteId,
                RouteConfigRevision = newRevision,
                FromHttpMethod = FromHttpMethod,
                ToHttpMethod = ToHttpMethod
            };
        }

        public override RequestTransformExport ToExport() {
            return new RequestMethodTransformExport {
                Id = Id,
                RouteId = RouteId,
                FromHttpMethod = FromHttpMethod,
                ToHttpMethod = ToHttpMethod
            };
        }
    }

    public class RequestMethodTransformExport : RequestTransformExport {
        public string FromHttpMethod { get; set; }
        public string ToHttpMethod { get; set; }

        public override ProxyRequestTransform Realize(int revision) {
            return new ProxyRequestMethodTransform {
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
