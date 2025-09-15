using FinanceApi.Data;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;
using FinanceApi.Interfaces;



namespace FinanceApi.Repositories
{
    public class ServiceRepository : IServicesRepository
    {
        private readonly ApplicationDbContext _context;

        public ServiceRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Service>> GetAllAsync()
        {
            return await _context.Service.Where(c => c.IsActive).OrderBy(c => c.Id).ToListAsync();
        }

        public async Task<Service?> GetByIdAsync(int id)
        {
            return await _context.Service.FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
        }

        public async Task<Service> CreateAsync(Service service)
        {
            try
            {
                service.CreatedBy = "System";
                service.CreatedDate = DateTime.UtcNow;
                service.IsActive = true;
                _context.Service.Add(service);
                await _context.SaveChangesAsync();
                return service;
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

        public async Task<Service?> UpdateAsync(int id, Service updatedService)
        {
            try
            {
                var existingService = await _context.Service.FirstOrDefaultAsync(s => s.Id == id);
                if (existingService == null) return null;

                // Manually update only scalar properties (excluding Id)
                existingService.Name = updatedService.Name;
                existingService.Charge = updatedService.Charge;
                existingService.Tax = updatedService.Tax;
                existingService.Description = updatedService.Description;
                existingService.UpdatedBy = "System";
                existingService.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return updatedService;
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
            var service = await _context.Service.FirstOrDefaultAsync(s => s.Id == id);
            if (service == null) return false;

            service.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
