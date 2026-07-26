using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradeMind.AI.Abstractions;
using TradeMind.AI.Application;
using TradeMind.AI.Memory;

namespace TradeMind.AI.Tests;

public sealed class MemoryEngineTests
{
    [Fact]
    public void ConversationMemoryKey_ShouldValidateAndIsolateTenantAndUser()
    {
        Assert.Throws<ArgumentException>(() => new ConversationMemoryKey(" "));
        Assert.NotEqual(new ConversationMemoryKey("c", "tenant-a"), new ConversationMemoryKey("c", "tenant-b"));
        Assert.NotEqual(new ConversationMemoryKey("c", "tenant", "user-a"), new ConversationMemoryKey("c", "tenant", "user-b"));
        Assert.Equal(new ConversationMemoryKey(" c ", " tenant ", " user "), new ConversationMemoryKey("c", "tenant", "user"));
    }

    [Fact]
    public void ConversationMemoryEntry_ShouldValidateRequiredFields()
    {
        var utc = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => Entry(content: " "));
        Assert.Throws<ArgumentOutOfRangeException>(() => Entry(sequenceNumber: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Entry(tokenCount: -1));
        Assert.Throws<ArgumentException>(() => Entry(createdAtUtc: new DateTimeOffset(2026, 7, 18, 10, 0, 0, TimeSpan.FromHours(1))));

        var entry = Entry(createdAtUtc: utc, correlationId: "corr", sessionId: "session");
        Assert.Equal("corr", entry.CorrelationId);
        Assert.Equal("session", entry.SessionId);
        Assert.Equal(utc, entry.CreatedAtUtc);
    }

    [Fact]
    public void MemoryWindowOptions_ShouldValidateCoherentValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MemoryWindowOptions(maxEntries: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MemoryWindowOptions(maxCharacters: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MemoryWindowOptions(maxEstimatedTokens: 0));
        Assert.Throws<ArgumentException>(() => new MemoryWindowOptions(maxEntries: 1, recentUserMessagesMinimum: 1, recentAssistantMessagesMinimum: 1));

        var options = new MemoryWindowOptions(maxEntries: 4, maxCharacters: 100, maxEstimatedTokens: 100);
        Assert.Equal(4, options.MaxEntries);
    }

    [Fact]
    public async Task Store_ShouldAppendEntriesWithMonotonicSequencesAndReturnOrderedImmutableSnapshot()
    {
        var store = Store();
        var key = Key();

        var first = await store.AppendAsync(Write(key, ConversationMemoryRole.User, "first"), CancellationToken.None);
        var second = await store.AppendAsync(Write(key, ConversationMemoryRole.Assistant, "second"), CancellationToken.None);
        var memory = await store.GetAsync(key, CancellationToken.None);

        Assert.Equal(1, first.SequenceNumber);
        Assert.Equal(2, second.SequenceNumber);
        Assert.Equal(["first", "second"], memory?.Entries.Select(entry => entry.Content));
        Assert.IsAssignableFrom<IReadOnlyList<ConversationMemoryEntry>>(memory!.Entries);
    }

    [Fact]
    public async Task Store_ShouldIsolateConversationTenantAndUser()
    {
        var store = Store();
        var first = new ConversationMemoryKey("conversation", "tenant-a", "user-a");
        var second = new ConversationMemoryKey("conversation", "tenant-b", "user-a");
        var third = new ConversationMemoryKey("conversation", "tenant-a", "user-b");

        await store.AppendAsync(Write(first, ConversationMemoryRole.User, "first"), CancellationToken.None);
        await store.AppendAsync(Write(second, ConversationMemoryRole.User, "second"), CancellationToken.None);
        await store.AppendAsync(Write(third, ConversationMemoryRole.User, "third"), CancellationToken.None);

        Assert.Equal("first", (await store.GetAsync(first, CancellationToken.None))!.Entries.Single().Content);
        Assert.Equal("second", (await store.GetAsync(second, CancellationToken.None))!.Entries.Single().Content);
        Assert.Equal("third", (await store.GetAsync(third, CancellationToken.None))!.Entries.Single().Content);
    }

    [Fact]
    public async Task Store_ShouldAppendRangeSaveSummaryDeleteAndReportExists()
    {
        var store = Store();
        var key = Key();
        var entries = await store.AppendRangeAsync(
            key,
            [
                Write(key, ConversationMemoryRole.User, "one"),
                Write(key, ConversationMemoryRole.Assistant, "two")
            ],
            CancellationToken.None);
        var summary = new ConversationSummary("summary", 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1);

        await store.SaveSummaryAsync(key, summary, CancellationToken.None);

        Assert.Equal([1, 2], entries.Select(entry => entry.SequenceNumber));
        Assert.True(await store.ExistsAsync(key, CancellationToken.None));
        Assert.Equal("summary", (await store.GetAsync(key, CancellationToken.None))!.Summary?.Content);
        Assert.True(await store.DeleteAsync(key, CancellationToken.None));
        Assert.False(await store.ExistsAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task Store_ShouldRespectCancellationToken()
    {
        var store = Store();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            store.AppendAsync(Write(Key(), ConversationMemoryRole.User, "content"), source.Token));
    }

    [Fact]
    public async Task Store_ShouldNotLoseConcurrentEntriesOrDuplicateSequences()
    {
        var store = Store();
        var key = Key();
        var writes = Enumerable.Range(0, 100)
            .Select(index => store.AppendAsync(Write(key, ConversationMemoryRole.User, $"message {index}"), CancellationToken.None));

        await Task.WhenAll(writes);
        var memory = await store.GetAsync(key, CancellationToken.None);

        Assert.Equal(100, memory!.Entries.Count);
        Assert.Equal(100, memory.Entries.Select(entry => entry.SequenceNumber).Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 100).Select(value => (long)value), memory.Entries.Select(entry => entry.SequenceNumber));
    }

    [Fact]
    public async Task Store_ShouldAllowDifferentConversationsToProgressIndependently()
    {
        var store = Store();
        var first = Key("first");
        var second = Key("second");

        await Task.WhenAll(
            store.AppendAsync(Write(first, ConversationMemoryRole.User, "a"), CancellationToken.None),
            store.AppendAsync(Write(second, ConversationMemoryRole.User, "b"), CancellationToken.None));

        Assert.True(await store.ExistsAsync(first, CancellationToken.None));
        Assert.True(await store.ExistsAsync(second, CancellationToken.None));
    }

    [Fact]
    public async Task Reader_ShouldReturnRecentEntriesInChronologicalOrderAndRespectMaxEntries()
    {
        var (store, reader, key) = await ReaderWithEntriesAsync(6);

        var result = await reader.ReadAsync(
            new MemoryReadRequest(key, new MemoryWindowOptions(maxEntries: 3, recentUserMessagesMinimum: 0, recentAssistantMessagesMinimum: 0)),
            CancellationToken.None);

        Assert.Equal([4, 5, 6], result.SelectedEntries.Select(entry => entry.SequenceNumber));
        Assert.True(result.Truncated);
        Assert.Equal(4, result.OldestIncludedSequence);
        Assert.Equal(6, result.NewestIncludedSequence);
        Assert.Equal(6, result.TotalAvailableEntries);
    }

    [Fact]
    public async Task Reader_ShouldRespectCharacterAndTokenWindows()
    {
        var store = Store();
        var estimator = new CharacterBasedTokenEstimator(charactersPerToken: 2);
        var reader = new MemoryReader(store, estimator);
        var key = Key();

        await store.AppendAsync(Write(key, ConversationMemoryRole.User, "aaaa"), CancellationToken.None);
        await store.AppendAsync(Write(key, ConversationMemoryRole.Assistant, "bbbb"), CancellationToken.None);
        await store.AppendAsync(Write(key, ConversationMemoryRole.User, "cccc"), CancellationToken.None);

        var characterResult = await reader.ReadAsync(
            new MemoryReadRequest(key, new MemoryWindowOptions(maxEntries: 5, maxCharacters: 4, recentUserMessagesMinimum: 0, recentAssistantMessagesMinimum: 0)),
            CancellationToken.None);
        var tokenResult = await reader.ReadAsync(
            new MemoryReadRequest(key, new MemoryWindowOptions(maxEntries: 5, maxCharacters: null, maxEstimatedTokens: 2, recentUserMessagesMinimum: 0, recentAssistantMessagesMinimum: 0)),
            CancellationToken.None);

        Assert.Single(characterResult.SelectedEntries);
        Assert.Single(tokenResult.SelectedEntries);
    }

    [Fact]
    public async Task Reader_ShouldIncludeOrExcludeSummaryAndSkipCoveredEntries()
    {
        var store = Store();
        var reader = new MemoryReader(store, new CharacterBasedTokenEstimator());
        var key = Key();
        for (var index = 1; index <= 4; index++)
        {
            await store.AppendAsync(Write(key, ConversationMemoryRole.User, $"message {index}"), CancellationToken.None);
        }

        await store.SaveSummaryAsync(
            key,
            new ConversationSummary("covered", 2, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 2),
            CancellationToken.None);

        var withSummary = await reader.ReadAsync(
            new MemoryReadRequest(key, new MemoryWindowOptions(maxEntries: 10, includeSummary: true, recentUserMessagesMinimum: 0, recentAssistantMessagesMinimum: 0)),
            CancellationToken.None);
        var withoutSummary = await reader.ReadAsync(
            new MemoryReadRequest(key, new MemoryWindowOptions(maxEntries: 10, includeSummary: false, recentUserMessagesMinimum: 0, recentAssistantMessagesMinimum: 0)),
            CancellationToken.None);

        Assert.NotNull(withSummary.Summary);
        Assert.Equal([3, 4], withSummary.SelectedEntries.Select(entry => entry.SequenceNumber));
        Assert.Null(withoutSummary.Summary);
    }

    [Fact]
    public async Task Reader_ShouldKeepConfiguredRecentUserAndAssistantMinimums()
    {
        var (store, reader, key) = await ReaderWithEntriesAsync(4);

        var result = await reader.ReadAsync(
            new MemoryReadRequest(key, new MemoryWindowOptions(maxEntries: 2, maxCharacters: 1, recentUserMessagesMinimum: 1, recentAssistantMessagesMinimum: 1)),
            CancellationToken.None);

        Assert.Contains(result.SelectedEntries, entry => entry.Role == ConversationMemoryRole.User);
        Assert.Contains(result.SelectedEntries, entry => entry.Role == ConversationMemoryRole.Assistant);
        Assert.NotNull(store);
    }

    [Fact]
    public async Task Store_ShouldApplyExpirationSlidingExpirationAndRemoveExpiredOnAccess()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 7, 18, 10, 0, 0, TimeSpan.Zero));
        var key = Key();
        var store = Store(new MemoryRetentionOptions(TimeSpan.FromMinutes(1), slidingExpiration: true, removeExpiredOnAccess: true), time);

        await store.AppendAsync(Write(key, ConversationMemoryRole.User, "content"), CancellationToken.None);
        Assert.NotNull(await store.GetAsync(key, CancellationToken.None));

        var extended = (await store.GetAsync(key, CancellationToken.None))!.ExpiresAtUtc;
        time.Advance(TimeSpan.FromMinutes(2));

        Assert.Null(await store.GetAsync(key, CancellationToken.None));
        Assert.False(await store.ExistsAsync(key, CancellationToken.None));
        Assert.True(extended > time.InitialUtc);
    }

    [Fact]
    public async Task Summarizer_ShouldProduceDeterministicBoundedSummaryWithPreviousSummary()
    {
        var time = new MutableTimeProvider(new DateTimeOffset(2026, 7, 18, 10, 0, 0, TimeSpan.Zero));
        var summarizer = new DeterministicConversationSummarizer(new CharacterBasedTokenEstimator(), time);
        var key = Key();
        var previous = new ConversationSummary("previous", 1, time.GetUtcNow(), time.GetUtcNow(), 1);

        var summary = await summarizer.SummarizeAsync(
            new ConversationSummaryRequest(key, previous, [Entry(content: "new", sequenceNumber: 2)], 200),
            CancellationToken.None);

        Assert.Contains("previous", summary.Content, StringComparison.Ordinal);
        Assert.Equal(2, summary.SummarizedThroughSequence);
        Assert.Equal(2, summary.Version);
        Assert.Equal("deterministic-local", summary.ModelName);
    }

    [Fact]
    public async Task Writer_ShouldTriggerCompactionAtThresholdAndNotBefore()
    {
        var store = Store();
        var writer = Writer(store, new MemoryCompactionOptions(summarizeAfterEntryCount: 3, entriesToKeepAfterSummary: 1));
        var key = Key();

        await writer.WriteUserMessageAsync(Write(key, ConversationMemoryRole.User, "one"), CancellationToken.None);
        await writer.WriteAssistantMessageAsync(Write(key, ConversationMemoryRole.Assistant, "two"), CancellationToken.None);
        Assert.Null((await store.GetAsync(key, CancellationToken.None))!.Summary);

        await writer.WriteUserMessageAsync(Write(key, ConversationMemoryRole.User, "three"), CancellationToken.None);

        var memory = await store.GetAsync(key, CancellationToken.None);
        Assert.NotNull(memory!.Summary);
        Assert.Equal(2, memory.Summary!.SummarizedThroughSequence);
        Assert.Equal(3, memory.Entries.Count);
    }

    [Fact]
    public async Task Writer_ShouldWriteUserAndAssistantOnceAndPropagateSessionIdentifiers()
    {
        var store = Store();
        var writer = Writer(store, new MemoryCompactionOptions(enabled: false));
        var key = Key();

        await writer.WriteUserMessageAsync(Write(key, ConversationMemoryRole.User, "user", "corr", "session"), CancellationToken.None);
        await writer.WriteAssistantMessageAsync(Write(key, ConversationMemoryRole.Assistant, "assistant", "corr", "session"), CancellationToken.None);

        var memory = await store.GetAsync(key, CancellationToken.None);
        Assert.Equal([ConversationMemoryRole.User, ConversationMemoryRole.Assistant], memory!.Entries.Select(entry => entry.Role));
        Assert.All(memory.Entries, entry => Assert.Equal("session", entry.SessionId));
        Assert.All(memory.Entries, entry => Assert.Equal("corr", entry.CorrelationId));
    }

    [Fact]
    public async Task MemoryLogs_ShouldNotContainSensitiveContent()
    {
        var loggerProvider = new RecordingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(loggerProvider));
        services.AddTradeMindMemory(compactionOptions: new MemoryCompactionOptions(enabled: false));
        using var provider = services.BuildServiceProvider(validateScopes: true);

        await provider.GetRequiredService<IMemoryWriter>()
            .WriteUserMessageAsync(Write(Key(), ConversationMemoryRole.User, "super secret", isSensitive: true), CancellationToken.None);

        Assert.DoesNotContain("super secret", string.Join(Environment.NewLine, loggerProvider.Messages), StringComparison.Ordinal);
    }

    [Fact]
    public void DependencyInjection_ShouldResolveMemoryServicesAndUseSharedStore()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTradeMindMemory();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(provider.GetRequiredService<IMemoryStore>());
        Assert.NotNull(provider.GetRequiredService<IMemoryReader>());
        Assert.NotNull(provider.GetRequiredService<IMemoryWriter>());
        Assert.NotNull(provider.GetRequiredService<IConversationSummarizer>());
        Assert.NotNull(provider.GetRequiredService<ITokenEstimator>());
        Assert.Same(provider.GetRequiredService<IMemoryStore>(), provider.GetRequiredService<IMemoryStore>());
    }

    [Fact]
    public void DependencyInjection_ShouldBeCompatibleWithAIOrchestrationAndRemainOptional()
    {
        var withoutMemory = new ServiceCollection();
        withoutMemory.AddLogging();
        withoutMemory.AddSingleton<IChatProvider>(new FakeChatProvider());
        withoutMemory.AddSingleton<IAIProviderMetadata>(new FakeProviderMetadata());
        withoutMemory.AddTradeMindAIOrchestration();
        using var providerWithoutMemory = withoutMemory.BuildServiceProvider(validateScopes: true);

        var withMemory = new ServiceCollection();
        withMemory.AddLogging();
        withMemory.AddSingleton<IChatProvider>(new FakeChatProvider());
        withMemory.AddSingleton<IAIProviderMetadata>(new FakeProviderMetadata());
        withMemory.AddTradeMindAIOrchestration();
        withMemory.AddTradeMindMemory();
        using var providerWithMemory = withMemory.BuildServiceProvider(validateScopes: true);

        Assert.Equal(5, providerWithoutMemory.GetServices<IAIOrchestrationStep>().Count());
        Assert.Equal(7, providerWithMemory.GetServices<IAIOrchestrationStep>().Count());
        Assert.NotNull(providerWithMemory.GetRequiredService<IAIOrchestrator>());
    }

    [Fact]
    public async Task Orchestrator_ShouldNotAccessMemoryWhenDisabled()
    {
        var chatProvider = new FakeChatProvider();
        using var provider = CreateOrchestrationProvider(chatProvider);

        await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(Request(useMemory: false), CancellationToken.None);

        Assert.False(await provider.GetRequiredService<IMemoryStore>().ExistsAsync(Key("conversation"), CancellationToken.None));
        Assert.Equal(1, chatProvider.CallCount);
    }

    [Fact]
    public async Task Orchestrator_ShouldRequireConversationIdWhenMemoryEnabled()
    {
        using var provider = CreateOrchestrationProvider(new FakeChatProvider());

        await Assert.ThrowsAsync<AIOrchestrationValidationException>(() =>
            provider.GetRequiredService<IAIOrchestrator>()
                .ExecuteAsync(Request(useMemory: true, conversationId: null), CancellationToken.None));
    }

    [Fact]
    public async Task Orchestrator_ShouldInjectHistoryBeforeProviderAndWriteAfterSuccess()
    {
        var chatProvider = new FakeChatProvider();
        using var provider = CreateOrchestrationProvider(chatProvider);
        var store = provider.GetRequiredService<IMemoryStore>();
        var key = Key("conversation");
        await store.AppendAsync(Write(key, ConversationMemoryRole.User, "previous user"), CancellationToken.None);
        await store.AppendAsync(Write(key, ConversationMemoryRole.Assistant, "previous assistant"), CancellationToken.None);

        var response = await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(Request(useMemory: true), CancellationToken.None);

        Assert.Equal("response", response.Content);
        Assert.Equal(
            [ChatRole.System, ChatRole.User, ChatRole.Assistant, ChatRole.User],
            chatProvider.LastRequest!.Messages.Select(message => message.Role));
        Assert.Equal("current user", chatProvider.LastRequest.Messages.Last().Content);
        Assert.Equal(4, (await store.GetAsync(key, CancellationToken.None))!.Entries.Count);
    }

    [Fact]
    public async Task Orchestrator_ShouldInjectSummaryAndKeepPromptEnginePathFunctional()
    {
        var chatProvider = new FakeChatProvider();
        using var provider = CreateOrchestrationProvider(chatProvider);
        var key = Key("conversation");
        var store = provider.GetRequiredService<IMemoryStore>();
        await store.AppendAsync(Write(key, ConversationMemoryRole.User, "old"), CancellationToken.None);
        await store.SaveSummaryAsync(key, new ConversationSummary("summary", 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1), CancellationToken.None);

        await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(Request(useMemory: true, promptEngine: true), CancellationToken.None);

        Assert.Contains(chatProvider.LastRequest!.Messages, message => message.Content.Contains("summary", StringComparison.Ordinal));
        Assert.Equal("template user", chatProvider.LastRequest.Messages.Last().Content);
    }

    [Fact]
    public async Task Orchestrator_ShouldNotWriteAssistantWhenProviderFailsOrPromptRenderingFails()
    {
        using var providerWithProviderFailure = CreateOrchestrationProvider(new FakeChatProvider(new InvalidOperationException("provider failed")));
        await Assert.ThrowsAsync<AIOrchestrationException>(() =>
            providerWithProviderFailure.GetRequiredService<IAIOrchestrator>()
                .ExecuteAsync(Request(useMemory: true), CancellationToken.None));
        Assert.False(await providerWithProviderFailure.GetRequiredService<IMemoryStore>().ExistsAsync(Key("conversation"), CancellationToken.None));

        using var providerWithPromptFailure = CreateOrchestrationProvider(new FakeChatProvider());
        await Assert.ThrowsAsync<PromptValidationException>(() =>
            providerWithPromptFailure.GetRequiredService<IAIOrchestrator>()
                .ExecuteAsync(Request(useMemory: true, promptEngine: true, invalidPrompt: true), CancellationToken.None));
        Assert.False(await providerWithPromptFailure.GetRequiredService<IMemoryStore>().ExistsAsync(Key("conversation"), CancellationToken.None));
    }

    [Fact]
    public async Task Orchestrator_ShouldPropagateTenantUserConversationAndCancellationToken()
    {
        var chatProvider = new FakeChatProvider();
        using var provider = CreateOrchestrationProvider(chatProvider);
        using var source = new CancellationTokenSource();

        await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(Request(useMemory: true, tenantId: "tenant", userId: "user"), source.Token);

        Assert.Equal(source.Token, chatProvider.LastCancellationToken);
        Assert.True(await provider.GetRequiredService<IMemoryStore>()
            .ExistsAsync(new ConversationMemoryKey("conversation", "tenant", "user"), CancellationToken.None));
    }

    [Fact]
    public async Task Orchestrator_ShouldContinueWithoutMemoryInFailOpenMode()
    {
        var chatProvider = new FakeChatProvider();
        var services = BaseOrchestrationServices(chatProvider);
        services.AddSingleton<IMemoryReader>(new ThrowingMemoryReader());
        services.AddTradeMindMemory(orchestrationOptions: new MemoryOrchestrationOptions(MemoryFailureMode.ContinueWithoutMemory));
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var response = await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(Request(useMemory: true), CancellationToken.None);

        Assert.Equal("response", response.Content);
        Assert.Equal(1, chatProvider.CallCount);
    }

    [Fact]
    public async Task Orchestrator_ShouldFailClosedByDefaultWhenMemoryReadFails()
    {
        var services = BaseOrchestrationServices(new FakeChatProvider());
        services.AddSingleton<IMemoryReader>(new ThrowingMemoryReader());
        services.AddTradeMindMemory(orchestrationOptions: new MemoryOrchestrationOptions(MemoryFailureMode.FailClosed));
        using var provider = services.BuildServiceProvider(validateScopes: true);

        await Assert.ThrowsAsync<AIOrchestrationException>(() =>
            provider.GetRequiredService<IAIOrchestrator>()
                .ExecuteAsync(Request(useMemory: true), CancellationToken.None));
    }

    [Fact]
    public async Task Orchestrator_ShouldExposeMemoryStepsAndKeepMetrics()
    {
        using var provider = CreateOrchestrationProvider(new FakeChatProvider());

        var response = await provider.GetRequiredService<IAIOrchestrator>()
            .ExecuteAsync(Request(useMemory: true), CancellationToken.None);

        Assert.Contains(AIOrchestrationStepNames.MemoryRead, response.ExecutedSteps);
        Assert.Contains(AIOrchestrationStepNames.MemoryWrite, response.ExecutedSteps);
        Assert.Contains(AIOrchestrationStepNames.PromptConstruction, response.ExecutedSteps);
        Assert.True(response.TotalDuration >= TimeSpan.Zero);
        Assert.NotNull(response.ProviderDuration);
    }

    private static async Task<(IMemoryStore Store, IMemoryReader Reader, ConversationMemoryKey Key)> ReaderWithEntriesAsync(int count)
    {
        var store = Store();
        var reader = new MemoryReader(store, new CharacterBasedTokenEstimator());
        var key = Key();
        for (var index = 1; index <= count; index++)
        {
            var role = index % 2 == 0 ? ConversationMemoryRole.Assistant : ConversationMemoryRole.User;
            await store.AppendAsync(Write(key, role, $"message {index}"), CancellationToken.None);
        }

        return (store, reader, key);
    }

    private static InMemoryMemoryStore Store(
        MemoryRetentionOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        return new InMemoryMemoryStore(options ?? new MemoryRetentionOptions(), timeProvider ?? TimeProvider.System);
    }

    private static MemoryWriter Writer(IMemoryStore store, MemoryCompactionOptions options)
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new RecordingLoggerProvider()));
        return new MemoryWriter(
            store,
            new DeterministicConversationSummarizer(new CharacterBasedTokenEstimator(), TimeProvider.System),
            options,
            loggerFactory.CreateLogger<MemoryWriter>());
    }

    private static ConversationMemoryKey Key(string conversationId = "conversation")
    {
        return new ConversationMemoryKey(conversationId, "tenant", "user");
    }

    private static MemoryWriteRequest Write(
        ConversationMemoryKey key,
        ConversationMemoryRole role,
        string content,
        string? correlationId = null,
        string? sessionId = null,
        int? tokenCount = null,
        bool isSensitive = false)
    {
        return new MemoryWriteRequest(key, role, content, correlationId, sessionId, tokenCount, isSensitive);
    }

    private static ConversationMemoryEntry Entry(
        string content = "content",
        long sequenceNumber = 1,
        int? tokenCount = null,
        DateTimeOffset? createdAtUtc = null,
        string? correlationId = null,
        string? sessionId = null)
    {
        return new ConversationMemoryEntry(
            "entry",
            ConversationMemoryRole.User,
            content,
            createdAtUtc ?? DateTimeOffset.UtcNow,
            sequenceNumber,
            correlationId,
            sessionId,
            tokenCount);
    }

    private static AIOrchestrationRequest Request(
        bool useMemory,
        string? conversationId = "conversation",
        string? tenantId = "tenant",
        string? userId = "user",
        bool promptEngine = false,
        bool invalidPrompt = false)
    {
        var request = new AIOrchestrationRequest("system", "current user", "GenericChat")
        {
            UseMemory = useMemory,
            ConversationId = conversationId,
            Identity = new AIIdentityContext(tenantId, userId, null)
        };

        if (!promptEngine)
        {
            return request;
        }

        var variables = new Dictionary<string, string>
        {
            ["systemInstruction"] = "template system",
            ["userMessage"] = "template user"
        };

        if (invalidPrompt)
        {
            variables["unexpected"] = "value";
        }

        return request with
        {
            PromptTemplateId = new PromptTemplateId("generic-chat"),
            PromptTemplateVersion = PromptTemplateVersion.Parse("1.0"),
            PromptVariables = variables
        };
    }

    private static ServiceProvider CreateOrchestrationProvider(FakeChatProvider chatProvider)
    {
        var services = BaseOrchestrationServices(chatProvider);
        services.AddTradeMindMemory(compactionOptions: new MemoryCompactionOptions(enabled: false));
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static ServiceCollection BaseOrchestrationServices(FakeChatProvider chatProvider)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IChatProvider>(chatProvider);
        services.AddSingleton<IAIProviderMetadata>(new FakeProviderMetadata());
        services.AddTradeMindAIOrchestration();
        return services;
    }

    private sealed class FakeChatProvider(Exception? exception = null) : IChatProvider
    {
        public int CallCount { get; private set; }

        public ChatRequest? LastRequest { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            LastCancellationToken = cancellationToken;

            if (exception is not null)
            {
                throw exception;
            }

            return Task.FromResult(new ChatResponse(
                "response",
                "Fake",
                "fake-model",
                new ChatUsage(3, 2, 5),
                "response-id",
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class FakeProviderMetadata : IAIProviderMetadata
    {
        public string ProviderName => "Fake";

        public AIProviderCapabilities Capabilities { get; } = new(
            SupportsChat: true,
            SupportsEmbeddings: false,
            SupportsStreaming: false,
            SupportsToolCalling: false,
            SupportsVision: false);
    }

    private sealed class ThrowingMemoryReader : IMemoryReader
    {
        public Task<MemoryReadResult> ReadAsync(MemoryReadRequest request, CancellationToken cancellationToken)
        {
            throw new MemoryOperationException("read failed", request.Key);
        }
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        public MutableTimeProvider(DateTimeOffset initialUtc)
        {
            InitialUtc = initialUtc;
            _utcNow = initialUtc;
        }

        private DateTimeOffset _utcNow;

        public DateTimeOffset InitialUtc { get; }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public IList<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName)
        {
            return new RecordingLogger(Messages);
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger(IList<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Add(formatter(state, exception));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
