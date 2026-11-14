using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
using Radzen.Blazor;
using StemStudio.Web.Services;

namespace StemStudio.Web.Tests.Services;

public sealed class DialogConfirmServiceTests : BunitContext
{
    public DialogConfirmServiceTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddRadzenComponents();
        Services.AddScoped<IConfirmService, DialogConfirmService>();
    }

    [Theory]
    [InlineData("confirm-dialog-ok", true)]
    [InlineData("confirm-dialog-cancel", false)]
    public async Task Confirm_OpensRadzenDialog_AndReturnsChoice(string button, bool expected)
    {
        var host = Render<RadzenDialog>();
        var service = Services.GetRequiredService<IConfirmService>();

        var answer = host.InvokeAsync(() => service.ConfirmAsync("Delete mix", "Delete \"Guitar practice\"?", "Delete"));

        host.WaitForAssertion(() => host.Find("[data-testid=confirm-dialog-message]").TextContent.ShouldBe("Delete \"Guitar practice\"?"));
        host.Find("[data-testid=confirm-dialog-ok]").TextContent.Trim().ShouldBe("Delete");
        await host.Find($"[data-testid={button}]").ClickAsync(new());

        (await answer).ShouldBe(expected);
    }
}
