// Repositories/SalesInvoiceRepository.cs
using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using static FinanceApi.ModelDTO.SalesInvoiceDtos;
using System.Data;

namespace FinanceApi.Repositories
{
    public class SalesInvoiceRepository : DynamicRepository, ISalesInvoiceRepository
    {
        public SalesInvoiceRepository(IDbConnectionFactory factory) : base(factory) { }

        public async Task<IEnumerable<SiSourceLineDto>> GetSourceLinesAsync(byte sourceType, int sourceId)
        {
            var sql = sourceType == 1
                ? @"SELECT SourceLineId, SourceType, SourceId, ItemId, ItemName, UomName,
                           QtyOpen, UnitPrice, DiscountPct, TaxCodeId, DefaultCurrencyId
                    FROM dbo.vw_SI_SourceFromSO WHERE SourceId=@Id"
                : @"SELECT SourceLineId, SourceType, SourceId, ItemId, ItemName, UomName,
                           QtyOpen, UnitPrice, DiscountPct, TaxCodeId, DefaultCurrencyId
                    FROM dbo.vw_SI_SourceFromDO WHERE SourceId=@Id";

           
            return await Connection.QueryAsync<SiSourceLineDto>(sql, new { Id = sourceId });
        }

        public async Task<int> CreateAsync(int userId, SiCreateRequest req)
        {
           

            try
            {
                const string insHead = @"
INSERT INTO dbo.SalesInvoice
(InvoiceDate, SourceType, SoId, DoId, Status, CurrencyId, CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive)
OUTPUT INSERTED.Id
VALUES (@InvoiceDate, @SourceType, @SoId, @DoId, 0, @CurrencyId, @UserId, SYSUTCDATETIME(), @UserId, SYSUTCDATETIME(), 1);";

                var siId = await Connection.ExecuteScalarAsync<int>(
                    insHead,
                    new
                    {
                        req.InvoiceDate,
                        req.SourceType,
                        req.SoId,
                        req.DoId,
                        req.CurrencyId,
                        UserId = userId
                    }
                   
                );

                const string insLine = @"
INSERT INTO dbo.SalesInvoiceLine
(SiId, SourceType, SourceLineId, ItemId, ItemName, Uom, Qty, UnitPrice, DiscountPct, TaxCodeId, CurrencyId)
VALUES
(@SiId, @SourceType, @SourceLineId, @ItemId, @ItemName, @Uom, @Qty, @UnitPrice, @DiscountPct, @TaxCodeId, @CurrencyId);";

                foreach (var l in req.Lines)
                {
                    await Connection.ExecuteAsync(
                        insLine,
                        new
                        {
                            SiId = siId,
                            SourceType = req.SourceType,
                            SourceLineId = l.SourceLineId,
                            l.ItemId,
                            l.ItemName,
                            Uom = l.Uom,
                            l.Qty,
                            l.UnitPrice,
                            l.DiscountPct,
                            l.TaxCodeId,
                            CurrencyId = l.CurrencyId ?? req.CurrencyId
                        }
                    );
                }

                // Optional invoice number pattern
                await Connection.ExecuteAsync(
                    @"UPDATE dbo.SalesInvoice SET InvoiceNo = CONCAT('SI-', RIGHT(CONVERT(VARCHAR(8), Id + 100000), 6))
                      WHERE Id=@Id", new { Id = siId });

               
                return siId;
            }
            catch
            {
               
                throw;
            }
        }

        public async Task<SiHeaderDto?> GetAsync(int id)
        {
            const string sql = @"SELECT Id, ISNULL(InvoiceNo,'') InvoiceNo, InvoiceDate, SourceType, SoId, DoId,
                                        CurrencyId, Status, IsActive
                                 FROM dbo.SalesInvoice WHERE Id=@Id";
           
            return await Connection.QueryFirstOrDefaultAsync<SiHeaderDto>(sql, new { Id = id });
        }

        public async Task<IEnumerable<SiLineDto>> GetLinesAsync(int id)
        {
            const string sql = @"SELECT Id, SiId, SourceType, SourceLineId, ItemId, ItemName, Uom, Qty, UnitPrice,
                                        DiscountPct, TaxCodeId, CurrencyId
                                 FROM dbo.SalesInvoiceLine WHERE SiId=@Id";
           
            return await Connection.QueryAsync<SiLineDto>(sql, new { Id = id });
        }

        public async Task<IEnumerable<SiListRowDto>> GetListAsync()
        {
            const string sql = @"
SELECT si.Id,
       ISNULL(si.InvoiceNo,'') AS InvoiceNo,
       si.InvoiceDate,
       si.SourceType,
       CASE WHEN si.SourceType=1 THEN ISNULL(so.SalesOrderNo,'')
            WHEN si.SourceType=2 THEN ISNULL(d.DoNumber,'') END AS SourceRef,
       SUM((sil.Qty*sil.UnitPrice)*(1-(sil.DiscountPct/100.0))) AS Total
FROM dbo.SalesInvoice si
LEFT JOIN dbo.SalesInvoiceLine sil ON sil.SiId = si.Id
LEFT JOIN dbo.SalesOrder so ON so.Id = si.SoId
LEFT JOIN dbo.DeliveryOrder d ON d.Id = si.DoId
WHERE si.IsActive=1
GROUP BY si.Id, si.InvoiceNo, si.InvoiceDate, si.SourceType, so.SalesOrderNo, d.DoNumber
ORDER BY si.Id DESC;";
           
            return await Connection.QueryAsync<SiListRowDto>(sql);
        }

        public async Task DeactivateAsync(int id)
        {
            
            await Connection.ExecuteAsync(@"UPDATE dbo.SalesInvoice SET IsActive=0, UpdatedDate=SYSUTCDATETIME() WHERE Id=@Id", new { Id = id });
        }
    }
}
