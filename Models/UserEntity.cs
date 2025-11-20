namespace ByProxy.Models {
    public class UserEntity {
        public Guid Id { get; set; }
        
        public string FullName { get; set; }
        public string DisplayName { get; set; }

        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public bool PasswordResetRequired { get; set; }
        public DateTime PasswordLastSet { get; set; }
        public ICollection<AuthRole> Roles { get; set; }
        public string? Culture { get; set; }
        public string? PreferredTheme { get; set; }
    }
}
