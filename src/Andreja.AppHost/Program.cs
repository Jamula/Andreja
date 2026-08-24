using Andreja.AppHost.Components;
using Andreja.AppHost.Hosting;
using Andreja.AppHost.OpenLoops;
using Andreja.AppHost.Identity;

if (await ContainerHealthProbe.TryRunAsync(args))
{
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddAndrejaFoundation(builder.Configuration);
builder.Services.AddAndrejaOperations(builder.Configuration);
builder.Services.AddAndrejaOpenLoops(builder.Configuration, builder.Environment);
builder.Services.ConfigureAndrejaCookieBehavior();
builder.Host.UseDefaultServiceProvider((context, options) =>
{
    options.ValidateScopes = context.HostingEnvironment.IsDevelopment();
    options.ValidateOnBuild = true;
});

var app = builder.Build();
await app.ApplyAndrejaOpenLoopsMigrationsAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.Use(TaskRequestContext.ResolveAsync);
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapAndrejaOperationalEndpoints();
app.MapLocalAccountEndpoints(app.Environment);
if (app.Configuration.GetValue<bool>($"{OpenLoopsOptions.SectionName}:Enabled"))
{
    app.MapOpenLoopsEndpoints();
}

app.Run();

public partial class Program;
