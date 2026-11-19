using Microsoft.Extensions.FileProviders;
using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var loginPogingen = new ConcurrentDictionary<string, LoginPogingen>();

var supabaseUrl = builder.Configuration["Supabase:Url"]
    ?? throw new InvalidOperationException("Supabase URL is missing.");
var supabaseKey = builder.Configuration["Supabase:Key"]
    ?? throw new InvalidOperationException("Supabase key is missing.");

var supabase = new Supabase.Client(supabaseUrl, supabaseKey);
await supabase.InitializeAsync();

builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalLogin", policy =>
        policy.WithOrigins("null", "http://localhost:5500", "http://127.0.0.1:5500")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();
var frontendPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, ".."));

if (app.Environment.IsDevelopment())
{
    app.UseCors("LocalLogin");
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(frontendPath, "Afbeeldingen")),
    RequestPath = "/Afbeeldingen"
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(frontendPath, "Levelbord")),
    RequestPath = "/Levelbord"
});

app.MapGet("/", () => Results.File(Path.Combine(frontendPath, "login.html"), "text/html"));
app.MapGet("/login.css", () => Results.File(Path.Combine(frontendPath, "login.css"), "text/css"));
app.MapGet("/index", () => Results.File(Path.Combine(builder.Environment.ContentRootPath, "index.html"), "text/html"));

app.MapPost("/login", async (LoginRequest gegevens, HttpContext context) =>
{
    var ip = context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "onbekend";
    var pogingen = loginPogingen.GetOrAdd(ip, _ => new LoginPogingen());
        IResult MisluktePogingen()
        {
            pogingen.Aantal++;
            if (pogingen.Aantal >= 3)
            {
                pogingen.GeblokkeerdTot = DateTimeOffset.UtcNow.AddMinutes(1);
                return Results.Json(new { message = "3 mislukte pogingen. Wacht 1 minuut voordat je het opnieuw porbeert."},
                    statusCode: 429);
            }
            return Results.Unauthorized();
        }
        await pogingen.Slot.WaitAsync();
        try
        {
            if (pogingen.GeblokkeerdTot > DateTimeOffset.UtcNow)
            {
                return Results.Json(new { message = $"Te veel inlogpogingen. Probeer het opnieuw na {pogingen.GeblokkeerdTot:HH:mm:ss} UTC." },
                    statusCode: 429); 
            }
            if (pogingen.GeblokkeerdTot != DateTimeOffset.MinValue)
            {
                pogingen.Aantal = 0;
                pogingen.GeblokkeerdTot = DateTimeOffset.MinValue;
            }
    if (string.IsNullOrWhiteSpace(gegevens.Email) ||
        string.IsNullOrWhiteSpace(gegevens.Password))
    {
        return Results.BadRequest("Vul je e-mailadres en wachtwoord in.");
    }

    try
    {
        var session = await supabase.Auth.SignIn(gegevens.Email, gegevens.Password);

        if (session?.User is null)
        {
            return MisluktePogingen();
        }

        pogingen.Aantal = 0;
        pogingen.GeblokkeerdTot = DateTimeOffset.MinValue;
        return Results.Ok("Inloggegevens kloppen.");
    }
    catch (Supabase.Gotrue.Exceptions.GotrueException fout)
    {
        Console.WriteLine(fout.Message);
        return MisluktePogingen();
    }
    }
    finally
    {
        pogingen.Slot.Release();
    }
});

app.Run();
