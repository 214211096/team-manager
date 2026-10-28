using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TeamManager.Api.Dtos;
using TeamManager.Api.Entities;
using TeamManager.Api.Services;

namespace TeamManager.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize(Roles = nameof(UserRole.TeamLead))]
public class SettingsController(SettingsService settings) : ControllerBase
{
    [HttpGet]
    public Task<AppSettingsDto> Get() => settings.GetAsync();

    [HttpPut]
    public async Task<AppSettingsDto> Save(AppSettingsDto dto)
    {
        await settings.SaveAsync(dto);
        return await settings.GetAsync();
    }
}
