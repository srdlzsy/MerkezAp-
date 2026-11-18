using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FurpaMerkezApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClassifySafeWriteValidationFailures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE dbo.mobile_offline_sync_requests
                SET error_code = N'SAFE_WRITE_VALIDATION_REJECTED',
                    retryable = 0
                WHERE operation_code = N'siparis-islemleri.verilen-depo-siparisleri.create'
                  AND status = N'Failed'
                  AND error_code IS NULL
                  AND retryable IS NULL
                  AND error_message LIKE N'%(Parameter ''%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE dbo.mobile_offline_sync_requests
                SET error_code = NULL,
                    retryable = NULL
                WHERE operation_code = N'siparis-islemleri.verilen-depo-siparisleri.create'
                  AND status = N'Failed'
                  AND error_code = N'SAFE_WRITE_VALIDATION_REJECTED'
                  AND retryable = 0
                  AND error_message LIKE N'%(Parameter ''%';
                """);
        }
    }
}
