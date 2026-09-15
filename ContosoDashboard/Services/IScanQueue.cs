using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

public interface IScanQueue
{
    ValueTask EnqueueAsync(DocumentScanMessage message, CancellationToken cancellationToken = default);
}
