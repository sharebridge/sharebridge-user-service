namespace SharingBridge.UserService;

public static class RequestAuth
{
    public static (string? UserId, IResult? Error) RequireUser(
        HttpRequest request,
        TokenService tokens,
        string pathUserId)
    {
        var headerUserId = tokens.TryGetSubFromAuthorization(request.Headers.Authorization.ToString());
        if (headerUserId is null)
        {
            return (null, Results.Json(
                new ErrorBody
                {
                    Code = "missing_auth_context",
                    Message = "A valid Bearer token is required."
                },
                statusCode: 401));
        }

        if (!string.Equals(headerUserId, pathUserId, StringComparison.Ordinal))
        {
            return (null, Results.Json(
                new ErrorBody
                {
                    Code = "user_id_mismatch",
                    Message = "user_id in URL does not match the authenticated user_id."
                },
                statusCode: 403));
        }

        return (headerUserId, null);
    }
}
