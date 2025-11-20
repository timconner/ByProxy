namespace ByProxy.Models {
    public record ForwardedTransformAction(string Action, ForwardedTransformActions EnumValue) {
        public static class Constants {
            public const string Set = "Set";
            public const string Append = "Append";
            public const string Remove = "Remove";
            public const string Ignore = "Ignore";
        }

        public static readonly ForwardedTransformAction Set = new(Constants.Set, ForwardedTransformActions.Set);
        public static readonly ForwardedTransformAction Append = new(Constants.Append, ForwardedTransformActions.Append);
        public static readonly ForwardedTransformAction Remove = new(Constants.Remove, ForwardedTransformActions.Remove);
        public static readonly ForwardedTransformAction Ignore = new(Constants.Ignore, ForwardedTransformActions.Off);

        public static ImmutableArray<ForwardedTransformAction> Actions = [Set, Append, Remove, Ignore];

        public static ForwardedTransformAction? FromString(string? value) => value switch {
            Constants.Append => Append,
            Constants.Remove => Remove,
            Constants.Ignore => Ignore,
            _ => Set
        };

        public class ForwardedTransformActionConverter : ValueConverter<ForwardedTransformAction?, string?> {
            public ForwardedTransformActionConverter() : base(
                forwardedTransformAction => forwardedTransformAction == null ? null : forwardedTransformAction.Action,
                    value => FromString(value)) { }
        }
    }
}
