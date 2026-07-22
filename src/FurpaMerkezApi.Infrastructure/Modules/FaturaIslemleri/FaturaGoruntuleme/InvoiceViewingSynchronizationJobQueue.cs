using System.Threading.Channels;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;

namespace FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;

internal sealed class InvoiceViewingSynchronizationJobQueue(
    InvoiceViewingSynchronizationProgressStore progressStore)
{
    private const int PageSize = 20;
    private readonly Channel<InvoiceViewingSynchronizationRequest> channel =
        Channel.CreateUnbounded<InvoiceViewingSynchronizationRequest>();
    private readonly object gate = new();
    private bool hasActiveWork;

    public InvoiceViewingSynchronizationProgressResponse Enqueue(InvoiceViewingSynchronizationRequest request)
    {
        TryEnqueue(request, out var progress);

        return progress;
    }

    public bool TryEnqueue(
        InvoiceViewingSynchronizationRequest request,
        out InvoiceViewingSynchronizationProgressResponse progress)
    {
        lock (gate)
        {
            var currentProgress = progressStore.Get();

            if (hasActiveWork || currentProgress.IsRunning)
            {
                progress = currentProgress;
                return false;
            }

            hasActiveWork = true;
            progress = progressStore.Queue(
                request.StartDate,
                request.EndDate,
                request.IncludeStatuses,
                PageSize);

            channel.Writer.TryWrite(request);

            return true;
        }
    }

    public ValueTask<InvoiceViewingSynchronizationRequest> DequeueAsync(CancellationToken cancellationToken) =>
        channel.Reader.ReadAsync(cancellationToken);

    public void MarkCompleted()
    {
        lock (gate)
        {
            hasActiveWork = false;
        }
    }
}
