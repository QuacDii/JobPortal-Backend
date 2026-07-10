using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TKVL.Models;
using TKVL.Services;
using CloudinaryDotNet;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình Controllers và Swagger
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
}); ;
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Đăng ký HttpClient và Service xử lý phân tích AI
builder.Services.AddHttpClient<IAiService, AiService>();
builder.Services.AddHttpClient<IAiAnalysisService, AiAnalysisService>();

// Đăng ký Email Service
builder.Services.AddScoped<IEmailService, EmailService>();

// 2. Cấu hình Database
builder.Services.AddDbContext<JobPortalDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var cloudinarySection = builder.Configuration.GetSection("Cloudinary");
var account = new Account(
    cloudinarySection["CloudName"],
    cloudinarySection["ApiKey"],
    cloudinarySection["ApiSecret"]
);
var cloudinary = new Cloudinary(account);
builder.Services.AddSingleton(cloudinary);

// 3. Cấu hình dịch vụ Thanh toán MoMo
builder.Services.Configure<TKVL.DTOs.Payment.MomoConfig>(builder.Configuration.GetSection("MomoAPI"));
builder.Services.AddScoped<TKVL.Services.IPaymentService, TKVL.Services.PaymentService>();

// 4. Cấu hình dịch vụ Cloudinary
builder.Services.AddScoped<ICloudinaryService, CloudinaryService>();

// 5. Cấu hình CORS cho ReactJS
builder.Services.AddCors(options => {
    options.AddPolicy("AllowReactApp",
        policy => policy.WithOrigins("http://localhost:5173")
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

// 6. Cấu hình JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });
var app = builder.Build();

// Cấu hình HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReactApp");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();