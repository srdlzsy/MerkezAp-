namespace FurpaMerkezApi.Application.Modules.Common.OfflineSync;

public interface IExplicitNewCreateRequest
{
    bool ForceCreateNew { get; }
}
