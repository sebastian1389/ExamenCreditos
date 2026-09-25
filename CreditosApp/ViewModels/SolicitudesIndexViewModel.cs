using System;
using System.Collections.Generic;
using CreditosApp.Models;

namespace CreditosApp.ViewModels;

public class SolicitudesIndexViewModel
{
    public IReadOnlyList<SolicitudCredito> Solicitudes { get; set; } = Array.Empty<SolicitudCredito>();

    public EstadoSolicitud? Estado { get; set; }

    public decimal? MontoMin { get; set; }

    public decimal? MontoMax { get; set; }

    public DateTime? FechaInicio { get; set; }

    public DateTime? FechaFin { get; set; }
}
