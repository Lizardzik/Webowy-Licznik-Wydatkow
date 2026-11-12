using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Data
{
    public static class ApplicationDbContextExtensions
    {
        public static async Task<User> GetOrCreateDefaultUserAsync(this ApplicationDbContext context)
        {
            User? user = await context.User.OrderBy(u => u.Id).FirstOrDefaultAsync();
            if (user != null)
            {
                return user;
            }

            user = new User
            {
                FirstName = "Użytkownik",
                LastName = "Domyślny",
                Nickname = "Admin",
                WalletBalance = 0m,
                MonthlyBudgetLimit = 0m
            };

            context.User.Add(user);
            await context.SaveChangesAsync();

            return user;
        }
    }
}