using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;

internal sealed class InvoiceViewingSynchronizationWorker(
    InvoiceViewingSynchronizationJobQueue queue,
    IServiceScopeFactory scopeFactory,
    InvoiceViewingSynchronizationProgressStore progressStore,
    ILogger<InvoiceViewingSynchronizationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = await queue.DequeueAsync(stoppingToken);

                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var invoiceViewingService = scope.ServiceProvider.GetRequiredService<InvoiceViewingService>();

                    await invoiceViewingService.SynchronizeAsync(request, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    progressStore.Fail("Senkronizasyon uygulama kapanirken iptal edildi.");
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Invoice viewing synchronization failed for {StartDate} - {EndDate}.",
                        request.StartDate,
                        request.EndDate);

                    if (progressStore.Get().IsRunning)
                    {
                        progressStore.Fail(exception.Message);
                    }
                }
                finally
                {
                    queue.MarkCompleted();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
