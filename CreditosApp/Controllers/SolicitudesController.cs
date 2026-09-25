using System;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using CreditosApp.Data;
using CreditosApp.Hubs;
using CreditosApp.Models;
using CreditosApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace CreditosApp.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    public const string UltimaSolicitudIdSessionKey = "UltimaSolicitudId";
    public const string UltimaSolicitudMontoSessionKey = "UltimaSolicitudMonto";
    public const string UltimaSolicitudUsuarioIdSessionKey = "UltimaSolicitudUsuarioId";

    private const string CacheVersionPrefix = "Solicitudes:Version:";
    private const string CacheListPrefix = "Solicitudes:List:";
    private const int CacheDurationSeconds = 60;

    private readonly ApplicationDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly IHubContext<SolicitudesHub> _solicitudesHub;

    public SolicitudesController(
        ApplicationDbContext context,
        IDistributedCache cache,
        IHubContext<SolicitudesHub> solicitudesHub)
    {
        _context = context;
        _cache = cache;
        _solicitudesHub = solicitudesHub;
    }

    public async Task<IActionResult> Index(SolicitudesIndexViewModel filtros)
    {
        filtros ??= new SolicitudesIndexViewModel();
        ValidarFiltros(filtros);

        if (!ModelState.IsValid)
        {
            filtros.Solicitudes = Array.Empty<SolicitudCredito>();
            return View(filtros);
        }

        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            return Challenge();
        }

        var cacheVersion = await GetCacheVersionAsync(usuarioId);
        var cacheKey = GetCacheKey(usuarioId, cacheVersion, filtros);
        var cachedValue = await _cache.GetStringAsync(cacheKey);

        if (cachedValue is not null)
        {
            try
            {
                var cachedSolicitudes = JsonSerializer.Deserialize<List<SolicitudCredito>>(cachedValue);
                if (cachedSolicitudes is not null)
                {
                    filtros.Solicitudes = cachedSolicitudes;
                    return View(filtros);
                }
            }
            catch (JsonException)
            {
                await _cache.RemoveAsync(cacheKey);
            }
        }

        var consulta = _context.SolicitudesCredito
            .AsNoTracking()
            .Where(s => s.Cliente!.UsuarioId == usuarioId);

        if (filtros.Estado.HasValue)
        {
            consulta = consulta.Where(s => s.Estado == filtros.Estado.Value);
        }

        if (filtros.MontoMin.HasValue)
        {
            consulta = consulta.Where(s => s.MontoSolicitado >= filtros.MontoMin.Value);
        }

        if (filtros.MontoMax.HasValue)
        {
            consulta = consulta.Where(s => s.MontoSolicitado <= filtros.MontoMax.Value);
        }

        if (filtros.FechaInicio.HasValue)
        {
            var fechaInicio = filtros.FechaInicio.Value.Date;
            consulta = consulta.Where(s => s.FechaSolicitud >= fechaInicio);
        }

        if (filtros.FechaFin.HasValue)
        {
            var fechaFinExclusiva = filtros.FechaFin.Value.Date.AddDays(1);
            consulta = consulta.Where(s => s.FechaSolicitud < fechaFinExclusiva);
        }

        var solicitudes = await consulta
            .OrderByDescending(s => s.FechaSolicitud)
            .ThenByDescending(s => s.Id)
            .Select(s => new SolicitudCredito
            {
                Id = s.Id,
                ClienteId = s.ClienteId,
                MontoSolicitado = s.MontoSolicitado,
                FechaSolicitud = s.FechaSolicitud,
                Estado = s.Estado,
                MotivoRechazo = s.MotivoRechazo
            })
            .ToListAsync();

        filtros.Solicitudes = solicitudes;
        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(solicitudes),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheDurationSeconds)
            });

        return View(filtros);
    }

    public async Task<IActionResult> Detalle(int id)
    {
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            return Challenge();
        }

        var solicitud = await _context.SolicitudesCredito
            .AsNoTracking()
            .Include(s => s.Cliente)
            .ThenInclude(c => c!.Usuario)
            .Where(s => s.Id == id && s.Cliente!.UsuarioId == usuarioId)
            .SingleOrDefaultAsync();

        if (solicitud is null)
        {
            return NotFound();
        }

        HttpContext.Session.SetInt32(UltimaSolicitudIdSessionKey, solicitud.Id);
        HttpContext.Session.SetString(UltimaSolicitudMontoSessionKey, solicitud.MontoSolicitado.ToString("C"));
        HttpContext.Session.SetString(UltimaSolicitudUsuarioIdSessionKey, usuarioId);

        return View(solicitud);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            return Challenge();
        }

        var solicitud = new SolicitudCreditoCreateViewModel();
        await CargarClientesActivosAsync(solicitud, usuarioId);

        if (solicitud.Clientes.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "No tienes clientes activos registrados.");
        }

        return View(solicitud);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SolicitudCreditoCreateViewModel solicitud)
    {
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "No se pudo registrar la solicitud. Revisa los datos ingresados.";
            await CargarClientesActivosAsync(solicitud, usuarioId);
            return View(solicitud);
        }

        var clienteId = solicitud.ClienteId!.Value;
        var cliente = await _context.Clientes
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == clienteId && c.UsuarioId == usuarioId && c.Activo);

        if (cliente is null)
        {
            const string mensaje = "El cliente seleccionado no existe o no está activo.";
            TempData["ErrorMessage"] = mensaje;
            ModelState.AddModelError(nameof(solicitud.ClienteId), mensaje);
            await CargarClientesActivosAsync(solicitud, usuarioId);
            return View(solicitud);
        }

        var montoMaximo = cliente.IngresosMensuales * 10m;
        if (solicitud.MontoSolicitado!.Value > montoMaximo)
        {
            var mensaje = $"El monto solicitado no puede superar 10 veces tus ingresos mensuales ({montoMaximo:C}).";
            TempData["ErrorMessage"] = mensaje;
            ModelState.AddModelError(nameof(solicitud.MontoSolicitado), mensaje);
            await CargarClientesActivosAsync(solicitud, usuarioId);
            return View(solicitud);
        }

        var tieneSolicitudPendiente = await _context.SolicitudesCredito
            .AsNoTracking()
            .AnyAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);

        if (tieneSolicitudPendiente)
        {
            const string mensaje = "El cliente seleccionado ya tiene una solicitud en estado Pendiente.";
            TempData["ErrorMessage"] = mensaje;
            ModelState.AddModelError(nameof(solicitud.ClienteId), mensaje);
            await CargarClientesActivosAsync(solicitud, usuarioId);
            return View(solicitud);
        }

        var nuevaSolicitud = new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = solicitud.MontoSolicitado.Value,
            FechaSolicitud = DateTime.Now,
            Estado = EstadoSolicitud.Pendiente,
            MotivoRechazo = null
        };

        _context.SolicitudesCredito.Add(nuevaSolicitud);

        await _context.SaveChangesAsync();
        await InvalidarCacheSolicitudesAsync(usuarioId);

        TempData["SuccessMessage"] = "La solicitud de crédito se registró correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Analista")]
    public async Task<IActionResult> ActualizarEstado(int id, EstadoSolicitud estado, string? motivoRechazo)
    {
        var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(usuarioId))
        {
            return Challenge();
        }

        if (id <= 0 || !Enum.IsDefined(estado))
        {
            return BadRequest();
        }

        if (estado == EstadoSolicitud.Rechazado && string.IsNullOrWhiteSpace(motivoRechazo))
        {
            TempData["ErrorMessage"] = "Debes indicar el motivo del rechazo.";
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .SingleOrDefaultAsync(s => s.Id == id);

        if (solicitud?.Cliente is null)
        {
            return NotFound();
        }

        var propietarioUsuarioId = solicitud.Cliente.UsuarioId;
        if (string.IsNullOrWhiteSpace(propietarioUsuarioId))
        {
            return NotFound();
        }

        solicitud.Estado = estado;
        solicitud.MotivoRechazo = estado == EstadoSolicitud.Rechazado
            ? motivoRechazo?.Trim()
            : null;

        await _context.SaveChangesAsync();
        await InvalidarCacheSolicitudesAsync(propietarioUsuarioId);

        await _solicitudesHub.Clients.User(propietarioUsuarioId).SendAsync(
            "SolicitudEstadoActualizado",
            new
            {
                SolicitudId = solicitud.Id,
                Estado = solicitud.Estado.ToString(),
                MotivoRechazo = solicitud.MotivoRechazo
            });

        TempData["SuccessMessage"] = "El estado de la solicitud se actualizó correctamente.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    private async Task<string> GetCacheVersionAsync(string usuarioId)
    {
        var versionKey = GetCacheVersionKey(usuarioId);
        var cacheVersion = await _cache.GetStringAsync(versionKey);
        if (!string.IsNullOrWhiteSpace(cacheVersion))
        {
            return cacheVersion;
        }

        cacheVersion = Guid.NewGuid().ToString("N");
        await SetCacheVersionAsync(usuarioId, cacheVersion);
        return cacheVersion;
    }

    private async Task SetCacheVersionAsync(string usuarioId, string cacheVersion)
    {
        await _cache.SetStringAsync(
            GetCacheVersionKey(usuarioId),
            cacheVersion,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(CacheDurationSeconds)
            });
    }

    private static string GetCacheVersionKey(string usuarioId)
    {
        return $"{CacheVersionPrefix}{usuarioId}";
    }

    private static string GetCacheKey(string usuarioId, string cacheVersion, SolicitudesIndexViewModel filtros)
    {
        var estado = filtros.Estado?.ToString() ?? "todos";
        var montoMin = filtros.MontoMin?.ToString(CultureInfo.InvariantCulture) ?? "todos";
        var montoMax = filtros.MontoMax?.ToString(CultureInfo.InvariantCulture) ?? "todos";
        var fechaInicio = filtros.FechaInicio?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "todas";
        var fechaFin = filtros.FechaFin?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "todas";

        return $"{CacheListPrefix}{usuarioId}:{cacheVersion}:{estado}:{montoMin}:{montoMax}:{fechaInicio}:{fechaFin}";
    }

    private async Task InvalidarCacheSolicitudesAsync(string usuarioId)
    {
        await _cache.RemoveAsync(GetCacheVersionKey(usuarioId));
    }

    private async Task CargarClientesActivosAsync(SolicitudCreditoCreateViewModel solicitud, string usuarioId)
    {
        solicitud.Clientes = await _context.Clientes
            .AsNoTracking()
            .Where(c => c.UsuarioId == usuarioId && c.Activo)
            .OrderBy(c => c.Id)
            .ToListAsync();
    }

    private void ValidarFiltros(SolicitudesIndexViewModel filtros)
    {
        if (filtros.MontoMin.HasValue && filtros.MontoMin.Value < 0)
        {
            ModelState.AddModelError(nameof(filtros.MontoMin), "El monto mínimo no puede ser negativo.");
        }

        if (filtros.MontoMax.HasValue && filtros.MontoMax.Value < 0)
        {
            ModelState.AddModelError(nameof(filtros.MontoMax), "El monto máximo no puede ser negativo.");
        }

        if (filtros.MontoMin.HasValue && filtros.MontoMax.HasValue && filtros.MontoMin.Value > filtros.MontoMax.Value)
        {
            ModelState.AddModelError(nameof(filtros.MontoMin), "El monto mínimo no puede ser mayor que el monto máximo.");
        }

        if (filtros.FechaInicio.HasValue && filtros.FechaFin.HasValue && filtros.FechaInicio.Value > filtros.FechaFin.Value)
        {
            ModelState.AddModelError(nameof(filtros.FechaInicio), "La fecha de inicio no puede ser posterior a la fecha de fin.");
        }
    }
}
