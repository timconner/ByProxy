namespace ByProxy.Models {
    public class ProxyRouteMethodTransform : ProxyRouteTransform {
        public override RouteTransformType TransformType => RouteTransformType.Method;

        public string FromHttpMethod { get; set; }
        public string ToHttpMethod { get; set; }

        public override ProxyRouteTransform Clone(int newRevision) {
            return new ProxyRouteMethodTransform() {
                Id = Id,
                ConfigRevision = newRevision,
                RouteId = RouteId,
                RouteConfigRevision = newRevision,
                FromHttpMethod = FromHttpMethod,
                ToHttpMethod = ToHttpMethod
            };
        }

        public override dynamic ToComparable() {
            return new {
                Id = Id,
                Type = TransformType.Type,
                FromHttpMethod = FromHttpMethod,
                ToHttpMethod = ToHttpMethod
            };
        }
    }
}
