using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PharmacyAPI.Data; //  √ﬂœ √‰ Â–« «·„”«— Ìÿ«»ﬁ „Ã·œ «·»Ì«‰«  ⁄‰œﬂ
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. —»ÿ ﬁ«⁄œ… «·»Ì«‰«  SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. ≈⁄œ«œ«  «·‹ JSON (·Õ„«Ì… «·⁄·«ﬁ«  «·œ«∆—Ì…)
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

// 3.  ⁄—Ì› ”Ì«”… «·‹ CORS (⁄‘«‰ —›Ìﬁﬂ Ìﬁœ— Ì”Õ» »Ì«‰«  „‰ «·‹ Frontend)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// 4. ≈⁄œ«œ«  ‰Ÿ«„ «·√„«‰ JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
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

// 5. ≈⁄œ«œ«  Œÿ «·⁄„· (Middleware Pipeline) - «· — Ì» ÂÊ‰ "„ﬁœ”"
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

//  ›⁄Ì· «·‹ CORS √Ê·«
app.UseCors("AllowAll");

//  ›⁄Ì· ‰Ÿ«„ «· Õﬁﬁ „‰ «·ÂÊÌ… ( Authentication ﬁ»· Authorization)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();