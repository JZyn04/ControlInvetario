using back.Auth;
using back.Contracts;
using back.Data;
using back.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace back.Controllers;

[ApiController]
[Route("api/perfil")]
[Authorize(Policy = "EmpresaSeleccionada")]
[AutoValidateAntiforgeryToken]
public sealed class PerfilController(InventoryDbContext database, CurrentUsuario current,
    IPasswordHasher<Usuario> hasher, AdministracionEmpresa administracion) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UsuarioResponse>> Get(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var usuario = await database.Usuarios.AsNoTracking().Include(item => item.Rol).SingleOrDefaultAsync(item =>
            item.Id == current.UsuarioId && item.EmpresaId == current.EmpresaId, cancellationToken);
        return usuario is null ? NotFound() : Ok(UsuarioResponse.From(usuario));
    }

    [HttpPut]
    public async Task<ActionResult<UsuarioResponse>> Update(ActualizarPerfilRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await administracion.Begin(cancellationToken, permiso: null);
        if (transaction is null) return Forbid();
        if (!string.IsNullOrEmpty(request.Contrasenia) && !administracion.UsuarioActual!.Rol.EsAdministrador)
            return Forbid();
        var usuario = await database.Usuarios.Include(item => item.Rol).SingleAsync(item =>
            item.Id == current.UsuarioId && item.EmpresaId == current.EmpresaId, cancellationToken);
        var normalizado = AuthController.NormalizeEmail(request.Correo);
        if (await database.Usuarios.AnyAsync(item => item.EmpresaId == current.EmpresaId && item.Id != usuario.Id &&
            item.CorreoNormalizado == normalizado, cancellationToken))
            return Problem(statusCode: 409, title: "Ese correo ya tiene una cuenta en esta empresa.");
        var cambianCredenciales = PerfilUsuario.Actualizar(usuario, request.Correo, request.Telefono, request.Contrasenia, hasher);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: "23505" })
        { return Problem(statusCode: 409, title: "Ese correo ya tiene una cuenta en esta empresa."); }
        await transaction.CommitAsync(cancellationToken);
        if (cambianCredenciales) await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(UsuarioResponse.From(usuario));
    }
}
