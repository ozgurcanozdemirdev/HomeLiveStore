using HomeLiveStore.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddStoreApi(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseStoreApi();
await app.InitializeStoreDatabaseAsync();

app.Run();

public partial class Program;
