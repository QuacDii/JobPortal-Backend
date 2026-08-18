using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TKVL.Models;

namespace TKVL.Services
{
    public class SubscriptionCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        // Chu kỳ quét ngầm: Quét định kỳ mỗi 30 phút một lần
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30);

        public SubscriptionCleanupService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("[SUBSCRIPTION CLEANUP] Dịch vụ quét ngầm gói dịch vụ đã khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessExpiredSubscriptionsAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LỖI CLEANUP SERVICE]: {ex.Message}");
                }

                // Chờ 30 phút trước lần quét tiếp theo
                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task ProcessExpiredSubscriptionsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<JobPortalDbContext>();
            var now = DateTime.Now;

            // 1. Quét tìm tất cả User có gói dịch vụ đã quá hạn
            var expiredUsers = await context.Users
                .Where(u => u.NgayHetHanGoi != null && u.NgayHetHanGoi < now)
                .ToListAsync();

            if (expiredUsers.Any())
            {
                foreach (var user in expiredUsers)
                {
                    user.NgayHetHanGoi = null;
                    user.LuotXemCvConLai = 0; // Đặt lại lượt xem CV về 0
                }

                // 2. Dọn dẹp các bản ghi đặc quyền UserDacQuyen hết hạn tương ứng
                var expiredUserIds = expiredUsers.Select(u => u.MaUser).ToList();
                var expiredDacQuyens = await context.UserDacQuyens
                    .Where(ud => expiredUserIds.Contains(ud.MaUser) && ud.NgayHetHan < now)
                    .ToListAsync();

                if (expiredDacQuyens.Any())
                {
                    context.UserDacQuyens.RemoveRange(expiredDacQuyens);
                }

                await context.SaveChangesAsync();
                Console.WriteLine($"[SUBSCRIPTION CLEANUP] Đã tự động reset {expiredUsers.Count} tài khoản hết hạn về NULL lúc: {DateTime.Now:HH:mm:ss dd/MM/yyyy}");
            }
        }
    }
}