using System.Collections.Concurrent;

namespace TradeMind.AI.Application;

public sealed class InMemoryPromptTemplateRegistry : IPromptTemplateRegistry
{
    private readonly ConcurrentDictionary<string, IReadOnlyList<PromptTemplate>> _templates;

    public InMemoryPromptTemplateRegistry(IEnumerable<PromptTemplate> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);

        var grouped = templates
            .GroupBy(template => template.Id.Value, StringComparer.Ordinal)
            .ToArray();

        var dictionary = new ConcurrentDictionary<string, IReadOnlyList<PromptTemplate>>(StringComparer.Ordinal);
        foreach (var group in grouped)
        {
            var duplicates = group
                .GroupBy(template => template.Version)
                .Where(versionGroup => versionGroup.Count() > 1)
                .Select(versionGroup => versionGroup.Key)
                .ToArray();

            if (duplicates.Length > 0)
            {
                throw new ArgumentException($"Prompt template '{group.Key}' contains duplicate version '{duplicates[0]}'.", nameof(templates));
            }

            dictionary[group.Key] = group
                .OrderBy(template => template.Version)
                .ToArray();
        }

        _templates = dictionary;
    }

    public Task<PromptTemplate> GetAsync(
        PromptTemplateId templateId,
        PromptTemplateVersion? version,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return version is null
            ? GetLatestAsync(templateId, cancellationToken)
            : Task.FromResult(GetVersion(templateId, version));
    }

    public Task<PromptTemplate> GetLatestAsync(
        PromptTemplateId templateId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(templateId);

        if (!_templates.TryGetValue(templateId.Value, out var versions))
        {
            throw new PromptTemplateNotFoundException(templateId);
        }

        var latest = versions
            .Where(template => template.IsActive)
            .MaxBy(template => template.Version);

        if (latest is null)
        {
            throw new PromptTemplateNotFoundException(templateId);
        }

        return Task.FromResult(latest);
    }

    private PromptTemplate GetVersion(PromptTemplateId templateId, PromptTemplateVersion version)
    {
        ArgumentNullException.ThrowIfNull(templateId);
        ArgumentNullException.ThrowIfNull(version);

        if (!_templates.TryGetValue(templateId.Value, out var versions))
        {
            throw new PromptTemplateNotFoundException(templateId);
        }

        return versions.FirstOrDefault(template => template.Version == version)
            ?? throw new PromptTemplateVersionNotFoundException(templateId, version);
    }
}
