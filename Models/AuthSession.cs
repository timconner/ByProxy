namespace ByProxy.Models {
    public class AuthSession {
        public string Key { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }

        public Guid? UserId { get; set; }
        public UserEntity User { get; set; }
    }
}
