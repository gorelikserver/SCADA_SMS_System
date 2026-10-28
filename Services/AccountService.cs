using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SCADASMSSystem.Web.Data;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services
{
    public enum SignInOutcome { Success, InvalidCredentials, LockedOut }

    public record SignInResult(SignInOutcome Outcome, Account? Account = null, DateTime? LockedUntil = null);

    public interface IAccountService
    {
        Task<bool> AnyAccountsAsync();
        Task<IReadOnlyList<Account>> ListAsync();
        Task<SignInResult> SignInAsync(string userName, string password);
        Task<(bool Ok, string? Error)> CreateAsync(string userName, string password, string role);
        Task<(bool Ok, string? Error)> ChangePasswordAsync(int accountId, string newPassword);
        Task<(bool Ok, string? Error)> SetRoleAsync(int accountId, string role);
        Task<(bool Ok, string? Error)> DeleteAsync(int accountId);
    }

    public class AccountService : IAccountService
    {
        public const int MinPasswordLength = 8;
        public const int MaxFailedLogins = 5;
        public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

        private readonly SCADADbContext _db;
        private readonly PasswordHasher<Account> _hasher = new();

        public AccountService(SCADADbContext db)
        {
            _db = db;
        }

        public Task<bool> AnyAccountsAsync() => _db.Accounts.AnyAsync();

        public async Task<IReadOnlyList<Account>> ListAsync() =>
            await _db.Accounts.AsNoTracking().OrderBy(a => a.UserName).ToListAsync();

        public async Task<SignInResult> SignInAsync(string userName, string password)
        {
            var account = await FindAsync(userName);
            if (account == null)
            {
                // Same work as a real check, so timing does not reveal which user names exist.
                _hasher.HashPassword(new Account(), password);
                return new SignInResult(SignInOutcome.InvalidCredentials);
            }

            if (account.LockedUntil > DateTime.Now)
                return new SignInResult(SignInOutcome.LockedOut, LockedUntil: account.LockedUntil);

            var verdict = _hasher.VerifyHashedPassword(account, account.PasswordHash, password);
            if (verdict == PasswordVerificationResult.Failed)
            {
                account.FailedLogins++;
                if (account.FailedLogins >= MaxFailedLogins)
                {
                    account.LockedUntil = DateTime.Now + LockoutDuration;
                    account.FailedLogins = 0;
                }
                await _db.SaveChangesAsync();
                return account.LockedUntil > DateTime.Now
                    ? new SignInResult(SignInOutcome.LockedOut, LockedUntil: account.LockedUntil)
                    : new SignInResult(SignInOutcome.InvalidCredentials);
            }

            if (verdict == PasswordVerificationResult.SuccessRehashNeeded)
                account.PasswordHash = _hasher.HashPassword(account, password);
            account.FailedLogins = 0;
            account.LockedUntil = null;
            account.LastLoginAt = DateTime.Now;
            await _db.SaveChangesAsync();
            return new SignInResult(SignInOutcome.Success, account);
        }

        public async Task<(bool Ok, string? Error)> CreateAsync(string userName, string password, string role)
        {
            userName = userName?.Trim() ?? string.Empty;
            if (userName.Length is < 3 or > 64)
                return (false, "User name must be 3-64 characters.");
            if (!AccountRoles.All.Contains(role))
                return (false, "Unknown role.");
            if (PasswordError(password) is { } passwordError)
                return (false, passwordError);
            if (await FindAsync(userName) != null)
                return (false, "That user name is already taken.");

            var account = new Account { UserName = userName, Role = role };
            account.PasswordHash = _hasher.HashPassword(account, password);
            _db.Accounts.Add(account);
            await _db.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Ok, string? Error)> ChangePasswordAsync(int accountId, string newPassword)
        {
            if (PasswordError(newPassword) is { } passwordError)
                return (false, passwordError);
            var account = await _db.Accounts.FindAsync(accountId);
            if (account == null)
                return (false, "Account not found.");
            account.PasswordHash = _hasher.HashPassword(account, newPassword);
            account.FailedLogins = 0;
            account.LockedUntil = null;
            await _db.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Ok, string? Error)> SetRoleAsync(int accountId, string role)
        {
            if (!AccountRoles.All.Contains(role))
                return (false, "Unknown role.");
            var account = await _db.Accounts.FindAsync(accountId);
            if (account == null)
                return (false, "Account not found.");
            if (account.Role == AccountRoles.Admin && role != AccountRoles.Admin && await IsLastAdminAsync(accountId))
                return (false, "The last admin cannot be demoted.");
            account.Role = role;
            await _db.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool Ok, string? Error)> DeleteAsync(int accountId)
        {
            var account = await _db.Accounts.FindAsync(accountId);
            if (account == null)
                return (false, "Account not found.");
            if (account.Role == AccountRoles.Admin && await IsLastAdminAsync(accountId))
                return (false, "The last admin cannot be deleted.");
            _db.Accounts.Remove(account);
            await _db.SaveChangesAsync();
            return (true, null);
        }

        public static string? PasswordError(string? password) =>
            string.IsNullOrEmpty(password) || password.Length < MinPasswordLength
                ? $"Password must be at least {MinPasswordLength} characters."
                : null;

        private Task<Account?> FindAsync(string userName)
        {
            var name = userName?.Trim() ?? string.Empty;
            return _db.Accounts.FirstOrDefaultAsync(a => a.UserName == name);
        }

        private async Task<bool> IsLastAdminAsync(int accountId) =>
            !await _db.Accounts.AnyAsync(a => a.Role == AccountRoles.Admin && a.AccountId != accountId);
    }
}
