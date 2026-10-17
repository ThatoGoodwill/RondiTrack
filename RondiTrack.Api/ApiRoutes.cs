namespace RondiTrack.Api;

// ONE place that says which API version every route lives under. Controllers never type "api/v1".
public static class ApiRoutes
{
    public const string Version = "v1";
    public const string Base = "api/" + Version;   // [Route(ApiRoutes.Base + "/stokvels")]
    public const string Root = "/" + Base;         // absolute routes and the cookie path: "/api/v1"
}