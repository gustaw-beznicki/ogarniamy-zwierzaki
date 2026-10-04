namespace ogarniamy_zwierzaki_api.Auth;

// HeaderName is the request header that must carry Token on capture mutations.
public sealed record AntiforgeryTokenResponse(string Token, string HeaderName);
