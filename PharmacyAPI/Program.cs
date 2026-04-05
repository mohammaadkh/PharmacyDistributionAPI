using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. ربط قاعدة البيانات SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. إعدادات الـ JSON لحماية العلاقات الدائرية
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// 3. تعريف سياسة الـ CORS
// ✅ تعديل: أضفنا سياسة ثانية للـ Production بدل AllowAll
// قبل: سياسة وحدة AllowAnyOrigin للكل
// بعد: سياستين — AllowAll للـ Development، AllowFrontend للـ Production
// ليش: AllowAnyOrigin بالـ Production يخلي أي موقع يكلم الـ API
//       وهاد خطر أمني — نحدد الفرونت بس يكلمنا
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", b =>
        b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

    options.AddPolicy("AllowFrontend", b =>
        b.WithOrigins(builder.Configuration["AppSettings:FrontendUrl"]!)
         .AllowAnyMethod()
         .AllowAnyHeader());
});

// 4. إعدادات نظام الأمان JWT
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrEmpty(jwtKey) || string.IsNullOrEmpty(jwtIssuer))
    throw new Exception("إعدادات الـ JWT مفقودة في ملف appsettings.json!");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

// 5. إعدادات Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "أدخل التوكن هنا: اكتب Bearer ثم مسافة ثم التوكن"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] {}
        }
    });
});

// 6. تسجيل الـ EmailService
builder.Services.AddSingleton<EmailService>();

var app = builder.Build();

// 7. ترتيب الـ Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // ✅ تعديل: AllowAll بالـ Development بس
    // قبل: app.UseCors("AllowAll") دايماً
    // بعد: AllowAll بالـ Development، AllowFrontend بالـ Production
    app.UseCors("AllowAll");
}
else
{
    app.UseCors("AllowFrontend");
}

app.UseStaticFiles();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// 8. Seed Data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // ✅ تعديل: Migrate() بدل EnsureCreated()
    // قبل: context.Database.EnsureCreated()
    // بعد: context.Database.Migrate()
    // ليش: EnsureCreated بيعمل الداتابيز من الصفر بس ما بطبق
    //       أي Migration جديد — Migrate() بيطبق كل التغييرات
    //       الجديدة تلقائياً وما بمسح البيانات الموجودة
    context.Database.Migrate();

    if (!context.Categories.Any())
    {
        context.Categories.AddRange(
            new Category { Name = "Antibiotics" },
            new Category { Name = "Cardiology" },
            new Category { Name = "Endocrinology" }
        );
        context.SaveChanges();
    }

    if (!context.Medicines.Any())
    {
        var categories = context.Categories.ToList();

        context.Medicines.AddRange(
            new Medicine
            {
                Name = "Amoxicillin Capsules",
                Dosage = "500mg",
                Manufacturer = "Pfizer Global Pharma",
                PackSize = "100 Capsules",
                SKU = "PH-AMX-500-100CT",
                Price = 42.50m,
                StockQuantity = 500,
                IsFdaApproved = true,
                IsGmpCertified = true,
                CategoryId = categories.First(c => c.Name == "Antibiotics").Id,
                ImageUrl = "/images/amoxicillin.png"
            },
            new Medicine
            {
                Name = "Lisinopril Tablets",
                Dosage = "10mg",
                Manufacturer = "AstraZeneca Labs",
                PackSize = "500 Tablets",
                SKU = "PH-LIS-010-500CT",
                Price = 15.20m,
                StockQuantity = 12,
                IsFdaApproved = true,
                IsGmpCertified = true,
                CategoryId = categories.First(c => c.Name == "Cardiology").Id,
                ImageUrl = "/images/lisinopril.png"
            },
            new Medicine
            {
                Name = "Insulin Glargine Pen",
                Dosage = "100 units/mL",
                Manufacturer = "Sanofi Specialty",
                PackSize = "5-Pack Pens",
                SKU = "PH-INS-GLA-005PK",
                Price = 245.00m,
                StockQuantity = 45,
                IsColdChain = true,
                IsFdaApproved = true,
                CategoryId = categories.First(c => c.Name == "Endocrinology").Id,
                ImageUrl = "/images/insulin.png"
            }
        );
        context.SaveChanges();
    }
}

app.Run();