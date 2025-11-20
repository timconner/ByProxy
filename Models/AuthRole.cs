namespace ByProxy.Models {
    public class AuthRole {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public static class AuthRoles {
        public const string User = "User";

        public const string Admin = "Admin";
        public const string Editor = "Editor";

        public static readonly ReadOnlyCollection<string> AssignableRoles = new(
            new List<string>() {
                Admin, Editor
            }
        );
    }
}
