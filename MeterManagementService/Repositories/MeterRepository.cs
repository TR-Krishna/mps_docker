// ???????????????????????????????????????????????????????????????????????????
// MeterManagementService/Repositories/MeterRepository.cs
// ???????????????????????????????????????????????????????????????????????????

using MeterManagementService.Data;
using MeterManagementService.Models;
using Microsoft.EntityFrameworkCore;

namespace MeterManagementService.Repositories;

public interface IMeterRepository
{
    Task<IEnumerable<Meter>> GetAllAsync(
        MeterType? type = null, MeterStatus? status = null,
        CommunicationType? commType = null, string? zone = null, string? substationId = null);
    Task<Meter?> GetByIdAsync(Guid id);
    Task<Meter?> GetBySerialAsync(string serial);
    Task<Meter> CreateAsync(Meter meter);
    Task<Meter> UpdateAsync(Meter meter);
    Task<bool> DeleteAsync(Guid id);
}

public class MeterRepository : IMeterRepository
{
    private readonly MeterDbContext _db;
    public MeterRepository(MeterDbContext db) => _db = db;

    public async Task<IEnumerable<Meter>> GetAllAsync(
        MeterType? type = null, MeterStatus? status = null,
        CommunicationType? commType = null, string? zone = null, string? substationId = null)
    {
        var q = _db.Meters.AsQueryable();
        if (type         is not null) q = q.Where(m => m.MeterType         == type.Value);
        if (status       is not null) q = q.Where(m => m.Status            == status.Value);
        if (commType     is not null) q = q.Where(m => m.CommunicationType == commType.Value);
        if (zone         is not null) q = q.Where(m => m.Zone              == zone);
        if (substationId is not null) q = q.Where(m => m.SubstationId      == substationId);
        return await q.OrderByDescending(m => m.OnboardedAt).AsNoTracking().ToListAsync();
    }

    public async Task<Meter?> GetByIdAsync(Guid id)      => await _db.Meters.FindAsync(id);
    public async Task<Meter?> GetBySerialAsync(string s)  =>
        await _db.Meters.AsNoTracking().FirstOrDefaultAsync(m => m.SerialNumber == s);

    public async Task<Meter> CreateAsync(Meter m)
    { _db.Meters.Add(m); await _db.SaveChangesAsync(); return m; }

    public async Task<Meter> UpdateAsync(Meter m)
    { _db.Entry(m).State = EntityState.Modified; await _db.SaveChangesAsync(); return m; }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var m = await _db.Meters.FindAsync(id);
        if (m is null) return false;
        _db.Meters.Remove(m); await _db.SaveChangesAsync(); return true;
    }
}
