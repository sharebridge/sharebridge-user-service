namespace SharingBridge.UserService;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/auth/google", HandleGoogleAuth);
        return app;
    }

    private static async Task<IResult> HandleGoogleAuth(
        GoogleAuthRequest body,
        IUserStore store,
        GoogleAuthService googleAuth,
        TokenService tokens,
        DataAccessOptions dataAccessOptions,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Auth");
        try
        {
            var idToken = body.IdToken?.Trim() ?? "";
            var accessToken = body.AccessToken?.Trim() ?? "";
            if (idToken.Length == 0 && accessToken.Length == 0)
            {
                return Results.Json(
                    new ErrorBody { Code = "invalid_request", Message = "id_token or access_token is required." },
                    statusCode: 400);
            }

            var clientType = string.IsNullOrWhiteSpace(body.ClientType)
                ? "web"
                : body.ClientType.Trim().ToLowerInvariant();

            // Prefer access_token when both are present (previous Node behaviour).
            var profile = accessToken.Length > 0
                ? await googleAuth.VerifyAccessTokenAsync(accessToken, ct)
                : await googleAuth.VerifyIdTokenAsync(idToken, ct);

            if (string.IsNullOrWhiteSpace(profile.Email))
            {
                return Results.Json(
                    new ErrorBody
                    {
                        Code = "invalid_request",
                        Message = "Google account must expose an email address."
                    },
                    statusCode: 400);
            }

            var user = await DbRetry.ExecuteAsync(
                token => store.FindOrCreateGoogleUserAsync(
                    profile.GoogleSub, profile.Email, profile.Name, profile.Picture, token),
                logger,
                dataAccessOptions,
                ct);
            await DbRetry.ExecuteAsync(
                token => store.EnsureRoleAsync(user.Id, Roles.Donor, token),
                logger,
                dataAccessOptions,
                ct);
            var roles = await DbRetry.ExecuteAsync(
                token => store.GetRolesForUserAsync(user.Id, token),
                logger,
                dataAccessOptions,
                ct);
            var roleError = Roles.ClientRoleError(clientType, roles);
            if (roleError is not null)
            {
                return Results.Json(roleError, statusCode: 403);
            }

            var role = Roles.RoleForClientType(clientType, roles);
            var token = tokens.Mint(user.Id, role, roles);
            user.Role = role;
            return Results.Json(new AuthResponse { Token = token, TokenType = "Bearer", User = user });
        }
        catch (Exception ex) when (ex.Message.Contains("invalid_request", StringComparison.Ordinal))
        {
            return Results.Json(
                new ErrorBody { Code = "invalid_request", Message = ex.Message },
                statusCode: 400);
        }
        catch (Exception ex) when (IsDatabaseFailure(ex))
        {
            logger.LogError(ex, "Database error during Google sign-in");
            return Results.Json(
                new ErrorBody
                {
                    Code = "database_unavailable",
                    Message = "Could not reach the user database. Try again shortly."
                },
                statusCode: 503);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Google sign-in failed");
            return Results.Json(
                new ErrorBody
                {
                    Code = "invalid_google_token",
                    Message = string.IsNullOrWhiteSpace(ex.Message) ? "Google sign-in failed." : ex.Message
                },
                statusCode: 401);
        }
    }

    private static bool IsDatabaseFailure(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException!)
        {
            if (current is Npgsql.NpgsqlException or Npgsql.PostgresException)
            {
                return true;
            }

            var message = current.Message ?? "";
            if (message.Contains("Exception while reading from stream", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Failed to connect", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (current.InnerException is null)
            {
                break;
            }
        }

        return false;
    }
}
