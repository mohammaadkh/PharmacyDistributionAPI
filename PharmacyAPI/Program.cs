using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data; //  √ﬂœ √‰ «·„”«— ’ÕÌÕ

var builder = WebApplication.CreateBuilder(args);

// √÷› Â–Â «·√”ÿ— Â‰« ﬁ»· builder.Build
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//  √ﬂœ „‰ ÊÃÊœ Â–« «·”ÿ— ÊÊ÷ÊÕÂ
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
