using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.FileProviders;
using ChatServer.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();

app.UseStaticFiles();

app.MapControllers();
app.MapHub<ChatHub>("/chatHub");

app.Run("http://0.0.0.0:5000");
