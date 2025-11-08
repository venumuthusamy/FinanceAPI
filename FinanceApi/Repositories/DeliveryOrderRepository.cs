using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using System.Data;
using static FinanceApi.ModelDTO.DeliveryOrderDtos;

namespace FinanceApi.Repositories
{
    public class DeliveryOrderRepository : DynamicRepository, IDeliveryOrderRepository
    {
        public DeliveryOrderRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        public async Task<int> CreateAsync(DoCreateRequest req, int userId)
        {
           
            var doNumber = $"DO-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

            var id = await Connection.QuerySingleAsync<int>(@"
      INSERT INTO dbo.DeliveryOrder
        (DoNumber, Status, SoId, PackId, CreatedBy)
      OUTPUT INSERTED.Id
      VALUES (@DoNumber, 0, @SoId, @PackId, @UserId)",
              new { DoNumber = doNumber, req.SoId, req.PackId, UserId = userId });

            foreach (var l in req.Lines)
            {
                await Connection.ExecuteAsync(@"
        INSERT INTO dbo.DeliveryOrderLine
          (DoId, SoLineId, PackLineId, ItemId, ItemName, Uom, Qty, Notes)
        VALUES
          (@DoId, @SoLineId, @PackLineId, @ItemId, @ItemName, @Uom, @Qty, @Notes)",
                  new
                  {
                      DoId = id,
                      l.SoLineId,
                      l.PackLineId,
                      l.ItemId,
                      l.ItemName,
                      l.Uom,
                      l.Qty,
                      l.Notes
                  });
            }

           
            return id;
        }

        public Task<DoHeaderDto?> GetHeaderAsync(int id)
          => Connection.QuerySingleOrDefaultAsync<DoHeaderDto>(@"
        SELECT Id, DoNumber, Status, SoId, PackId, DriverName, VehicleId, RouteName,
               DeliveryDate, PodFileUrl, IsPosted
        FROM dbo.DeliveryOrder WHERE Id=@id", new { id });

        public Task<IEnumerable<DoLineDto>> GetLinesAsync(int doId)
          => Connection.QueryAsync<DoLineDto>(@"
        SELECT Id, DoId, SoLineId, PackLineId, ItemId, ItemName, Uom, Qty, Notes
        FROM dbo.DeliveryOrderLine WHERE DoId=@doId", new { doId });

        public Task UpdateHeaderAsync(int id, DoUpdateHeaderRequest req, int userId)
          => Connection.ExecuteAsync(@"
        UPDATE dbo.DeliveryOrder SET
          DriverName=@DriverName, VehicleId=@VehicleId, RouteName=@RouteName,
          DeliveryDate=@DeliveryDate, UpdatedBy=@UserId, UpdatedOn=SYSUTCDATETIME()
        WHERE Id=@Id",
              new { Id = id, req.DriverName, req.VehicleId, req.RouteName, req.DeliveryDate, UserId = userId });

        public async Task<int> AddLineAsync(DoAddLineRequest req, int userId)
        {
            return await Connection.QuerySingleAsync<int>(@"
      INSERT INTO dbo.DeliveryOrderLine
        (DoId, SoLineId, PackLineId, ItemId, ItemName, Uom, Qty, Notes)
      OUTPUT INSERTED.Id
      VALUES (@DoId, @SoLineId, @PackLineId, @ItemId, @ItemName, @Uom, @Qty, @Notes)",
              req);
        }

        public Task RemoveLineAsync(int lineId)
          => Connection.ExecuteAsync(@"DELETE FROM dbo.DeliveryOrderLine WHERE Id=@lineId", new { lineId });

        public Task SetStatusAsync(int id, int status, int userId)
          => Connection.ExecuteAsync(@"
        UPDATE dbo.DeliveryOrder SET Status=@status, UpdatedBy=@userId, UpdatedOn=SYSUTCDATETIME()
        WHERE Id=@id", new { id, status, userId });

        public Task PostAsync(int id, int userId)
          => Connection.ExecuteAsync(@"
        UPDATE dbo.DeliveryOrder SET Status=4, IsPosted=1,
               UpdatedBy=@userId, UpdatedOn=SYSUTCDATETIME()
        WHERE Id=@id", new { id, userId });
    }
}
