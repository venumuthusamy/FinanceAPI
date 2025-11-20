using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using FinanceApi.Data;
using UnityWorksERP.Finance.AR;

namespace FinanceApi.Repositories
{
    public class ArInvoiceRepository : DynamicRepository, IArInvoiceRepository
    {
        public ArInvoiceRepository(IDbConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        public async Task<IEnumerable<ArInvoiceListDto>> GetAllAsync()
        {
            const string sql = @"
;WITH InvBase AS (
    SELECT
        v.Id          AS InvoiceId,
        v.InvoiceNo,
        v.InvoiceDate,
        v.Amount,
        v.PaidAmount,
        v.Balance,
        v.CustomerId,
        ISNULL(c.CustomerName,'') AS CustomerName
    FROM dbo.vwSalesInvoiceOpenForReceipt v
    LEFT JOIN dbo.Customer c ON c.Id = v.CustomerId
),
-- credit per invoice
CnInvoiceAgg AS (
    SELECT
        cn.SiId AS InvoiceId,
        InvoiceCreditAmount = SUM(ISNULL(cn.Subtotal,0))
    FROM dbo.CreditNote cn
    WHERE cn.IsActive = 1
      AND cn.SiId IS NOT NULL
    GROUP BY cn.SiId
),
-- credit per customer (for the summary row)
CnCustomerAgg AS (
    SELECT
        cn.CustomerId,
        CustomerCreditAmount = SUM(ISNULL(cn.Subtotal,0)),
        CustomerCreditNo     = MIN(cn.CreditNoteNo),
        CustomerCreditDate   = MIN(cn.CreditNoteDate),
        CustomerCreditStatus = MAX(cn.Status) 
    FROM dbo.CreditNote cn
    WHERE cn.IsActive = 1
      AND cn.CustomerId IS NOT NULL
    GROUP BY cn.CustomerId
)
SELECT
    i.InvoiceId,
    i.InvoiceNo,
    i.InvoiceDate,
    DueDate = i.InvoiceDate,
    i.CustomerId,
    i.CustomerName,
    Amount     = i.Amount,
    Paid       = i.PaidAmount,
    CreditNote = ISNULL(ci.InvoiceCreditAmount,0),
    Outstanding = i.Amount - i.PaidAmount - ISNULL(ci.InvoiceCreditAmount,0),

    CustomerCreditNoteAmount = ISNULL(cc.CustomerCreditAmount,0),
    CustomerCreditNoteNo     = cc.CustomerCreditNo,
    CustomerCreditNoteDate   = cc.CustomerCreditDate,
    CustomerCreditStatus     = ISNULL(cc.CustomerCreditStatus,0)
FROM InvBase i
LEFT JOIN CnInvoiceAgg  ci ON ci.InvoiceId   = i.InvoiceId
LEFT JOIN CnCustomerAgg cc ON cc.CustomerId  = i.CustomerId
ORDER BY i.CustomerName, i.InvoiceDate, i.InvoiceNo;";

            return await Connection.QueryAsync<ArInvoiceListDto>(sql);
        }



    }
}
