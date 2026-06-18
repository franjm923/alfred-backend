using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Alfred2.Services;

public record PendingSlots(Guid MedicoId, List<(DateTime startUtc, DateTime endUtc)> Slots, DateTime ExpireUtc);

public class PendingSlotsService
{
    private readonly ConcurrentDictionary<string, PendingSlots> _pending = new();

    public void Set(string fromE164, Guid medicoId, IEnumerable<(DateTime startUtc, DateTime endUtc)> slots)
    {
        var list = slots.ToList();
        _pending[fromE164] = new PendingSlots(medicoId, list, DateTime.UtcNow.AddMinutes(10));
    }

    public bool TryGetValid(string fromE164, [NotNullWhen(true)] out PendingSlots? value)
    {
        if (_pending.TryGetValue(fromE164, out var v))
        {
            if (DateTime.UtcNow <= v.ExpireUtc)
            {
                value = v;
                return true;
            }
            // expirado
            _pending.TryRemove(fromE164, out _);
        }
        value = null;
        return false;
    }

    public void Clear(string fromE164) => _pending.TryRemove(fromE164, out _);

    // TODO: background cleanup task para eliminar expirados periódicamente si hiciera falta
}
