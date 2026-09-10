using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Server.InputPorts;
using Server.Models;
using Server.Utils;

namespace Server.Controllers;

[ApiController]
[Route("/api/v1/user/")]
public class AuthController(IUserRepository userRepository, ITokenGenerator tokenGenerator,
    IEmailSender emailSender) : ControllerBase
{
    private const int ExpireMinutes = 5;
    private const int MaxLoginAttempts = 5;
    private const int PasswordChangeDays = 90;

    [HttpPost("confirmCode")]
    public Task<ActionResult<AuthResponse>> ConfirmCode([FromBody] Confirm2FaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ConfirmCodeInner(request);
    }

    [HttpPost("resendCode")]
    public Task<ActionResult> ResendCode([FromBody] RegisterOrLoginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ResendCodeInner(request);
    }

    [HttpPost("register")]
    public Task<ActionResult> Register([FromBody] RegisterOrLoginRequest registerRequest)
    {
        ArgumentNullException.ThrowIfNull(registerRequest);
        return RegisterInner(registerRequest);
    }

    [HttpPost("login")]
    public Task<ActionResult> Login([FromBody] RegisterOrLoginRequest loginRequest)
    {
        ArgumentNullException.ThrowIfNull(loginRequest);
        return LoginInner(loginRequest);
    }

    [Authorize]
    [HttpPatch("update")]
    public Task<ActionResult> UpdateUser([FromBody] RegisterOrLoginRequest updateRequest)
    {
        ArgumentNullException.ThrowIfNull(updateRequest);
        return UpdateUserInner(updateRequest);
    }

    [Authorize]
    [HttpDelete("delete")]
    public async Task<ActionResult> DeleteUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId is null)
            return Unauthorized();

        var user = await userRepository.FindUserAsync(Guid.Parse(userId)).ConfigureAwait(false);

        if (user is null)
            return NotFound();

        if (user.GameId is not null)
            return Conflict();

        await userRepository.DeleteUserAsync(user).ConfigureAwait(false);

        return Ok();
    }

    private async Task<ActionResult<AuthResponse>> ConfirmCodeInner([FromBody] Confirm2FaRequest request)
    {
        var user = await userRepository.FindUserByNameAsync(request.Username).ConfigureAwait(false);
        if (user is null)
            return NotFound();

        if (user.Code != request.Code ||
            user.TwoFactorExpire < DateTimeOffset.UtcNow.DateTime)
        {
            return Unauthorized("Invalid or expired code");
        }

        user.Code = null;
        user.TwoFactorExpire = null;
        await userRepository.UpdateUserAsync(user).ConfigureAwait(false);

        var jwt = tokenGenerator.CreateJwtToken(user.Id, user.Name);

        return Ok(new AuthResponse(jwt, user));
    }

    private async Task<ActionResult> ResendCodeInner([FromBody] RegisterOrLoginRequest request)
    {
        var user = await userRepository.FindUserByNameAsync(request.Username).ConfigureAwait(false);
        if (user is null)
            return NotFound();

        user.Email = request.Email;
        var emailCode = TwoFactorService.GenerateCode();
        user.Code = emailCode;
        user.TwoFactorExpire = DateTimeOffset.UtcNow.AddMinutes(ExpireMinutes).DateTime;
        await userRepository.UpdateUserAsync(user).ConfigureAwait(false);

        await emailSender.Send(user.Email,
            "Code for email confirmation in Bang!",
            emailCode).ConfigureAwait(false);
        return Ok();
    }

    private async Task<ActionResult> RegisterInner([FromBody] RegisterOrLoginRequest registerRequest)
    {
        if (await userRepository.FindUserByNameAsync(registerRequest.Username).ConfigureAwait(false) is not null)
            return Conflict();
        var id = Guid.NewGuid();
        var password = PasswordHasher.HashPassword(registerRequest.Password);
        var emailCode = TwoFactorService.GenerateCode();
        var user = new User(id, registerRequest.Username, registerRequest.Email, password, null)
        {
            Code = emailCode,
            TwoFactorExpire = DateTimeOffset.UtcNow.AddMinutes(ExpireMinutes).DateTime
        };
        await userRepository.AddUserAsync(user).ConfigureAwait(false);
        if (registerRequest.NeedTwoFactor)
        {
            await emailSender.Send(user.Email,
                "Code for email confirmation in Bang!",
                emailCode).ConfigureAwait(false);
            return Ok();
        }

        var jwt = tokenGenerator.CreateJwtToken(user.Id, user.Name);
        var response = new AuthResponse(jwt, user);
        return Ok(response);
    }

    private async Task<ActionResult> LoginInner([FromBody] RegisterOrLoginRequest loginRequest)
    {
        var user = await userRepository.FindUserByNameAsync(loginRequest.Username).ConfigureAwait(false);
        if (user is null)
            return NotFound();
        if (user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow.DateTime)
            return Unauthorized($"Account locked until {user.LockoutEnd}");
        if (!PasswordHasher.VerifyPassword(loginRequest.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;

            if (user.FailedLoginCount >= MaxLoginAttempts)
                user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(ExpireMinutes).DateTime;

            await userRepository.UpdateUserAsync(user).ConfigureAwait(false);
            return Unauthorized("Invalid password");
        }
        user.FailedLoginCount = 0;
        user.LockoutEnd = null;

        var code = TwoFactorService.GenerateCode();
        user.Code = code;
        user.TwoFactorExpire = DateTimeOffset.UtcNow.AddMinutes(ExpireMinutes).DateTime;

        await userRepository.UpdateUserAsync(user).ConfigureAwait(false);

        if (loginRequest.NeedTwoFactor)
        {
            await emailSender.Send(
                user.Email,
                "Your 2FA code for Bang!",
                $"{code}").ConfigureAwait(false);

            if ((DateTimeOffset.UtcNow.DateTime - user.LastPasswordChange).TotalDays >= PasswordChangeDays)
                return Ok("Please, update your password");
            return Ok();
        }

        var jwt = tokenGenerator.CreateJwtToken(user.Id, user.Name);
        var response = new AuthResponse(jwt, user);
        return Ok(response);
    }

    private async Task<ActionResult> UpdateUserInner([FromBody] RegisterOrLoginRequest updateRequest)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userId is null)
            return Unauthorized();

        var user = await userRepository.FindUserAsync(Guid.Parse(userId)).ConfigureAwait(false);

        if (user is null)
            return NotFound();

        if (user.GameId is not null)
            return Conflict("Can't update when user is in game");

        var userWithSameName = await userRepository.FindUserByNameAsync(updateRequest.Username).ConfigureAwait(false);

        if (userWithSameName is not null)
            return Conflict("User with this name already exists");

        if (!PasswordHasher.VerifyPassword(updateRequest.Password, user.PasswordHash))
            user.LastPasswordChange = DateTimeOffset.UtcNow.DateTime;

        user.Name = updateRequest.Username;
        user.Email = updateRequest.Email;
        user.PasswordHash = PasswordHasher.HashPassword(updateRequest.Password);
        await userRepository.UpdateUserAsync(user).ConfigureAwait(false);

        return Ok();
    }
}
