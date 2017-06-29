namespace NuClear.VStore.Host.Options
{
    public sealed class JwtOptions
    {
        public string Issuer { get; set; }
        public string SecretKey { get; set; }
    }
}