using ExpenseTracker.Data;
using ExpenseTracker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExpenseTracker.Services
{
    public class NotificationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public NotificationBackgroundService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Uruchamiaj co 24 godziny (generowanie raportów tygodniowych)
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                        // Pobierz wszystkich użytkowników
                        var users = await dbContext.User.ToListAsync(stoppingToken);

                        foreach (var user in users)
                        {
                            // Sprawdź, czy dzisiaj jest niedziela (dzień generowania raportów tygodniowych)
                            if (DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
                            {
                                Console.WriteLine($"Generowanie tygodniowego raportu dla użytkownika: {user.Id}");
                                await notificationService.CreateWeeklyReportNotification(user.Id);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Błąd w usłudze w tle: {ex.Message}");
                }

                // Odczekaj 24 godziny
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}