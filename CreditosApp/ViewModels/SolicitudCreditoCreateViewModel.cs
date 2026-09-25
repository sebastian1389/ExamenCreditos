using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CreditosApp.Models;

namespace CreditosApp.ViewModels;

public class SolicitudCreditoCreateViewModel
{
    [Required(ErrorMessage = "Selecciona un cliente.")]
    [Display(Name = "Cliente")]
    public int? ClienteId { get; set; }

    [Required(ErrorMessage = "Ingresa el monto solicitado.")]
    [Range(typeof(decimal), "0.01", "9999999999999999.99", ErrorMessage = "El monto solicitado debe ser mayor que cero y estar dentro del límite permitido.")]
    [Display(Name = "Monto solicitado")]
    public decimal? MontoSolicitado { get; set; }

    public IReadOnlyList<Cliente> Clientes { get; set; } = Array.Empty<Cliente>();
}
