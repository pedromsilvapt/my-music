using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(opts =>
    {
        opts.Servers = [new ScalarServer("http://localhost:5000/api")];
        opts.EnabledTargets = [ScalarTarget.JavaScript, ScalarTarget.Shell, ScalarTarget.Python, ScalarTarget.CSharp];
        opts.Theme = ScalarTheme.Purple;
        opts.DefaultHttpClient = new KeyValuePair<ScalarTarget, ScalarClient>(ScalarTarget.JavaScript, ScalarClient.Fetch);
    });
}

// app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();