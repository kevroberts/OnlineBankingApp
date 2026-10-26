using Microsoft.EntityFrameworkCore;
using OnlineBankingAPI.Domain;

namespace OnlineBankingAPI.Data;

public class BankDbContext(DbContextOptions<BankDbContext> options) : DbContext(options)
{
    public const string TransferReferenceSequence = "transfer_reference_seq";

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Payee> Payees => Set<Payee>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Names below that appear in raw SQL (checks, filters) are the snake_case database names.
        b.HasPostgresExtension("citext");
        b.HasPostgresExtension("pg_trgm");
        b.HasSequence<long>(TransferReferenceSequence);

        b.Entity<Customer>(e =>
        {
            e.Property(x => x.Username).HasColumnType("citext").HasMaxLength(50);
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(200);
            e.Property(x => x.DisplayName).HasMaxLength(100);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.FailedLoginCount).HasDefaultValue(0);
        });

        b.Entity<Session>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.Property(x => x.UserAgent).HasMaxLength(300);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        });

        b.Entity<Account>(e =>
        {
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_accounts_kind", "kind IN ('checking', 'savings', 'clearing')");
                t.HasCheckConstraint("ck_accounts_status", "status IN ('active', 'frozen', 'closed')");
                t.HasCheckConstraint("ck_accounts_balance", "kind = 'clearing' OR balance >= 0");
            });
            e.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId);
            e.Property(x => x.Kind).HasMaxLength(10);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.AccountNumber).HasColumnType("char(10)");
            e.HasIndex(x => x.AccountNumber).IsUnique();
            e.Property(x => x.Currency).HasColumnType("char(3)").HasDefaultValue("USD");
            e.Property(x => x.Balance).HasColumnType("numeric(19,4)");
            e.Property(x => x.Status).HasMaxLength(10);
            e.Property(x => x.Version).IsRowVersion().HasColumnName("xmin").HasColumnType("xid");
            e.Ignore(x => x.MaskedNumber);
        });

        b.Entity<Payee>(e =>
        {
            e.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId);
            e.Property(x => x.Nickname).HasMaxLength(50);
            e.HasIndex(x => new { x.CustomerId, x.Nickname }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.BankName).HasMaxLength(100);
            e.Property(x => x.AccountNumber).HasMaxLength(17);
            e.Property(x => x.RoutingNumber).HasColumnType("char(9)");
            e.Property(x => x.Category).HasMaxLength(20);
            e.Ignore(x => x.MaskedAccountNumber);
        });

        b.Entity<Quote>(e =>
        {
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_quotes_amount", "amount > 0");
                t.HasCheckConstraint("ck_quotes_destination", "num_nonnulls(destination_account_id, payee_id) = 1");
                t.HasCheckConstraint("ck_quotes_not_same_account", "source_account_id <> destination_account_id");
            });
            e.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId);
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.SourceAccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.DestinationAccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Payee>().WithMany().HasForeignKey(x => x.PayeeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Transfer>().WithMany().HasForeignKey(x => x.TransferId).OnDelete(DeleteBehavior.Restrict);
            // Guard 2: a quote can be executed by one transfer only.
            e.HasIndex(x => x.TransferId).IsUnique().HasFilter("transfer_id IS NOT NULL");
            e.Property(x => x.Amount).HasColumnType("numeric(19,4)");
            e.Property(x => x.Note).HasMaxLength(140);
        });

        b.Entity<Transfer>(e =>
        {
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_transfers_amount", "amount > 0");
                t.HasCheckConstraint("ck_transfers_destination", "num_nonnulls(destination_account_id, payee_id) = 1");
                t.HasCheckConstraint("ck_transfers_not_same_account", "source_account_id <> destination_account_id");
            });
            e.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId);
            e.HasOne(x => x.SourceAccount).WithMany().HasForeignKey(x => x.SourceAccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DestinationAccount).WithMany().HasForeignKey(x => x.DestinationAccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Payee).WithMany().HasForeignKey(x => x.PayeeId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Reference).HasMaxLength(24);
            e.HasIndex(x => x.Reference).IsUnique();
            e.HasIndex(x => new { x.CustomerId, x.CreatedAt, x.Reference }).IsDescending(false, true, true);
            e.Property(x => x.Amount).HasColumnType("numeric(19,4)");
            e.Property(x => x.Note).HasMaxLength(140);
            e.Property(x => x.Status).HasMaxLength(10);
        });

        b.Entity<LedgerEntry>(e =>
        {
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_ledger_entries_direction", "direction IN ('D', 'C')");
                t.HasCheckConstraint("ck_ledger_entries_amount", "amount > 0");
                t.HasCheckConstraint("ck_ledger_entries_category",
                    "category IN ('transfer', 'payment', 'deposit', 'card', 'fee', 'interest')");
            });
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Transfer>().WithMany().HasForeignKey(x => x.TransferId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.AccountId, x.Sequence }).IsUnique();
            e.HasIndex(x => new { x.AccountId, x.PostedAt, x.Sequence })
                .IsDescending(false, true, true)
                .IncludeProperties(x => new { x.Amount, x.Direction, x.BalanceAfter, x.Description })
                .HasDatabaseName("ix_ledger_entries_history");
            e.HasIndex(x => x.TransferId);
            e.HasIndex(x => new { x.Description, x.Counterparty, x.Note })
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops", "gin_trgm_ops", "gin_trgm_ops")
                .HasDatabaseName("ix_ledger_entries_search");
            e.Property(x => x.Direction).HasColumnType("char(1)");
            e.Property(x => x.Amount).HasColumnType("numeric(19,4)");
            e.Property(x => x.BalanceAfter).HasColumnType("numeric(19,4)");
            e.Property(x => x.Description).HasMaxLength(200);
            e.Property(x => x.Counterparty).HasMaxLength(100);
            e.Property(x => x.Category).HasMaxLength(20);
            e.Property(x => x.Note).HasMaxLength(140);
            e.Ignore(x => x.SignedAmount);
        });

        b.Entity<IdempotencyRecord>(e =>
        {
            e.HasKey(x => new { x.CustomerId, x.IdempotencyKey });
            e.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId);
            e.HasOne<Transfer>().WithMany().HasForeignKey(x => x.TransferId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.ResponseBody).HasColumnType("jsonb");
            e.HasIndex(x => x.ExpiresAt);
        });
    }
}
