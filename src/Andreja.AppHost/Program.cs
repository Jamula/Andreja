using Andreja.AppHost.Components;
using Andreja.AppHost.Hosting;
using Andreja.AppHost.OpenLoops;
using Andreja.AppHost.Identity;

if (await ContainerHealthProbe.TryRunAsync(args))
{
    return;
}

var migrationCommandRequested = DatabaseMigrationCommand.IsRequested(args);
var builder = WebApplication.CreateBuilder(migrationCommandRequested ? [] : args);

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
app.Use(async (context, next) =>
{
    var nonce = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
        System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
    context.Items["Andreja.CspNonce"] = nonce;
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.ContentSecurityPolicy =
            "default-src 'self'; "
            + $"script-src 'self' 'nonce-{nonce}'; "
            + "style-src 'self'; img-src 'self' data:; "
            + "connect-src 'self' ws: wss:; "
            + "object-src 'none'; base-uri 'self'; form-action 'self'; "
            + "frame-ancestors 'none'";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["Permissions-Policy"] =
            "publickey-credentials-get=(self)";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        return Task.CompletedTask;
    });
    await next();
});
using var migrationCancellation = new CancellationTokenSource();
ConsoleCancelEventHandler cancelMigration = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    migrationCancellation.Cancel();
};
if (migrationCommandRequested)
{
    Console.CancelKeyPress += cancelMigration;
}

int? migrationExitCode;
try
{
    migrationExitCode = await DatabaseMigrationCommand.TryRunAsync(
        args,
        app.Services,
        Console.Out,
        migrationCancellation.Token);
}
finally
{
    if (migrationCommandRequested)
    {
        Console.CancelKeyPress -= cancelMigration;
    }
}

if (migrationExitCode is not null)
{
    Environment.ExitCode = migrationExitCode.Value;
    return;
}
await DatabaseMigrationStartupVerifier.VerifyAsync(
    app.Services,
    app.Lifetime.ApplicationStopping);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseRateLimiter();
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
