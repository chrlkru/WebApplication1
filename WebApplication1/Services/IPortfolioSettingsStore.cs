using WebApplication1.Models;

namespace WebApplication1.Services;

public interface IPortfolioSettingsStore
{
    Task<PortfolioSettings> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(PortfolioSettings settings, CancellationToken cancellationToken = default);
}
