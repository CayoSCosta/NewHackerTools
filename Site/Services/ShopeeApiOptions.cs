namespace HackerToolbox.Services;

public sealed class ShopeeApiOptions
{
    public long PartnerId { get; set; }
    public string PartnerKey { get; set; } = string.Empty;
    public long ShopId { get; set; }
    public string AccessToken { get; set; } = string.Empty;

    public bool IsConfigured =>
        PartnerId is > 0 and <= uint.MaxValue &&
        ShopId > 0 &&
        !string.IsNullOrWhiteSpace(PartnerKey) &&
        !string.IsNullOrWhiteSpace(AccessToken);
}
