namespace ByProxy.Utility {
    public static class Generators {
        public static string GenerateSessionKey() {
            int keyLength = 64;
            char[] charSet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();
            var result = new StringBuilder();
            for (var i = 0; i < keyLength; ++i) {
                result.Append(charSet[RandomNumberGenerator.GetInt32(charSet.Length)]);
            }
            return result.ToString();
        }
    }
}
