using AuditLog.Core.Models;
using AuditLog.Core.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

builder.Services.AddDbContext<AuditLogContext>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var auditLogService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
    var context = scope.ServiceProvider.GetRequiredService<AuditLogContext>();
}

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
