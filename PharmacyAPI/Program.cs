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

// 3.  ⁄—Ì› ”Ì«”… «·‹ CORS ( √ﬂœ √‰Â« ﬁ»· «·‹ Authentication)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// 4. ≈⁄œ«œ«  ‰Ÿ«„ «·√„«‰ JWT „⁄ «· Õﬁﬁ „‰ «·ﬁÌ„ (Null Check)
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

// «· √ﬂœ „‰ ÊÃÊœ ≈⁄œ«œ«  «·‹ JWT · Ã‰»  Êﬁ› «·”Ì—›—
if (string.IsNullOrEmpty(jwtKey) || string.IsNullOrEmpty(jwtIssuer))
{
    throw new Exception("≈⁄œ«œ«  «·‹ JWT (Key √Ê Issuer) „›ﬁÊœ… ›Ì „·› appsettings.json!");
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

// 5. ≈⁄œ«œ«  Swagger
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
        Description = "œŒ· «· Êﬂ‰ ÂÊ‰: «ﬂ » Bearer À„ „”ÿ—… Ê»⁄œÂ« «· Êﬂ‰"
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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowAll"); // «·‹ CORS œ«∆„« ﬁ»· «·‹ Auth

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 7.  ÕœÌÀ «·»Ì«‰«  «· Ã—Ì»Ì… (Seed Data) · ÿ«»ﬁ «·Ê«ÃÂ…
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();

    // ≈÷«›… «·√’‰«› ≈–« ·„  ﬂ‰ „ÊÃÊœ…
    if (!context.Categories.Any())
    {
        context.Categories.AddRange(
            new Category { Name = "Antibiotics" },
            new Category { Name = "Cardiology" },
            new Category { Name = "Endocrinology" }
        );
        context.SaveChanges();
    }

    // ≈÷«›… √œÊÌ…  ÿ«»ﬁ ’Ê—… «·Ê«ÃÂ… »«·Ÿ»ÿ
    if (!context.Medicines.Any())
    {
        var categories = context.Categories.ToList();

        context.Medicines.AddRange(
            new Medicine
            {
                Name = "Amoxicillin 500mg Capsules",
                SKU = "PH-AMX-500-100CT", //
                Price = 42.50m,
                StockQuantity = 500,
                IsFdaApproved = true,
                CategoryId = categories.First(c => c.Name == "Antibiotics").Id,
                ImageUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuCSF0mMCXg6O49Pfux84YxGdU2l1wQoh2mNdF28hZ7fab6MQB_przQYQvaJqrvGuA7XBdOHK7wpI7IT3O4CVUoURW9MWmuiIVNseETizaFYZqG5QMwatSGx11IByEWcxieDCpClr6SHo_3ZXpPrMj9Se5OcFdfK_MGEtG08K279bGQ89mc9up1Xbh1D3tXgT76ZUnoZEh4ANWZziDD9eecx0krE_kjmuUzXAFHzisdMIgretM_1L_WU6uGJBIKJC7bQNyZ9YvHlFkNX"
            },
            new Medicine
            {
                Name = "Lisinopril 10mg Tablets",
                SKU = "PH-LIS-010-500CT", //
                Price = 15.20m,
                StockQuantity = 12, // Low stock ·ÌŸÂ— «· ‰»ÌÂ »«·Ê«ÃÂ…
                IsFdaApproved = true,
                CategoryId = categories.First(c => c.Name == "Cardiology").Id,
                ImageUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuBIK7bz7PPSbTd8XXQcOCksSD2DmHPMNBq1IfMQnnq966i3_zx_b4nNbYIlB_0IX6sqpnwmk5c-RYDaOlIKR9Zm1IaNCvsZ8_-Ikk0uBWZhCuTALRDH_fmGTphak1WKIswRmuBCvB-SGJ3urzhNJqpGifCh3a2ALybzJgYCm4sRhoecptqYOWIO8yHvxtmNjsgiQXgDADIPdNciWwvJFMfnkiH-TsFgv8670-i9pUrXuOwxON4y4TJm5m-JouJelh1E_t7iauFQIjbC"
            },
            new Medicine
            {
                Name = "Insulin Glargine Pen (5-Pack)",
                SKU = "PH-INS-GLA-005PK", //
                Price = 245.00m,
                StockQuantity = 45,
                IsColdChain = true, //  ÿ·»  »—Ìœ
                CategoryId = categories.First(c => c.Name == "Endocrinology").Id,
                ImageUrl = "https://lh3.googleusercontent.com/aida-public/AB6AXuDk__S0BGcAo9OXbTZychryhayOepIStTVFYni-OPLuaM62OaOEoZxKWDEwmR9LienYW97GHD3yMX91ZknvFj2HUkLvSZ7YZsT3uJms6V-vjKXaylMy0j-rZ3edI_noyjKhMvV3TYZ1ojZsysLCXvNHW38aBLc7t3zPlVxFIObdrRNAF5iOaV3rdg-jXmvoXz8JNeEJ4XU6doxkvZnmmX8yxiDWQgCwFCqsUqob1V93JzfujqqISsAS31jpHxK7r2tF7drkAtluTjQw"
            }
        );
        context.SaveChanges();
    }
}

app.Run();