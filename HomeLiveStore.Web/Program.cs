using HomeLiveStore.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddStoreApi(builder.Configuration, builder.Environment);

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseStoreApi();
app.MapStaticAssets();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
await app.InitializeStoreDatabaseAsync();
app.Run();
