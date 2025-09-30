﻿using FinanceApi.Models;

namespace FinanceApi.Interfaces
{
    public interface ILocationRepository
    {
        Task<IEnumerable<LocationDto>> GetAllAsync();
        Task<LocationDto> GetByIdAsync(long id);
        Task<int> CreateAsync(Location location);
        Task UpdateAsync(Location location);
        Task DeactivateAsync(int id);
    }
}
