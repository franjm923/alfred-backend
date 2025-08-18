using Microsoft.EntityFrameworkCore;
using Alfred2.DBContext;
using Alfred2.OpenAIService;
using Alfred2.Models;

var builder = WebApplication.CreateBuilder(args);

// EF Core (SQL Server)
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Servicios propios
builder.Services.AddHttpClient<OpenAIChatService>();
builder.Services.AddHttpClient("whatsapp");
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
