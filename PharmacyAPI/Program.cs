using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. —»ÿ ﬁ«⁄œ… «·»Ì«‰«  SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. ≈⁄œ«œ«  «·‹ JSON ·Õ„«Ì… «·⁄·«ﬁ«  «·œ«∆—Ì…
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// 3.  ⁄—Ì› ”Ì«”… «·‹ CORS ·÷„«‰ « ’«· ›—Ê‰ -≈‰œ (React) »œÊ‰ „‘«ﬂ·
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// 4. ≈⁄œ«œ«  ‰Ÿ«„ «·√„«‰ JWT
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrEmpty(jwtKey) || string.IsNullOrEmpty(jwtIssuer))
{
    throw new Exception("≈⁄œ«œ«  «·‹ JWT „›ﬁÊœ… ›Ì „·› appsettings.json!");
}

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

// 5. ≈⁄œ«œ«  Swagger ·œ⁄„ «Œ »«— «· Êﬂ‰
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
        Description = "√œŒ· «· Êﬂ‰ Â‰«: «ﬂ » Bearer À„ „”«›… À„ «· Êﬂ‰"
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

var app = builder.Build();

// 6.  — Ì» «·‹ Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseStaticFiles(); // Â–« «·”ÿ— Ì”„Õ »«·Ê’Ê· ·„Ã·œ wwwroot
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 7.  ÕœÌÀ «·»Ì«‰«  «· Ã—Ì»Ì… (Seed Data) · ÿ«»ﬁ «·Ê«ÃÂ… 100%
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();

    // ≈÷«›… «·√’‰«›
    if (!context.Categories.Any())
    {
        context.Categories.AddRange(
            new Category { Name = "Antibiotics" },
            new Category { Name = "Cardiology" },
            new Category { Name = "Endocrinology" }
        );
        context.SaveChanges();
    }

    // ≈÷«›… «·√œÊÌ… „⁄ «·ÕﬁÊ· «·ÃœÌœ… («·Ã—⁄…° «·‘—ﬂ…° «·⁄»Ê…)
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
                StockQuantity = 12, // ÌŸÂ—  ‰»ÌÂ Low Stock
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