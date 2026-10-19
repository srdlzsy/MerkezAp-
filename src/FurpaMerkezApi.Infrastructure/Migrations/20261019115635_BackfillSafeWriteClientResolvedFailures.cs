using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillSafeWriteClientResolvedFailures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE dbo.mobile_offline_sync_requests
                SET error_code = N'MIKRO_DOCUMENT_IDENTITY_CONFLICT',
                    retryable = 0,
                    revision = NEWID()
                WHERE status = N'Failed'
                  AND error_code IS NULL
                  AND error_message IN (
                      N'Firma mal kabul belgesi kimligi zaten mevcut. Belge seri ve sira numarasi tum depolar genelinde benzersiz olmalidir.',
                      N'Bu belge seri ve sira numarasi Mikro''da zaten kullaniliyor. Bu denemede yeni evrak yazimi baslamadi. Seri veya sira numarasini degistirip yeniden kaydedin.');

                UPDATE dbo.mobile_offline_sync_requests
                SET error_code = N'MIKRO_OFFICIAL_DOCUMENT_ALREADY_RECEIVED',
                    retryable = 0,
                    revision = NEWID()
                WHERE status = N'Failed'
                  AND error_code IS NULL
                  AND error_message IN (
                      N'Company receiving document already exists for the selected customer and document no.',
                      N'Secilen cari ve resmi belge numarasiyla firma mal kabul kaydi zaten mevcut. Yeni belge olusturulmadi; listeyi yenileyin.',
                      N'Secilen cari ve resmi belge numarasiyla firma mal kabul kaydi zaten mevcut. Bu denemede yeni evrak yazimi baslamadi. Ikinci kaydi olusturmayin; mal kabul listesini yenileyip mevcut evraki kontrol edin.');

                UPDATE dbo.mobile_offline_sync_requests
                SET error_code = N'WAREHOUSE_ORDER_LINE_CLOSED',
                    retryable = 0,
                    revision = NEWID()
                WHERE status = N'Failed'
                  AND error_code IS NULL
                  AND error_message = N'Linked warehouse order line is already closed.';

                UPDATE dbo.mobile_offline_sync_requests
                SET error_code = N'CREATE_REFERENCE_NOT_FOUND',
                    retryable = 0,
                    revision = NEWID()
                WHERE status = N'Failed'
                  AND error_code IS NULL
                  AND (
                      error_message = N'Customer was not found in Mikro write database.' OR
                      error_message LIKE N'Warehouse order line was not found:%' OR
                      error_message LIKE N'Order line was not found in Mikro write database:%');

                UPDATE dbo.mobile_offline_sync_requests
                SET error_code = N'MIKRO_BUSINESS_RULE_REJECTED',
                    retryable = 0,
                    revision = NEWID()
                WHERE status = N'Failed'
                  AND error_code IS NULL
                  AND error_message LIKE N'%U17%';

                UPDATE dbo.mobile_offline_sync_requests
                SET error_code = N'MIKRO_WRITE_OUTCOME_UNCONFIRMED',
                    retryable = 1,
                    revision = NEWID()
                WHERE status = N'Failed'
                  AND error_code IS NULL
                  AND (
                      error_message = N'MikroAPI - TimeOut' OR
                      error_message = N'An error occurred while saving the entity changes. See the inner exception for details.' OR
                      error_message LIKE N'%Execution Timeout Expired%' OR
                      error_message LIKE N'%maximum number of retries%' OR
                      error_message LIKE N'%task was canceled%' OR
                      error_message LIKE N'%operation was canceled%' OR
                      error_message LIKE N'%Operation cancelled by user%' OR
                      error_message LIKE N'%specified in the FROM clause) does not exist%' OR
                      error_message LIKE N'%Insert bulk failed due to a schema change%');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE dbo.mobile_offline_sync_requests
                SET error_code = NULL,
                    retryable = NULL,
                    revision = NEWID()
                WHERE status = N'Failed'
                  AND error_code = N'MIKRO_DOCUMENT_IDENTITY_CONFLICT'
                  AND error_message IN (
                      N'Firma mal kabul belgesi kimligi zaten mevcut. Belge seri ve sira numarasi tum depolar genelinde benzersiz olmalidir.',
                      N'Bu belge seri ve sira numarasi Mikro''da zaten kullaniliyor. Bu denemede yeni evrak yazimi baslamadi. Seri veya sira numarasini degistirip yeniden kaydedin.');

                UPDATE dbo.mobile_offline_sync_requests
                SET error_code = NULL,
                    retryable = NULL,
                    revision = NEWID()
                WHERE status = N'Failed'
                  AND error_code = N'MIKRO_OFFICIAL_DOCUMENT_ALREADY_RECEIVED'
                  AND error_message IN (
                      N'Company receiving document already exists for the selected customer and document no.',
                      N'Secilen cari ve resmi belge numarasiyla firma mal kabul kaydi zaten mevcut. Yeni belge olusturulmadi; listeyi yenileyin.',
                      N'Secilen cari ve resmi belge numarasiyla firma mal kabul kaydi zaten mevcut. Bu denemede yeni evrak yazimi baslamadi. Ikinci kaydi olusturmayin; mal kabul listesini yenileyip mevcut evraki kontrol edin.');

                UPDATE dbo.mobile_offline_sync_requests
                SET error_code = NULL,
                    retryable = NULL,
                    revision = NEWID()
                WHERE status = N'Failed'
                  AND (
                      (error_code = N'WAREHOUSE_ORDER_LINE_CLOSED' AND
                       error_message = N'Linked warehouse order line is already closed.') OR
                      (error_code = N'CREATE_REFERENCE_NOT_FOUND' AND (
                       error_message = N'Customer was not found in Mikro write database.' OR
                       error_message LIKE N'Warehouse order line was not found:%' OR
                       error_message LIKE N'Order line was not found in Mikro write database:%')) OR
                      (error_code = N'MIKRO_BUSINESS_RULE_REJECTED' AND
                       error_message LIKE N'%U17%') OR
                      (error_code = N'MIKRO_WRITE_OUTCOME_UNCONFIRMED' AND (
                       error_message = N'MikroAPI - TimeOut' OR
                       error_message = N'An error occurred while saving the entity changes. See the inner exception for details.' OR
                       error_message LIKE N'%Execution Timeout Expired%' OR
                       error_message LIKE N'%maximum number of retries%' OR
                       error_message LIKE N'%task was canceled%' OR
                       error_message LIKE N'%operation was canceled%' OR
                       error_message LIKE N'%Operation cancelled by user%' OR
                       error_message LIKE N'%specified in the FROM clause) does not exist%' OR
                       error_message LIKE N'%Insert bulk failed due to a schema change%')));
                """);
        }
    }
}
