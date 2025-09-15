using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceApi.Repositories
{
    public class StateRepository : IStateRepository
    {
        private readonly ApplicationDbContext _context;

        public StateRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<StateDto>> GetAllAsync()
        {
            var result = await _context.State
                .Include(s => s.Country)
                .Where(c => c.IsActive)
                .OrderBy(c => c.Id)
                .Select(s => new StateDto
                {
                    Id = s.Id,
                    StateId = s.Id,
                    StateName = s.StateName,
                    CountryId = s.CountryId,
                    CountryName = s.Country.CountryName,
                    CreatedBy = s.CreatedBy,
                    CreatedDate = s.CreatedDate,
                    UpdatedBy = s.UpdatedBy,
                    UpdatedDate = s.UpdatedDate,
                    IsActive = s.IsActive
                })
                .ToListAsync();

            return result;
        }


        public async Task<StateDto?> GetByIdAsync(int id)
        {
            return await _context.State
                .Include(s => s.Country)
                .Where(s => s.Id == id)
                .Where(c => c.IsActive)
                .Select(s => new StateDto
                {
                    Id = s.Id,
                    StateId = s.Id,
                    StateName = s.StateName,
                    CountryId = s.CountryId,
                    CountryName = s.Country.CountryName,
                    CreatedBy = s.CreatedBy,
                    CreatedDate = s.CreatedDate,
                    UpdatedBy = s.UpdatedBy,
                    UpdatedDate = s.UpdatedDate,
                    IsActive = s.IsActive
                })
                .FirstOrDefaultAsync();
        }

        public async Task<State> CreateAsync(State state)
        {
            try
            {
                state.CreatedBy = "System";
                state.CreatedDate = DateTime.UtcNow;
                state.IsActive = true;
                _context.State.Add(state);
                await _context.SaveChangesAsync();
                return state;
            }
            catch (DbUpdateException dbEx)
            {
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                throw new Exception($"Error: {innerMessage}", dbEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }

        public async Task<State?> UpdateAsync(int id, State updatedState)
        {
            try
            {
                var existingState = await _context.State.FirstOrDefaultAsync(s => s.Id == id);
                if (existingState == null) return null;

                // Manually update only scalar properties (excluding Id)
                existingState.StateName = updatedState.StateName;
                existingState.CountryId = updatedState.CountryId;
                existingState.UpdatedBy = "System";
                existingState.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return existingState;
            }
            catch (DbUpdateException dbEx)
            {
                var innerMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                throw new Exception($"Error: {innerMessage}", dbEx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }


        public async Task<bool> DeleteAsync(int id)
        {
            var state = await _context.State.FirstOrDefaultAsync(s => s.Id == id);
            if (state == null) return false;

            state.IsActive = false;
            //_context.State.Remove(state);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
