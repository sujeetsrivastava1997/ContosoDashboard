using System.Threading.Channels;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

public sealed class LocalScanQueue : BackgroundService, IScanQueue
{
    private readonly Channel<DocumentScanMessage> _messages = Channel.CreateUnbounded<DocumentScanMessage>();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LocalScanQueue> _logger;

    public LocalScanQueue(IServiceScopeFactory scopeFactory, ILogger<LocalScanQueue> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public ValueTask EnqueueAsync(DocumentScanMessage message, CancellationToken cancellationToken = default)
        => _messages.Writer.WriteAsync(message, cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _messages.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IDocumentScanProcessor>();
                await processor.ProcessAsync(message, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Document scan processing failed for document {DocumentId}.", message.DocumentId);
            }
        }
    }
}
