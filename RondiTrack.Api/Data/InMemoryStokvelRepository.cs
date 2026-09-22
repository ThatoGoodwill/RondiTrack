using System.Collections.Concurrent;
using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Data;

public sealed class InMemoryStokvelRepository : IStokvelRepository
{
    private readonly ConcurrentDictionary<Guid, Stokvel> _stokvels = new();

    public Task<IReadOnlyList<Stokvel>> GetAllAsync(CancellationToken ct = default)
    { IReadOnlyList<Stokvel> all = _stokvels.Values.OrderBy(s => s.Name).ToList(); return Task.FromResult(all); }
    public Task<Stokvel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    { _stokvels.TryGetValue(id, out var s); return Task.FromResult<Stokvel?>(s); }
    public Task AddAsync(Stokvel stokvel, CancellationToken ct = default) { _stokvels[stokvel.Id] = stokvel; return Task.CompletedTask; }
    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_stokvels.TryRemove(id, out _));
    public Task<bool> HasMemberAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(_stokvels.Values.Any(s => s.HasMember(userId)));
}