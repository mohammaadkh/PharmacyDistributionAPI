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

// 3.  ⁄—Ì› ”Ì«”… «·‹ CORS ··—»ÿ „⁄ «·›—Ê‰ -¬‰œ
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// 4. ≈⁄œ«œ«  ‰Ÿ«„ «·√„«‰ JWT Authentication ( ⁄œÌ· ÃÊÂ—Ì ÂÊ‰)
// ”Õ»‰« «·ﬁÌ„ „»«‘—… „‰ appsettings.json ·÷„«‰ «· ÿ«»ﬁ
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

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

// 5. ≈⁄œ«œ«  Swagger „⁄ ≈÷«›… „Ì“… «·‹ Authorize («·ﬁ›·)
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
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var app = builder.Build();

// 6. ≈⁄œ«œ«  Œÿ «·⁄„· (Middleware)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 7.  ⁄»∆… »Ì«‰«   Ã—Ì»Ì… (Seed Data)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();

    if (!context.Categories.Any())
    {
        context.Categories.Add(new Category { Name = "Pain Relief" });
        context.SaveChanges();
    }

    if (!context.Medicines.Any())
    {
        var category = context.Categories.First();
        context.Medicines.Add(new Medicine
        {
            Name = "Panadol",
            Description = "Effective and fast pain relief",
            Price = 15.5m,
            Quantity = 100,
            CategoryId = category.Id,
            ImageUrl = "/images/default.png"
        });
        context.SaveChanges();
    }
}

app.Run();