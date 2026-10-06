using System.Security.Claims;
using back.Auth;
using back.Contracts;
using back.Data;
using back.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers;

[ApiController]
[Route("api/auth")]
[AutoValidateAntiforgeryToken]
public sealed class AuthController(InventoryDbContext database, IPasswordHasher<Usuario> hasher)
    : ControllerBase
{
    [HttpGet("csrf")]
    public IActionResult Csrf([FromServices] IAntiforgery antiforgery)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    }

    [HttpPost("register")]
    public async Task<ActionResult<SesionResponse>> Register(
        RegistrarEmpresaRequest request, CancellationToken cancellationToken)
    {
        var usuario = new Usuario
        {
            Correo = request.Correo.Trim(),
            CorreoNormalizado = NormalizeEmail(request.Correo),
            EsPrincipal = true,
            Empresa = new Empresa
            {
                Nombre = request.NombreEmpresa.Trim(),
                Telefono = request.Telefono.Trim(),
                TelefonoOpcional = string.IsNullOrWhiteSpace(request.TelefonoOpcional) ? null : request.TelefonoOpcional.Trim()
            }
        };
        var roles = Rol.CrearSistema(usuario.Empresa);
        database.Roles.AddRange(roles);
        usuario.Rol = roles[0];
        usuario.ContraseniaHash = hasher.HashPassword(usuario, request.Contrasenia);
        database.Usuarios.Add(usuario);
        // Cada registro crea una empresa y una cuenta principal independientes.
        await database.SaveChangesAsync(cancellationToken);
        await SignIn([usuario], usuario);
        return Ok(SessionFor([usuario], usuario.Id));
    }

    [HttpPost("login")]
    public async Task<ActionResult<SesionResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Correo);
        var candidates = await database.Usuarios.Include(item => item.Empresa).Include(item => item.Rol)
            .Where(item => item.CorreoNormalizado == email).ToListAsync(cancellationToken);
        var matches = new List<Usuario>();
        foreach (var usuario in candidates)
        {
            var result = hasher.VerifyHashedPassword(usuario, usuario.ContraseniaHash, request.Contrasenia);
            if (result == PasswordVerificationResult.Failed) continue;
            if (result == PasswordVerificationResult.SuccessRehashNeeded)
                usuario.ContraseniaHash = hasher.HashPassword(usuario, request.Contrasenia);
            matches.Add(usuario);
        }
        if (matches.Count == 0)
            return Problem(statusCode: 401, title: "Correo o contraseña incorrectos.");

        await database.SaveChangesAsync(cancellationToken);
        var activa = matches.Count == 1 ? matches[0] : null;
        await SignIn(matches, activa);
        return Ok(SessionFor(matches, activa?.Id));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<SesionResponse>> Me(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var usuarios = await AllowedAccounts(cancellationToken);
        var active = User.HasClaim(claim => claim.Type == "EmpresaId") ? UsuarioId : (int?)null;
        return Ok(SessionFor(usuarios, active));
    }

    [Authorize]
    [HttpPost("company")]
    public async Task<ActionResult<SesionResponse>> SelectCompany(
        SeleccionarEmpresaRequest request, CancellationToken cancellationToken)
    {
        // Solo se puede elegir una cuenta cuya contraseña se verificó al iniciar sesión.
        var usuarios = await AllowedAccounts(cancellationToken);
        var activa = usuarios.SingleOrDefault(item => item.EmpresaId == request.EmpresaId);
        if (activa is null)
            return Problem(statusCode: 403, title: "No tenés una cuenta habilitada en esa empresa.");
        await SignIn(usuarios, activa);
        return Ok(SessionFor(usuarios, activa.Id));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    private int UsuarioId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<List<Usuario>> AllowedAccounts(CancellationToken cancellationToken)
    {
        var ids = User.FindAll("CuentaPermitida").Select(claim => int.Parse(claim.Value)).ToArray();
        var usuarios = await database.Usuarios.Include(item => item.Empresa).Include(item => item.Rol)
            .Where(item => ids.Contains(item.Id)).ToListAsync(cancellationToken);
        return usuarios.Where(item => CredencialesUsuario.Verificada(User, item)).ToList();
    }

    private Task SignIn(IReadOnlyList<Usuario> cuentas, Usuario? activa)
    {
        var identity = activa ?? cuentas[0];
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identity.Id.ToString()),
            new(ClaimTypes.Name, identity.Correo)
        };
        claims.AddRange(cuentas.Select(item => new Claim("CuentaPermitida", item.Id.ToString())));
        claims.AddRange(cuentas.Select(CredencialesUsuario.CrearClaim));
        if (activa is not null)
        {
            claims.Add(new Claim("EmpresaId", activa.EmpresaId.ToString()));
        }
        return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = false });
    }

    internal static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
    private static SesionResponse SessionFor(IReadOnlyList<Usuario> cuentas, int? activaId)
    {
        var usuario = cuentas.FirstOrDefault(item => item.Id == activaId) ?? cuentas[0];
        var empresas = cuentas.OrderBy(item => item.Empresa.Nombre)
            .Select(item => new EmpresaResponse(item.EmpresaId, item.Empresa.Nombre,
                item.Empresa.Telefono, item.Empresa.TelefonoOpcional, RolResponse.From(item.Rol))).ToArray();
        return new SesionResponse(UsuarioResponse.From(usuario),
            activaId is null ? null : empresas.Single(item => item.Id == usuario.EmpresaId), empresas);
    }
}
