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
[Route("api/usuarios")]
[Authorize(Policy = Permisos.AdministrarEmpresa)]
[AutoValidateAntiforgeryToken]
public sealed class UsuariosController(InventoryDbContext database, CurrentUsuario current,
    IPasswordHasher<Usuario> hasher, AdministracionEmpresa administracion) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UsuarioResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var usuarios = await database.Usuarios.AsNoTracking().Include(item => item.Rol)
            .Where(item => item.EmpresaId == current.EmpresaId)
            .OrderByDescending(item => item.Rol.CodigoSistema == Rol.Administrador).ThenBy(item => item.Correo)
            .ToListAsync(cancellationToken);
        return Ok(usuarios.Select(UsuarioResponse.From).ToArray());
    }

    [HttpPost]
    public async Task<ActionResult<UsuarioResponse>> Create(CrearUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await administracion.Begin(cancellationToken);
        if (transaction is null) return Forbid();
        var rol = await database.Roles.SingleOrDefaultAsync(item => item.Id == request.RolId &&
            item.EmpresaId == current.EmpresaId, cancellationToken);
        if (rol is null) return Problem(statusCode: 400, title: "Elegí un rol de esta empresa.");
        var email = AuthController.NormalizeEmail(request.Correo);
        if (await database.Usuarios.AnyAsync(
            item => item.CorreoNormalizado == email && item.EmpresaId == current.EmpresaId, cancellationToken))
            return Problem(statusCode: 409, title: "Ese correo ya tiene una cuenta en esta empresa.");

        var usuario = new Usuario
        {
            EmpresaId = current.EmpresaId,
            Correo = request.Correo.Trim(),
            CorreoNormalizado = email,
            Rol = rol,
            EsPrincipal = rol.EsAdministrador
        };
        usuario.ContraseniaHash = hasher.HashPassword(usuario, request.Contrasenia);
        database.Usuarios.Add(usuario);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: "23505" })
        {
            return Problem(statusCode: 409, title: "Ese correo ya tiene una cuenta en esta empresa.");
        }
        await transaction.CommitAsync(cancellationToken);
        return StatusCode(201, UsuarioResponse.From(usuario));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UsuarioResponse>> Update(int id, ActualizarUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await administracion.Begin(cancellationToken);
        if (transaction is null) return Forbid();
        var usuario = await database.Usuarios.Include(item => item.Rol).SingleOrDefaultAsync(item =>
            item.Id == id && item.EmpresaId == current.EmpresaId, cancellationToken);
        if (usuario is null) return NotFound();
        var rol = await database.Roles.SingleOrDefaultAsync(item => item.Id == request.RolId &&
            item.EmpresaId == current.EmpresaId, cancellationToken);
        if (rol is null) return Problem(statusCode: 400, title: "Elegí un rol de esta empresa.");
        if (!rol.EsAdministrador && await EsUltimoAdministrador(usuario, cancellationToken))
            return Problem(statusCode: 409, title: "La empresa debe conservar al menos un administrador.");
        var email = AuthController.NormalizeEmail(request.Correo);
        if (await database.Usuarios.AnyAsync(item => item.Id != id && item.EmpresaId == current.EmpresaId &&
            item.CorreoNormalizado == email, cancellationToken))
            return Problem(statusCode: 409, title: "Ese correo ya tiene una cuenta en esta empresa.");

        var cambianCredenciales = email != usuario.CorreoNormalizado || !string.IsNullOrEmpty(request.Contrasenia);
        usuario.Correo = request.Correo.Trim();
        usuario.CorreoNormalizado = email;
        usuario.Rol = rol;
        usuario.RolId = rol.Id;
        usuario.EsPrincipal = rol.EsAdministrador;
        if (!string.IsNullOrEmpty(request.Contrasenia))
            usuario.ContraseniaHash = hasher.HashPassword(usuario, request.Contrasenia);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: "23505" })
        {
            return Problem(statusCode: 409, title: "Ese correo ya tiene una cuenta en esta empresa.");
        }
        await transaction.CommitAsync(cancellationToken);
        if (id == current.UsuarioId && cambianCredenciales)
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(UsuarioResponse.From(usuario));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await using var transaction = await administracion.Begin(cancellationToken);
        if (transaction is null) return Forbid();
        var usuario = await database.Usuarios.Include(item => item.Rol).SingleOrDefaultAsync(item =>
            item.Id == id && item.EmpresaId == current.EmpresaId, cancellationToken);
        if (usuario is null) return NotFound();
        if (await EsUltimoAdministrador(usuario, cancellationToken))
            return Problem(statusCode: 409, title: "La empresa debe conservar al menos un administrador.");
        database.Usuarios.Remove(usuario);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (id == current.UsuarioId)
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpPut("{id:int}/rol")]
    public async Task<ActionResult<UsuarioResponse>> AssignRole(int id, AsignarRolRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await administracion.Begin(cancellationToken);
        if (transaction is null) return Forbid();
        var usuario = await database.Usuarios.Include(item => item.Rol).SingleOrDefaultAsync(item =>
            item.Id == id && item.EmpresaId == current.EmpresaId, cancellationToken);
        if (usuario is null) return NotFound();
        var rol = await database.Roles.SingleOrDefaultAsync(item => item.Id == request.RolId &&
            item.EmpresaId == current.EmpresaId, cancellationToken);
        if (rol is null) return Problem(statusCode: 400, title: "No se puede asignar un rol de otra empresa.");
        if (!rol.EsAdministrador && await EsUltimoAdministrador(usuario, cancellationToken))
            return Problem(statusCode: 409, title: "La empresa debe conservar al menos un administrador.");
        usuario.Rol = rol;
        usuario.RolId = rol.Id;
        usuario.EsPrincipal = rol.EsAdministrador;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(UsuarioResponse.From(usuario));
    }

    private async Task<bool> EsUltimoAdministrador(Usuario usuario, CancellationToken cancellationToken) =>
        usuario.Rol.EsAdministrador && await database.Usuarios.CountAsync(item =>
            item.EmpresaId == current.EmpresaId && item.Rol.CodigoSistema == Rol.Administrador, cancellationToken) <= 1;
}
