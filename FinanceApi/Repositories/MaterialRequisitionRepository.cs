using Dapper;
using FinanceApi.Data;
using FinanceApi.Interfaces;
using FinanceApi.ModelDTO;
using FinanceApi.Models;

namespace FinanceApi.Repositories
{
    public class MaterialRequisitionRepository : DynamicRepository, IMaterialRequisitionRepository
    {
        public MaterialRequisitionRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        // ✅ GetAll with Lines
        public async Task<IEnumerable<MaterialRequisitionDTO>> GetAllAsync()
        {
            const string sql = @"
SELECT
    mr.Id,
    mr.ReqNo,
    mr.OutletId,
    mr.RequesterName,
    mr.ReqDate,
    mr.Status,
    mr.CreatedBy,
    mr.CreatedDate,
    mr.UpdatedBy,
    mr.UpdatedDate,
    mr.IsActive
FROM dbo.MaterialRequisition mr
WHERE mr.IsActive = 1
ORDER BY mr.Id DESC;

SELECT
    l.Id,
    l.MaterialReqId,
    l.ItemId,
    l.ItemCode,
    l.ItemName,
    l.UomId,
    l.UomName,
    l.Qty,
    l.CreatedDate
FROM dbo.MaterialRequisitionLine l
INNER JOIN dbo.MaterialRequisition mr ON mr.Id = l.MaterialReqId
WHERE mr.IsActive = 1;";

            using var multi = await Connection.QueryMultipleAsync(sql);

            var headers = (await multi.ReadAsync<MaterialRequisitionDTO>()).ToList();
            var lines = (await multi.ReadAsync<MaterialRequisitionLineDTO>()).ToList();

            // group lines by header id
            var lineLookup = lines
                .GroupBy(x => x.MaterialReqId)
                .ToDictionary(g => g.Key, g => (ICollection<MaterialRequisitionLineDTO>)g.ToList());

            foreach (var h in headers)
            {
                h.Lines = lineLookup.TryGetValue(h.Id, out var lns)
                    ? lns
                    : new List<MaterialRequisitionLineDTO>();
            }

            return headers;
        }

        // ✅ GetById with DTO Lines (fix conversion error)
        public async Task<MaterialRequisitionDTO?> GetByIdAsync(int id)
        {
            const string sql = @"
SELECT
    mr.Id,
    mr.ReqNo,
    mr.OutletId,
    mr.RequesterName,
    mr.ReqDate,
    mr.Status,
    mr.CreatedBy,
    mr.CreatedDate,
    mr.UpdatedBy,
    mr.UpdatedDate,
    mr.IsActive
FROM dbo.MaterialRequisition mr
WHERE mr.Id = @Id AND mr.IsActive = 1;

SELECT
    l.Id,
    l.MaterialReqId,
    l.ItemId,
    l.ItemCode,
    l.ItemName,
    l.UomId,
    l.UomName,
    l.Qty,
    l.CreatedDate
FROM dbo.MaterialRequisitionLine l
WHERE l.MaterialReqId = @Id
ORDER BY l.Id;";

            using var multi = await Connection.QueryMultipleAsync(sql, new { Id = id });

            var header = await multi.ReadSingleOrDefaultAsync<MaterialRequisitionDTO>();
            if (header == null) return null;

            var lines = (await multi.ReadAsync<MaterialRequisitionLineDTO>()).ToList();
            header.Lines = lines;

            return header;
        }

        // ✅ Create/Update can stay same (Entity based)
        public async Task<int> CreateAsync(MaterialRequisition mrq)
        {
            const string getNextReqNoSql = @"
DECLARE @Prefix NVARCHAR(20) = 'MRQ-' + CONVERT(VARCHAR(8), GETDATE(), 112) + '-';

-- lock rows for this prefix to avoid duplicates in concurrent inserts
DECLARE @NextSeq INT =
(
  SELECT ISNULL(MAX(CAST(RIGHT(ReqNo, 4) AS INT)), 0) + 1
  FROM dbo.MaterialRequisition WITH (UPDLOCK, HOLDLOCK)
  WHERE ReqNo LIKE @Prefix + '%'
);

SELECT @Prefix + RIGHT('0000' + CAST(@NextSeq AS VARCHAR(4)), 4);
";

            const string insertHeaderSql = @"
INSERT INTO dbo.MaterialRequisition
    (ReqNo, OutletId, RequesterName, ReqDate, Status,
     CreatedBy, CreatedDate, UpdatedBy, UpdatedDate, IsActive)
OUTPUT INSERTED.Id
VALUES
    (@ReqNo, @OutletId, @RequesterName, @ReqDate, @Status,
     @CreatedBy, COALESCE(@CreatedDate, SYSUTCDATETIME()), @UpdatedBy, @UpdatedDate, @IsActive);";

            const string insertLineSql = @"
INSERT INTO dbo.MaterialRequisitionLine
    (MaterialReqId, ItemId, ItemCode, ItemName, UomId, UomName, Qty, CreatedDate)
VALUES
    (@MaterialReqId, @ItemId, @ItemCode, @ItemName, @UomId, @UomName, @Qty,
     COALESCE(@CreatedDate, SYSUTCDATETIME()));";

            using var conn = Connection; // ✅ IMPORTANT: new connection from factory
            conn.Open();

            using var tx = conn.BeginTransaction();

            try
            {
                // ✅ generate reqno inside transaction
                var reqNo = await conn.QuerySingleAsync<string>(getNextReqNoSql, transaction: tx);

                // ✅ header insert
                var headerId = await conn.QuerySingleAsync<int>(insertHeaderSql, new
                {
                    ReqNo = reqNo,
                    mrq.OutletId,
                    mrq.RequesterName,
                    mrq.ReqDate,
                    mrq.Status,
                    mrq.CreatedBy,
                    mrq.CreatedDate,
                    mrq.UpdatedBy,
                    mrq.UpdatedDate,
                    mrq.IsActive
                }, tx);

                // ✅ lines insert
                if (mrq.Lines != null && mrq.Lines.Count > 0)
                {
                    foreach (var line in mrq.Lines)
                    {
                        await conn.ExecuteAsync(insertLineSql, new
                        {
                            MaterialReqId = headerId,
                            line.ItemId,
                            line.ItemCode,
                            line.ItemName,
                            line.UomId,
                            line.UomName,
                            line.Qty,
                            line.CreatedDate
                        }, tx);
                    }
                }

                tx.Commit();
                return headerId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }



        public async Task UpdateAsync(MaterialRequisition mrq)
        {
            const string updateHeaderSql = @"
UPDATE dbo.MaterialRequisition
SET
    OutletId = @OutletId,
    RequesterName = @RequesterName,
    ReqDate = @ReqDate,
    Status = @Status,
    UpdatedBy = @UpdatedBy,
    UpdatedDate = COALESCE(@UpdatedDate, SYSUTCDATETIME())
WHERE Id = @Id;";

            const string deleteLinesSql = @"DELETE FROM dbo.MaterialRequisitionLine WHERE MaterialReqId = @Id;";

            const string insertLineSql = @"
INSERT INTO dbo.MaterialRequisitionLine
    (MaterialReqId, ItemId, ItemCode, ItemName, UomId, UomName, Qty, CreatedDate)
VALUES
    (@MaterialReqId, @ItemId, @ItemCode, @ItemName, @UomId, @UomName, @Qty,
     COALESCE(@CreatedDate, SYSUTCDATETIME()));";

            using var conn = Connection;
            conn.Open();

            using var tx = conn.BeginTransaction();

            try
            {
                await conn.ExecuteAsync(updateHeaderSql, new
                {
                    mrq.Id,
                    mrq.OutletId,
                    mrq.RequesterName,
                    mrq.ReqDate,
                    mrq.Status,
                    mrq.UpdatedBy,
                    mrq.UpdatedDate
                }, tx);

                await conn.ExecuteAsync(deleteLinesSql, new { mrq.Id }, tx);

                if (mrq.Lines != null && mrq.Lines.Count > 0)
                {
                    foreach (var line in mrq.Lines)
                    {
                        await conn.ExecuteAsync(insertLineSql, new
                        {
                            MaterialReqId = mrq.Id,
                            line.ItemId,
                            line.ItemCode,
                            line.ItemName,
                            line.UomId,
                            line.UomName,
                            line.Qty,
                            line.CreatedDate
                        }, tx);
                    }
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }


        public async Task DeactivateAsync(int id)
        {
            const string sql = @"UPDATE dbo.MaterialRequisition SET IsActive = 0 WHERE Id = @Id;";
            await Connection.ExecuteAsync(sql, new { Id = id });
        }
    }
}
