using System;
using System.Security.Claims;
using System.Threading.Tasks;
using CreditosApp.Data;
using CreditosApp.Models;
using CreditosApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditosApp.Controllers;

[Authorize]
public class SolicitudesController : Controller
{
    private readonly ApplicationDbContext _context;

    public SolicitudesController(ApplicationDbContext context)
    {
        _context = context;
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

        var consulta = _context.SolicitudesCredito
            .AsNoTracking()
            .Include(s => s.Cliente)
            .ThenInclude(c => c!.Usuario)
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

        filtros.Solicitudes = await consulta
            .OrderByDescending(s => s.FechaSolicitud)
            .ThenByDescending(s => s.Id)
            .ToListAsync();

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

        _context.SolicitudesCredito.Add(new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = solicitud.MontoSolicitado.Value,
            FechaSolicitud = DateTime.Now,
            Estado = EstadoSolicitud.Pendiente,
            MotivoRechazo = null
        });

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "La solicitud de crédito se registró correctamente.";
        return RedirectToAction(nameof(Index));
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
