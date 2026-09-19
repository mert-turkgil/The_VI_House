using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIHouse.DataAccess.Concrete.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TransactionId",
                table: "SeminarEnrollments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransactionId",
                table: "Payments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransactionId",
                table: "MembershipPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderCustomerId",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    AmountRefundedMinor = table.Column<long>(type: "bigint", nullable: false),
                    ProviderCustomerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProviderSessionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProviderPaymentIntentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProviderChargeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProviderSubscriptionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProviderInvoiceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PaidAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CanceledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RefundedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Disputed = table.Column<bool>(type: "bit", nullable: false),
                    DisputedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastEventId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeminarEnrollments_TransactionId",
                table: "SeminarEnrollments",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TransactionId",
                table: "Payments",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipPayments_TransactionId",
                table: "MembershipPayments",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ProviderCustomerId",
                table: "AspNetUsers",
                column: "ProviderCustomerId",
                unique: true,
                filter: "[ProviderCustomerId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_Kind_Status",
                table: "PaymentTransactions",
                columns: new[] { "Kind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ProviderChargeId",
                table: "PaymentTransactions",
                column: "ProviderChargeId",
                filter: "[ProviderChargeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ProviderInvoiceId",
                table: "PaymentTransactions",
                column: "ProviderInvoiceId",
                unique: true,
                filter: "[ProviderInvoiceId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ProviderPaymentIntentId",
                table: "PaymentTransactions",
                column: "ProviderPaymentIntentId",
                unique: true,
                filter: "[ProviderPaymentIntentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ProviderSessionId",
                table: "PaymentTransactions",
                column: "ProviderSessionId",
                unique: true,
                filter: "[ProviderSessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ProviderSubscriptionId",
                table: "PaymentTransactions",
                column: "ProviderSubscriptionId",
                filter: "[ProviderSubscriptionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_RelatedEntityType_RelatedEntityId",
                table: "PaymentTransactions",
                columns: new[] { "RelatedEntityType", "RelatedEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_UserId_CreatedAt",
                table: "PaymentTransactions",
                columns: new[] { "UserId", "CreatedAt" });

            // --- Backfill: one transaction per existing money row, keyed on the row's own id so the
            // back-reference is deterministic and re-runnable. PaymentStatus ints: 0 Created, 1 Pending,
            // 2 Authorized, 3 Paid, 4 Failed, 5 Cancelled, 6 PartiallyRefunded, 7 Refunded, 8 Chargeback.
            migrationBuilder.Sql("""
                INSERT INTO [PaymentTransactions] ([Id], [Kind], [UserId], [RelatedEntityType], [RelatedEntityId], [Status], [AmountMinor], [Currency], [AmountRefundedMinor],
                    [ProviderSessionId], [PaidAt], [CanceledAt], [RefundedAt], [Disputed], [CreatedAt], [UpdatedAt])
                SELECT p.[Id], N'Experience', p.[UserId], N'Payment', p.[Id],
                    CASE p.[Status] WHEN 3 THEN N'Succeeded' WHEN 4 THEN N'Failed' WHEN 5 THEN N'Canceled' WHEN 6 THEN N'PartiallyRefunded' WHEN 7 THEN N'Refunded' WHEN 8 THEN N'Succeeded' ELSE N'Pending' END,
                    p.[AmountMinor], p.[Currency], 0,
                    CASE WHEN p.[ProviderReference] LIKE N'cs_%' THEN p.[ProviderReference] END,
                    CASE WHEN p.[Status] IN (3, 6, 7, 8) THEN ISNULL(p.[UpdatedAt], p.[CreatedAt]) END,
                    CASE WHEN p.[Status] = 5 THEN ISNULL(p.[UpdatedAt], p.[CreatedAt]) END,
                    CASE WHEN p.[Status] IN (6, 7) THEN ISNULL(p.[UpdatedAt], p.[CreatedAt]) END,
                    CASE WHEN p.[Status] = 8 THEN 1 ELSE 0 END,
                    p.[CreatedAt], p.[UpdatedAt]
                FROM [Payments] p
                WHERE NOT EXISTS (SELECT 1 FROM [PaymentTransactions] t WHERE t.[Id] = p.[Id]);
                UPDATE [Payments] SET [TransactionId] = [Id] WHERE [TransactionId] IS NULL;

                INSERT INTO [PaymentTransactions] ([Id], [Kind], [UserId], [RelatedEntityType], [RelatedEntityId], [Status], [AmountMinor], [Currency], [AmountRefundedMinor],
                    [ProviderSessionId], [ProviderInvoiceId], [ProviderSubscriptionId], [ProviderCustomerId], [PaidAt], [CanceledAt], [RefundedAt], [Disputed], [CreatedAt], [UpdatedAt])
                SELECT mp.[Id],
                    CASE WHEN mp.[ProviderReference] LIKE N'renewal_%' THEN N'MembershipRenewal' ELSE N'Membership' END,
                    mp.[UserId], N'MembershipPayment', mp.[Id],
                    CASE mp.[Status] WHEN 3 THEN N'Succeeded' WHEN 4 THEN N'Failed' WHEN 5 THEN N'Canceled' WHEN 6 THEN N'PartiallyRefunded' WHEN 7 THEN N'Refunded' WHEN 8 THEN N'Succeeded' ELSE N'Pending' END,
                    mp.[AmountMinor], mp.[Currency], 0,
                    CASE WHEN mp.[ProviderReference] LIKE N'cs_%' THEN mp.[ProviderReference] END,
                    CASE WHEN mp.[ProviderReference] LIKE N'renewal_in_%' THEN SUBSTRING(mp.[ProviderReference], 9, 200) END,
                    m.[ProviderSubscriptionId], m.[ProviderCustomerId],
                    CASE WHEN mp.[Status] IN (3, 6, 7, 8) THEN ISNULL(mp.[UpdatedAt], mp.[CreatedAt]) END,
                    CASE WHEN mp.[Status] = 5 THEN ISNULL(mp.[UpdatedAt], mp.[CreatedAt]) END,
                    CASE WHEN mp.[Status] IN (6, 7) THEN ISNULL(mp.[UpdatedAt], mp.[CreatedAt]) END,
                    CASE WHEN mp.[Status] = 8 THEN 1 ELSE 0 END,
                    mp.[CreatedAt], mp.[UpdatedAt]
                FROM [MembershipPayments] mp
                LEFT JOIN [Memberships] m ON m.[Id] = mp.[MembershipId]
                WHERE NOT EXISTS (SELECT 1 FROM [PaymentTransactions] t WHERE t.[Id] = mp.[Id]);
                UPDATE [MembershipPayments] SET [TransactionId] = [Id] WHERE [TransactionId] IS NULL;

                INSERT INTO [PaymentTransactions] ([Id], [Kind], [UserId], [RelatedEntityType], [RelatedEntityId], [Status], [AmountMinor], [Currency], [AmountRefundedMinor],
                    [ProviderSessionId], [PaidAt], [CanceledAt], [Disputed], [CreatedAt], [UpdatedAt])
                SELECT e.[Id], N'Session', e.[UserId], N'SeminarEnrollment', e.[Id],
                    CASE e.[Status] WHEN 1 THEN N'Succeeded' WHEN 2 THEN N'Canceled' ELSE N'Pending' END,
                    e.[AmountMinor], e.[Currency], 0,
                    CASE WHEN e.[ProviderReference] LIKE N'cs_%' THEN e.[ProviderReference] END,
                    CASE WHEN e.[Status] = 1 THEN ISNULL(e.[ConfirmedAt], ISNULL(e.[UpdatedAt], e.[CreatedAt])) END,
                    CASE WHEN e.[Status] = 2 THEN ISNULL(e.[UpdatedAt], e.[CreatedAt]) END,
                    0, e.[CreatedAt], e.[UpdatedAt]
                FROM [SeminarEnrollments] e
                WHERE e.[GrantedVia] = 2
                  AND NOT EXISTS (SELECT 1 FROM [PaymentTransactions] t WHERE t.[Id] = e.[Id]);
                UPDATE [SeminarEnrollments] SET [TransactionId] = [Id] WHERE [TransactionId] IS NULL AND [GrantedVia] = 2;
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_MembershipPayments_PaymentTransactions_TransactionId",
                table: "MembershipPayments",
                column: "TransactionId",
                principalTable: "PaymentTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_PaymentTransactions_TransactionId",
                table: "Payments",
                column: "TransactionId",
                principalTable: "PaymentTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SeminarEnrollments_PaymentTransactions_TransactionId",
                table: "SeminarEnrollments",
                column: "TransactionId",
                principalTable: "PaymentTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MembershipPayments_PaymentTransactions_TransactionId",
                table: "MembershipPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_PaymentTransactions_TransactionId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_SeminarEnrollments_PaymentTransactions_TransactionId",
                table: "SeminarEnrollments");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_SeminarEnrollments_TransactionId",
                table: "SeminarEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_TransactionId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_MembershipPayments_TransactionId",
                table: "MembershipPayments");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ProviderCustomerId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "SeminarEnrollments");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "MembershipPayments");

            migrationBuilder.DropColumn(
                name: "ProviderCustomerId",
                table: "AspNetUsers");
        }
    }
}
