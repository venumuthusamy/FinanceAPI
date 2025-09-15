using FinanceApi.Data;
using FinanceApi.Models;
using Microsoft.EntityFrameworkCore;
using FinanceApi.Interfaces;

namespace FinanceApi.Repositories
{
    public class CustomerGroupsRepository : ICustomerGroupsRepository
    {
        private readonly ApplicationDbContext _context;

        public CustomerGroupsRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<CustomerGroups>> GetAllAsync()
        {
            return await _context.CustomerGroups.Where(c => c.IsActive).OrderBy(c => c.Id).ToListAsync();
        }

        public async Task<CustomerGroups?> GetByIdAsync(int id)
        {
            return await _context.CustomerGroups.FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
        }

        public async Task<CustomerGroups> CreateAsync(CustomerGroups customerGroups)
        {
            try
            {
                customerGroups.CreatedBy = "System";
                customerGroups.CreatedDate = DateTime.UtcNow;
                customerGroups.IsActive = true;
                _context.CustomerGroups.Add(customerGroups);
                await _context.SaveChangesAsync();
                return customerGroups;
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

        public async Task<CustomerGroups?> UpdateAsync(int id, CustomerGroups updatedCustomerGroups)
        {
            try
            {
                var existingCustomerGroups = await _context.CustomerGroups.FirstOrDefaultAsync(s => s.Id == id);
                if (existingCustomerGroups == null) return null;

                // Manually update only scalar properties (excluding Id)
                existingCustomerGroups.Name = updatedCustomerGroups.Name;
                existingCustomerGroups.Description = updatedCustomerGroups.Description;
                existingCustomerGroups.UpdatedBy = "System";
                existingCustomerGroups.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return existingCustomerGroups;
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
            var customerGroups = await _context.CustomerGroups.FirstOrDefaultAsync(s => s.Id == id);
            if (customerGroups == null) return false;

            customerGroups.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
