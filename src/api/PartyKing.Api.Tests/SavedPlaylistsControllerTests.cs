using System.Reflection;
using Microsoft.AspNetCore.Mvc.Routing;
using PartyKing.API.Authorization;
using PartyKing.API.Controllers;

namespace PartyKing.Api.Tests;

/// <summary>Saved playlists belong to an account, so every action needs the account cookie and access to the session.</summary>
public class SavedPlaylistsControllerTests
{
    private static IEnumerable<string> ActionNames => typeof(SavedPlaylistsController)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Select(x => x.Name);

    public static TheoryData<string> Actions => new(ActionNames);

    [Theory]
    [MemberData(nameof(Actions))]
    public void EveryAction_NeedsAnAccount(string action)
    {
        var method = typeof(SavedPlaylistsController).GetMethod(action)!;

        Assert.NotNull(method.GetCustomAttribute<AuthorizeAccountAttribute>());
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public void EveryAction_NeedsAccessToTheSession(string action)
    {
        var method = typeof(SavedPlaylistsController).GetMethod(action)!;

        Assert.NotNull(method.GetCustomAttribute<RequireSessionAccessAttribute>());
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public void EveryAction_IsRoutedUnderTheSession(string action)
    {
        var method = typeof(SavedPlaylistsController).GetMethod(action)!;

        var route = method.GetCustomAttributes<HttpMethodAttribute>().Single().Template;

        Assert.StartsWith("sessions/{sessionId:guid}/saved-playlists", route);
    }

    [Fact]
    public void TheControllerHasTheActionsThatSaveAndPreview()
    {
        Assert.Equal(["GetPreview", "SaveQueue"], ActionNames.Order());
    }
}
