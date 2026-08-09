using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TKVL.Models;
using TKVL.Services;

namespace TKVL.Services
{
    public class DailyJobAlertService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        public DailyJobAlertService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // 1. Tính toán thời gian chờ đến 8:00 Sáng ngày hôm sau
                var now = DateTime.Now;
                var nextRunTime = new DateTime(now.Year, now.Month, now.Day, 8, 0, 0); // Đặt lịch lúc 8h00 sáng

                if (now > nextRunTime)
                {
                    nextRunTime = nextRunTime.AddDays(1);
                }

                var delayTime = nextRunTime - now;

                Console.WriteLine($"[JOB ALERT] Đang đếm ngược để gửi mail tự động sau: {delayTime.TotalHours:F2} giờ nữa.");

                // 2. Chờ đến đúng giờ
                await Task.Delay(delayTime, stoppingToken);

                // 3. Thực thi công việc Gửi Email
                try
                {
                    await ProcessDailyJobAlertsAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LỖI DAILY JOB ALERT]: {ex.Message}");
                }
            }
        }

        private async Task ProcessDailyJobAlertsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var _context = scope.ServiceProvider.GetRequiredService<JobPortalDbContext>();
            var _emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            Console.WriteLine("[JOB ALERT] Đang bắt đầu quét dữ liệu để gửi Job Alert...");

            // CHỈ lấy những user chưa bị khóa VÀ đang Bật tìm việc
            var activeUsers = await _context.Users
                .Where(u => u.TrangThai == true && u.TrangThaiTimViec == true)
                .ToListAsync();

            foreach (var user in activeUsers)
            {
                // 2. Lấy danh sách ngành nghề con user có đăng ký nhận thông báo (🌟 Đã sửa: MaNganh -> MaNganhCon)
                var userAlertIndustries = await _context.JobAlerts
                    .Where(a => a.MaUser == user.MaUser && a.TrangThai == true)
                    .Select(a => a.MaNganhCon)
                    .ToListAsync();

                // 2b. TỰ ĐỘNG NHẬN DIỆN ngành nghề con user ĐÃ TỪNG ỨNG TUYỂN (từ bảng DonUngTuyen) (🌟 Đã sửa: vt.MaNganh -> vt.MaNganhCon)
                var appliedIndustries = await (from don in _context.DonUngTuyens
                                               join cv in _context.Cvs on don.MaCv equals cv.MaCv
                                               join vt in _context.ChiTietViTris on don.MaViTri equals vt.MaViTri
                                               where cv.MaUser == user.MaUser
                                               select vt.MaNganhCon).Distinct().ToListAsync();

                // 2c. Gộp cả 2 danh sách lại và loại bỏ trùng lặp
                var targetIndustries = userAlertIndustries
                    .Concat(appliedIndustries)
                    .Distinct()
                    .ToList();

                if (!targetIndustries.Any())
                {
                    continue; // Bỏ qua nếu user không cài đặt thông báo VÀ cũng chưa từng ứng tuyển Job nào
                }

                // 3. Tìm 5 công việc phù hợp nhất dựa trên danh sách ngành gộp (🌟 Đã sửa: vt.MaNganh -> vt.MaNganhCon)
                var matchingJobs = await (from vt in _context.ChiTietViTris
                                          join tin in _context.TinTuyenDungs on vt.MaTin equals tin.MaTin
                                          join ct in _context.CongTies on tin.MaCongTy equals ct.MaCongTy
                                          where targetIndustries.Contains(vt.MaNganhCon)
                                             && tin.TrangThai == 1
                                             && tin.NgayHetHan >= DateTime.Now
                                          orderby tin.NgayHetHan descending
                                          select new
                                          {
                                              vt.MaViTri,
                                              vt.TenViTri,
                                              vt.Luong,
                                              ct.TenCongTy,
                                              ct.Logo
                                          }).Take(5).ToListAsync();

                // 4. Nếu có việc làm -> Gửi email
                if (matchingJobs.Any())
                {
                    string jobListHtml = "";
                    foreach (var job in matchingJobs)
                    {
                        jobListHtml += $@"
                        <div style='border: 1px solid #eee; padding: 15px; margin-bottom: 15px; border-radius: 8px; background-color: #fcfcfc;'>
                            <h3 style='color: #1890ff; margin: 0 0 5px 0; font-size: 16px;'>{job.TenViTri}</h3>
                            <p style='margin: 0; color: #555; font-size: 14px;'>🏢 <b>{job.TenCongTy}</b></p>
                            <p style='margin: 5px 0 0 0; color: #00b14f; font-weight: bold;'>💰 Lương: {job.Luong}</p>
                            <div style='margin-top: 10px;'>
                                <a href='http://localhost:5173/chi-tiet-viec-lam/{job.MaViTri}' style='display: inline-block; padding: 8px 15px; background-color: #1890ff; color: #ffffff; text-decoration: none; border-radius: 5px; font-size: 13px;'>Xem chi tiết & Ứng tuyển</a>
                            </div>
                        </div>";
                    }

                    string emailBody = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; padding: 20px; border-radius: 8px;'>
                        <div style='text-align: center; border-bottom: 2px solid #00b14f; padding-bottom: 15px; margin-bottom: 20px;'>
                            <h2 style='color: #00b14f; margin: 0;'>JOBSNOW TÌM VIỆC</h2>
                            <p style='color: #666; margin: 5px 0 0 0;'>Bản tin Việc làm Hôm nay</p>
                        </div>
                        <p>Chào <b>{user.HoTen}</b>,</p>
                        <p>Hệ thống JOBSNOW vừa tổng hợp được các vị trí vô cùng hấp dẫn dành riêng cho bạn dựa trên thông báo cài đặt của bạn:</p>
                        
                        {jobListHtml}

                        <p style='color: #8c8c8c; font-size: 13px; text-align: center; margin-top: 30px; border-top: 1px solid #e0e0e0; padding-top: 15px;'>
                            Bạn nhận được email này vì bạn đã bật thông báo Job Alert. Nếu không muốn nhận nữa, bạn có thể tắt tại phần Quản lý hồ sơ.
                        </p>
                    </div>";

                    await _emailService.SendEmailAsync(user.Email, "[JOBSNOW] Bản tin Việc làm mới nhất trong ngày", emailBody);

                    await Task.Delay(1000);
                }
            }
            Console.WriteLine("[JOB ALERT] Đã gửi thông báo hoàn tất!");
        }
    }
}