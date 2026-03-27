using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. ÑÈØ ŞÇÚÏÉ ÇáÈíÇäÇÊ SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. ÅÚÏÇÏÇÊ ÇáÜ JSON (áÍãÇíÉ ÇáÚáÇŞÇÊ ÇáÏÇÆÑíÉ ÚÔÇä ÇáãíÏíÓä æÇáßÇÊíÌæÑí)
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// 3. ÊÚÑíİ ÓíÇÓÉ ÇáÜ CORS (ÚÔÇä ÑİíŞß íÑÈØ ãä ÇáÜ Frontend ÈÏæä ãÔÇßá)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// 4. ÅÚÏÇÏÇÊ äÙÇã ÇáÃãÇä JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey)) jwtKey = "YourSuperSecretKey1234567890123456"; // ãİÊÇÍ ÇÍÊíÇØí ááØæÇÑÆ

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 5. ÅÚÏÇÏÇÊ ÎØ ÇáÚãá (Middleware Pipeline) - ÇáÊÑÊíÈ åæä "ãŞÏÓ"
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ÊİÚíá ÇáãáİÇÊ ÇáËÇÈÊÉ (ÚÔÇä ÕæÑ ÇáÃÏæíÉ ÈãÌáÏ wwwroot ÊÙåÑ)
app.UseStaticFiles();

// ÊİÚíá ÇáÜ CORS
app.UseCors("AllowAll");

// ÊİÚíá äÙÇã ÇáÊÍŞŞ ãä ÇáåæíÉ
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 6. ÊÚÈÆÉ ÈíÇäÇÊ ÊÌÑíÈíÉ (Seed Data) Ãæá ãÇ íÔÊÛá ÇáãÔÑæÚ
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // ÊÃßÏ Åä ŞÇÚÏÉ ÇáÈíÇäÇÊ æÇáÌÏæá ãæÌæÏíä
    context.Database.EnsureCreated();

    // ÅÖÇİÉ Õäİ ÊÌÑíÈí ÅĞÇ ÇáÌÏæá İÇÖí
    if (!context.Categories.Any())
    {
        context.Categories.Add(new Category { Name = "Pain Relief" });
        context.SaveChanges();
    }

    // ÅÖÇİÉ Ãæá ÏæÇÁ (ÈÇäÏæá) ãÑÈæØ ÈÇáÕäİ
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