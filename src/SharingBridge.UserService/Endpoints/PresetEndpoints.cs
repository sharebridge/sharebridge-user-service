namespace SharingBridge.UserService;

public static class PresetEndpoints
{
    public static IEndpointRouteBuilder MapPresetEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/users/{userId}/donor-presets", GetPresets);
        app.MapGet("/v1/users/{userId}/initiator-presets", GetPresets);
        app.MapPut("/v1/users/{userId}/donor-presets", PutPresets);
        app.MapPut("/v1/users/{userId}/initiator-presets", PutPresets);
        app.MapPost("/v1/users/{userId}/donor-presets/delete-item", DeletePresetItem);
        app.MapPost("/v1/users/{userId}/initiator-presets/delete-item", DeletePresetItem);
        return app;
    }

    private static async Task<IResult> GetPresets(
        string userId,
        HttpRequest request,
        IUserStore store,
        TokenService tokens,
        CancellationToken ct)
    {
        var auth = RequestAuth.RequireUser(request, tokens, userId);
        if (auth.Error is not null)
        {
            return auth.Error;
        }

        var presets = await store.ListDonorPresetsAsync(auth.UserId!, ct);
        return Results.Json(new { presets });
    }

    private static async Task<IResult> PutPresets(
        string userId,
        PresetsBody body,
        HttpRequest request,
        IUserStore store,
        TokenService tokens,
        CancellationToken ct)
    {
        var auth = RequestAuth.RequireUser(request, tokens, userId);
        if (auth.Error is not null)
        {
            return auth.Error;
        }

        if (body.Presets is null)
        {
            return Results.Json(
                new ErrorBody { Code = "invalid_request", Message = "presets must be an array." },
                statusCode: 400);
        }

        for (var i = 0; i < body.Presets.Count; i++)
        {
            var validationError = DonorPresetUtils.ValidatePreset(body.Presets[i], i);
            if (validationError is not null)
            {
                return Results.Json(
                    new ErrorBody { Code = "invalid_request", Message = validationError },
                    statusCode: 400);
            }
        }

        await store.GetOrCreateUserAsync(auth.UserId!, null, null, ct);
        var presets = await store.ReplaceDonorPresetsAsync(auth.UserId!, body.Presets, ct);
        return Results.Json(new { presets });
    }

    private static async Task<IResult> DeletePresetItem(
        string userId,
        DeletePresetBody body,
        HttpRequest request,
        IUserStore store,
        TokenService tokens,
        CancellationToken ct)
    {
        var auth = RequestAuth.RequireUser(request, tokens, userId);
        if (auth.Error is not null)
        {
            return auth.Error;
        }

        if (string.IsNullOrWhiteSpace(body.RestaurantName))
        {
            return Results.Json(
                new ErrorBody { Code = "invalid_request", Message = "restaurant_name is required." },
                statusCode: 400);
        }

        if (string.IsNullOrWhiteSpace(body.OrderUrl))
        {
            return Results.Json(
                new ErrorBody { Code = "invalid_request", Message = "order_url is required." },
                statusCode: 400);
        }

        await store.GetOrCreateUserAsync(auth.UserId!, null, null, ct);
        var presets = await store.DeleteDonorPresetAsync(
            auth.UserId!, body.RestaurantName.Trim(), body.OrderUrl.Trim(), ct);
        return Results.Json(new { presets });
    }
}
