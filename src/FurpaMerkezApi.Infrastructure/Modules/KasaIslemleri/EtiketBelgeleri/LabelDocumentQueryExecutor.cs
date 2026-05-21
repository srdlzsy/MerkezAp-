using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed class LabelDocumentQueryExecutor(FurpaDbContext furpaDbContext)
{
    internal async Task<IReadOnlyCollection<LabelDocumentListItemDto>> ListAsync(
        LabelDocumentListRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo is <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (request.Take is <= 0)
        {
            throw new ArgumentException("Take must be greater than zero.", nameof(request.Take));
        }

        var query = furpaDbContext.LabelDocuments
            .AsNoTracking()
            .OrderByDescending(document => document.CreateDate)
            .ThenByDescending(document => document.Id)
            .AsQueryable();

        if (request.WarehouseNo.HasValue)
        {
            query = query.Where(document => document.BranchNo == request.WarehouseNo.Value);
        }

        if (request.Take.HasValue)
        {
            query = query.Take(request.Take.Value);
        }

        return await query
            .Select(document => new LabelDocumentListItemDto(
                document.Id,
                document.CreateDate,
                document.BranchNo))
            .ToArrayAsync(cancellationToken);
    }

    internal async Task<IReadOnlyCollection<string>> GetDetailProductCodesAsync(
        LabelDocumentDetailRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (request.DocumentId <= 0)
        {
            throw new ArgumentException("Document id must be greater than zero.", nameof(request.DocumentId));
        }

        var documentExists = await furpaDbContext.LabelDocuments
            .AsNoTracking()
            .AnyAsync(
                document => document.Id == request.DocumentId && document.BranchNo == request.WarehouseNo,
                cancellationToken);

        if (!documentExists)
        {
            throw new KeyNotFoundException("Label document was not found.");
        }

        return await furpaDbContext.LabelDocumentDetails
            .AsNoTracking()
            .Where(detail => detail.DocumentId == request.DocumentId)
            .OrderBy(detail => detail.DetailId)
            .Where(detail => detail.ProductCode != null && detail.ProductCode != string.Empty)
            .Select(detail => detail.ProductCode.Trim())
            .Where(productCode => productCode != string.Empty)
            .ToArrayAsync(cancellationToken);
    }
}
