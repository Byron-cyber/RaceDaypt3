using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Image uploads (event banners and profile pictures) go through the API into blob storage.</summary>
public class UploadTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public UploadTests(ApiFactory factory) => _factory = factory;

    // Smallest thing that starts like a real PNG / JPEG (the API checks the file signature, not just the name)
    private static readonly byte[] Png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 1, 2, 3, 4 };
    private static readonly byte[] Jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 0x4A, 0x46, 0x49, 0x46, 0, 1, 1, 2, 3 };

    private static MultipartFormDataContent File(byte[] bytes, string fileName, string contentType)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Organiser_CanUploadABanner_AndItIsServedBackThroughTheApi()
    {
        var organiser = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(organiser);

        var response = await organiser.Client.PostAsync($"/api/events/{ev.EventId}/banner", File(Png, "banner.png", "image/png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EventDto>();
        Assert.StartsWith("/api/images/events/", updated!.ImageUrl);
        Assert.EndsWith(".png", updated.ImageUrl);

        // The image is really in (fake) blob storage and can be fetched by anyone through the API
        var image = await _factory.CreateClient().GetAsync(updated.ImageUrl);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Png, await image.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ReplacingABanner_RemovesTheOldImage()
    {
        var organiser = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(organiser);
        var first = await (await organiser.Client.PostAsync($"/api/events/{ev.EventId}/banner", File(Png, "a.png", "image/png")))
            .Content.ReadFromJsonAsync<EventDto>();

        var second = await (await organiser.Client.PostAsync($"/api/events/{ev.EventId}/banner", File(Jpeg, "b.jpg", "image/jpeg")))
            .Content.ReadFromJsonAsync<EventDto>();

        Assert.NotEqual(first!.ImageUrl, second!.ImageUrl);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().GetAsync(first.ImageUrl!)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().GetAsync(second.ImageUrl!)).StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUploadABanner_Returns403()
    {
        var organiser = await _factory.NewOrganiser();
        var participant = await _factory.NewParticipant();
        var ev = await _factory.NewEvent(organiser);

        var response = await participant.Client.PostAsync($"/api/events/{ev.EventId}/banner", File(Png, "banner.png", "image/png"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUploadABannerForSomeoneElsesEvent_Returns403()
    {
        var owner = await _factory.NewOrganiser();
        var other = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(owner);

        var response = await other.Client.PostAsync($"/api/events/{ev.EventId}/banner", File(Png, "banner.png", "image/png"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotUploadABanner_Returns401()
    {
        var response = await _factory.CreateClient().PostAsync("/api/events/1/banner", File(Png, "banner.png", "image/png"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_ForMissingEvent_Returns404()
    {
        var organiser = await _factory.NewOrganiser();
        var response = await organiser.Client.PostAsync("/api/events/999999/banner", File(Png, "banner.png", "image/png"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_WithANonImageFile_Returns400()
    {
        var organiser = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(organiser);
        var notAnImage = System.Text.Encoding.UTF8.GetBytes("this is just a text file pretending to be a picture");

        var response = await organiser.Client.PostAsync($"/api/events/{ev.EventId}/banner", File(notAnImage, "evil.png", "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_ThatIsTooLarge_Returns400()
    {
        var organiser = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(organiser);
        var huge = new byte[6 * 1024 * 1024];
        Png.CopyTo(huge, 0);

        var response = await organiser.Client.PostAsync($"/api/events/{ev.EventId}/banner", File(huge, "huge.png", "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_WhenStorageIsDown_Returns503_AndLeavesTheEventUnchanged()
    {
        var organiser = await _factory.NewOrganiser();
        var ev = await _factory.NewEvent(organiser);

        _factory.Blobs.FailUploads = true;
        try
        {
            var response = await organiser.Client.PostAsync($"/api/events/{ev.EventId}/banner", File(Png, "banner.png", "image/png"));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        finally
        {
            _factory.Blobs.FailUploads = false;
        }

        var after = await _factory.CreateClient().GetFromJsonAsync<EventDto>($"/api/events/{ev.EventId}");
        Assert.Null(after!.ImageUrl);
    }

    [Fact]
    public async Task User_CanUploadAProfilePicture()
    {
        var participant = await _factory.NewParticipant();

        var response = await participant.Client.PostAsync("/api/users/me/profile-picture", File(Jpeg, "me.jpg", "image/jpeg"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await participant.Client.GetFromJsonAsync<UserDto>("/api/users/me");
        Assert.StartsWith("/api/images/profiles/", profile!.ProfilePictureUrl);
        Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().GetAsync(profile.ProfilePictureUrl!)).StatusCode);
    }

    [Fact]
    public async Task ProfilePictureUpload_RequiresLogin()
    {
        var response = await _factory.CreateClient().PostAsync("/api/users/me/profile-picture", File(Jpeg, "me.jpg", "image/jpeg"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/images/secrets/passwords.txt")]
    [InlineData("/api/images/events/does-not-exist.png")]
    public async Task Images_OutsideTheAllowedFolders_OrMissing_Return404(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
