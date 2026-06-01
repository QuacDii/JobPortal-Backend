using Microsoft.EntityFrameworkCore;
using TKVL.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình Controllers và Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. Cấu hình Database
builder.Services.AddDbContext<JobPortalDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 3. Cấu hình dịch vụ Thanh toán MoMo
builder.Services.Configure<TKVL.DTOs.Payment.MomoConfig>(builder.Configuration.GetSection("MomoAPI"));
builder.Services.AddScoped<TKVL.Services.IPaymentService, TKVL.Services.PaymentService>();

// 4. Cấu hình CORS cho ReactJS
builder.Services.AddCors(options => {
    options.AddPolicy("AllowReactApp",
        policy => policy.WithOrigins("http://localhost:5173")
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

var app = builder.Build();

// Cấu hình HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReactApp");

app.UseAuthorization(); // Giữ lại middleware này theo chuẩn mặc định của ASP.NET Core

app.MapControllers();

app.Run();