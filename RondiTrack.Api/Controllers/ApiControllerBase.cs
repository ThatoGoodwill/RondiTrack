using Microsoft.AspNetCore.Mvc;

namespace RondiTrack.Api.Controllers;

// Empty on purpose since Assignment 4.3: error formatting moved entirely to
// RondiTrackExceptionHandler. Controllers throw; they never format a response by hand.
public abstract class ApiControllerBase : ControllerBase
{
}
