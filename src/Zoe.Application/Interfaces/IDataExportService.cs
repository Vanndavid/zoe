using Zoe.Domain.Entities;

namespace Zoe.Application.Interfaces;

public interface IDataExportService
{
    Task ExportEventsAsync(string filePath, CancellationToken cancellationToken = default);

    Task DeleteAllDataAsync(CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task ShowInterventionAsync(Intervention intervention, CancellationToken cancellationToken = default);

    Task ShowInfoAsync(string title, string message, CancellationToken cancellationToken = default);
}
