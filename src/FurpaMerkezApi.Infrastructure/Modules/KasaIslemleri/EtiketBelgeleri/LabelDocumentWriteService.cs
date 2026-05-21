using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;
using Microsoft.EntityFrameworkCore;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed class LabelDocumentWriteService(FurpaDbContext furpaDbContext)
{
    internal async Task<CreateLabelDocumentResponse> ExecuteAsync(
        CreateLabelDocumentRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var executionStrategy = furpaDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await furpaDbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var now = DateTime.Now;
                var document = new LabelDocumentEntity
                {
                    CreateDate = now,
                    BranchNo = request.WarehouseNo
                };

                await furpaDbContext.LabelDocuments.AddAsync(document, cancellationToken);
                await furpaDbContext.SaveChangesAsync(cancellationToken);

                var details = request.Lines
                    .Select(line => new LabelDocumentDetailEntity
                    {
                        DocumentId = document.Id,
                        ProductCode = NormalizeText(line.ProductCode)
                    })
                    .ToArray();

                await furpaDbContext.LabelDocumentDetails.AddRangeAsync(details, cancellationToken);
                await furpaDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new CreateLabelDocumentResponse(
                    document.Id,
                    document.CreateDate,
                    document.BranchNo,
                    details.Length);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private static void Validate(CreateLabelDocumentRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ArgumentException("At least one label document line is required.", nameof(request.Lines));
        }

        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.ProductCode))
            {
                throw new ArgumentException("Product code is required.", nameof(request.Lines));
            }
        }
    }

    private static string NormalizeText(string value) =>
        value.Trim();
}
