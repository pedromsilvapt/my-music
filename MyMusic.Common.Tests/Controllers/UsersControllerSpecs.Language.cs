using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyMusic.Common.Entities;
using MyMusic.Common.Services;
using MyMusic.Server.Controllers;
using MyMusic.Server.DTO.Users;
using NSubstitute;
using Shouldly;

namespace MyMusic.Common.Tests.Controllers;

public partial class UsersControllerSpecs
{
    [Fact]
    public async Task UpdateCurrentUser_ValidLanguage_PersistsValue()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        // Change the admin user's language to Portuguese
        var request = new UpdateUserRequest { Language = "pt" };

        var result = await controller.UpdateCurrentUser(scenario.DbContext, request, CancellationToken.None);

        // Should return 200 OK with the updated language
        var okResult = result.Result.ShouldBeOfType<OkObjectResult>();
        var response = okResult.Value.ShouldBeOfType<GetUserResponse>();
        response.User.Language.ShouldBe("pt");

        // Should persist to the database
        var user = await scenario.DbContext.Users.FirstAsync(u => u.Id == scenario.AdminUser.Id);
        user.Language.ShouldBe("pt");
    }

    [Fact]
    public async Task UpdateCurrentUser_InvalidLanguage_ReturnsBadRequest()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);
        var originalLanguage = scenario.AdminUser.Language;

        var request = new UpdateUserRequest { Language = "fr" };

        var result = await controller.UpdateCurrentUser(scenario.DbContext, request, CancellationToken.None);

        // Should reject the unsupported language
        result.Result.ShouldBeOfType<BadRequestObjectResult>();

        // Should leave the existing value untouched
        var user = await scenario.DbContext.Users.FirstAsync(u => u.Id == scenario.AdminUser.Id);
        user.Language.ShouldBe(originalLanguage);
    }

    [Fact]
    public async Task UpdateCurrentUser_OmittedLanguage_LeavesExistingValue()
    {
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        // Seed a non-default language
        scenario.AdminUser.Language = "pt";
        await scenario.DbContext.SaveChangesAsync(CancellationToken.None);

        // Patch an unrelated field (volume) without sending language
        var request = new UpdateUserRequest { Volume = 0.5 };

        await controller.UpdateCurrentUser(scenario.DbContext, request, CancellationToken.None);

        // Language should remain unchanged
        var user = await scenario.DbContext.Users.FirstAsync(u => u.Id == scenario.AdminUser.Id);
        user.Language.ShouldBe("pt");
        user.Volume.ShouldBe(0.5);
    }

    [Fact]
    public async Task UpdateCurrentUser_NewUser_DefaultsToEnglish()
    {
        // New users should default to 'en' even though the migration seeds existing rows
        var scenario = new Scenario();
        var controller = CreateController(scenario);

        var request = new CreateUserRequest
        {
            User = new CreateUserRequest.UserData
            {
                Username = "languser",
                Name = "Lang User",
            },
        };

        var response = await controller.Create(scenario.DbContext, request, CancellationToken.None);

        response.User.Language.ShouldBe("en");
    }
}