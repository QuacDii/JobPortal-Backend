using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TKVL.Models;

namespace TKVL.Services
{
    public class JobExpirationCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        // Chu kỳ quét: Mỗi 30 phút quét 1 lần
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(30);

        public JobExpirationCleanupService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("[JOB EXPIRATION SERVICE] Dịch vụ quét hạn tuyển dụng đã khởi động.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessExpiredJobsAndPositionsAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LỖI EXPIRATION SERVICE]: {ex.Message}");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task ProcessExpiredJobsAndPositionsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<JobPortalDbContext>();
            var emailService = scope.ServiceProvider.GetService<IEmailService>();
            var now = DateTime.Now;

            // ===================================================================
            // BƯỚC 1: Quét và đóng các VỊ TRÍ CON đã quá hạn riêng
            // ===================================================================
            var expiredPositions = await context.ChiTietViTris
                .Include(v => v.MaTinNavigation)
                    .ThenInclude(t => t.MaCongTyNavigation)
                        .ThenInclude(c => c.MaUserNavigation)
                .Where(v => v.TrangThai == 1
                         && v.NgayHetHan.HasValue
                         && v.NgayHetHan.Value < now)
                .ToListAsync();

            if (expiredPositions.Any())
            {
                foreach (var pos in expiredPositions)
                {
                    pos.TrangThai = 2; // Đổi sang trạng thái: Đã đóng / Hết hạn nhận CV
                }
                Console.WriteLine($"[JOB EXPIRATION] Đã tự động đóng {expiredPositions.Count} vị trí tuyển dụng hết hạn.");
            }

            // ===================================================================
            // BƯỚC 2: Quét và đóng các CHIẾN DỊCH TỔNG (TinTuyenDung)
            // Đóng khi: Hạn chiến dịch < now HOẶC tất cả vị trí con bên trong đều đã đóng (TrangThai != 1)
            // ===================================================================
            var activeCampaigns = await context.TinTuyenDungs
                .Include(t => t.ChiTietViTris)
                .Include(t => t.MaCongTyNavigation)
                    .ThenInclude(c => c.MaUserNavigation)
                .Where(t => t.TrangThai == 1)
                .ToListAsync();

            var newlyExpiredCampaigns = new List<TinTuyenDung>();

            foreach (var camp in activeCampaigns)
            {
                bool isCampaignDateExpired = camp.NgayHetHan < now;
                bool areAllPositionsClosed = !camp.ChiTietViTris.Any(v => v.TrangThai == 1);

                if (isCampaignDateExpired || areAllPositionsClosed)
                {
                    camp.TrangThai = 2; // Đóng toàn bộ chiến dịch

                    // Đóng luôn tất cả các vị trí con chưa đóng
                    foreach (var pos in camp.ChiTietViTris.Where(v => v.TrangThai == 1))
                    {
                        pos.TrangThai = 2;
                    }

                    newlyExpiredCampaigns.Add(camp);
                }
            }

            await context.SaveChangesAsync();

            // ===================================================================
            // BƯỚC 3: Gửi email nhắc nhở Nhà tuyển dụng gia hạn tin (Tùy chọn)
            // ===================================================================
            if (emailService != null && newlyExpiredCampaigns.Any())
            {
                foreach (var camp in newlyExpiredCampaigns)
                {
                    string? employerEmail = camp.MaCongTyNavigation?.MaUserNavigation?.Email;
                    string employerName = camp.MaCongTyNavigation?.MaUserNavigation?.HoTen ?? "Nhà tuyển dụng";

                    if (!string.IsNullOrEmpty(employerEmail))
                    {
                        string emailBody = $@"
                            <p>Chào <b>{employerName}</b>,</p>
                            <p>Chiến dịch tuyển dụng <b>'{camp.TieuDeChienDich}'</b> của bạn đã chính thức <b>hết hạn nhận hồ sơ</b>.</p>
                            <p>Để tiếp tục nhận hồ sơ từ các ứng viên tiềm năng, bạn có thể đăng nhập vào hệ thống và gia hạn ngày tuyển dụng.</p>
                            <br/><p>Trân trọng,<br/><b>Ban quản trị JobsNow System</b></p>";

                        _ = emailService.SendEmailAsync(employerEmail, $"[JobsNow] Chiến dịch tuyển dụng '{camp.TieuDeChienDich}' đã hết hạn", emailBody);
                    }
                }
            }
        }
    }
}