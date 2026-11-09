namespace ByProxy.AdminApp.Utility {
    public static class Functions {
        public static string GetHumanReadableBytes(long bytes, bool useSI = true) {
            var unit = useSI ? 1000 : 1024;
            if (bytes < unit) { return $"{bytes} Bytes"; }

            var exp = (int)(Math.Log(bytes) / Math.Log(unit));
            return $"{bytes / Math.Pow(unit, exp):F2} {("KMGTPE")[exp - 1]}B";
        }
    }
}
