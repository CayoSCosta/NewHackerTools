using HackerToolbox.Services;

namespace HackerToolbox.Models;

public sealed class ShopeeDashboardViewModel
{
    public bool IsConfigured { get; init; }
    public long Offset { get; init; }
    public ShopeeProductPage? ProductPage { get; init; }
    public string? ErrorMessage { get; init; }
}
