using CreditosApp.Data;
using CreditosApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    // Seed user with role 'Analista'
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

    const string roleName = "Analista";
    if (!await roleManager.RoleExistsAsync(roleName))
    {
        await roleManager.CreateAsync(new IdentityRole(roleName));
    }

    var seedEmail = "analista@creditosexamen.com";
    var seedPassword = "Analista123!";
    var analista = await userManager.FindByEmailAsync(seedEmail);
    if (analista is null)
    {
        analista = new IdentityUser
        {
            UserName = seedEmail,
            Email = seedEmail,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(analista, seedPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(analista, roleName);
        }
    }

    // Seed clientes y solicitudes
    if (!await db.Clientes.AnyAsync())
    {
        var cliente1 = new Cliente
        {
            UsuarioId = analista!.Id,
            IngresosMensuales = 3500.50m,
            Activo = true
        };

        var cliente2 = new Cliente
        {
            UsuarioId = analista!.Id,
            IngresosMensuales = 1800.00m,
            Activo = true
        };

        db.Clientes.AddRange(cliente1, cliente2);
        await db.SaveChangesAsync();

        db.SolicitudesCredito.AddRange(
            new SolicitudCredito
            {
                ClienteId = cliente1.Id,
                MontoSolicitado = 12000.00m,
                FechaSolicitud = DateTime.Now,
                Estado = EstadoSolicitud.Pendiente
            },
            new SolicitudCredito
            {
                ClienteId = cliente2.Id,
                MontoSolicitado = 5000.00m,
                FechaSolicitud = DateTime.Now.AddDays(-5),
                Estado = EstadoSolicitud.Aprobado
            });

        await db.SaveChangesAsync();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();
